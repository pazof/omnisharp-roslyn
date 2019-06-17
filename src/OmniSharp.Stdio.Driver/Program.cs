using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.LanguageServerProtocol;
using OmniSharp.Services;
using OmniSharp.Stdio.Eventing;
using OmniSharp.Stdio.Logging;

namespace OmniSharp.Stdio.Driver
{
    internal class Program
    {
        private static readonly IDictionary<string, Assembly> _additional =
        new Dictionary<string, Assembly>();

        static int Main(string[] args) => HostHelpers.Start(() =>
        {
        // -- https://github.com/praeclarum/sqlite-net/issues/706
        // http://stackoverflow.com/a/9180843/107625

        var dir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        foreach (var assemblyName in Directory.GetFiles(dir, @"*.dll"))
        {
            var assembly = Assembly.LoadFile(assemblyName);
            _additional.Add(assembly.GetName().Name, assembly);
        }

        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += CurrentDomain_ResolveAssembly;
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_ResolveAssembly;

            var application = new StdioCommandLineApplication();
            application.OnExecute(() =>
            {
                // If an encoding was specified, be sure to set the Console with it before we access the input/output streams.
                // Otherwise, the streams will be created with the default encoding.
                if (application.Encoding != null)
                {
                    var encoding = Encoding.GetEncoding(application.Encoding);
                    Console.InputEncoding = encoding;
                    Console.OutputEncoding = encoding;
                }

                var cancellation = new CancellationTokenSource();

                if (application.Lsp)
                {
                    Configuration.ZeroBasedIndices = true;
                    using (var host = new LanguageServerHost(
                        Console.OpenStandardInput(),
                        Console.OpenStandardOutput(),
                        application,
                        cancellation))
                    {
                        host.Start().Wait();
                        cancellation.Token.WaitHandle.WaitOne();
                    }
                }
                else
                {
                    var input = Console.In;
                    var output = Console.Out;

                    var environment = application.CreateEnvironment();
                    Configuration.ZeroBasedIndices = application.ZeroBasedIndices;
                    var configuration = new ConfigurationBuilder(environment).Build();
                    var writer = new SharedTextWriter(output);
                    var serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(environment, configuration, new StdioEventEmitter(writer),
                        configureLogging: builder => builder.AddStdio(writer));

                    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                    var assemblyLoader = serviceProvider.GetRequiredService<IAssemblyLoader>();

                    var plugins = application.CreatePluginAssemblies();

                    var compositionHostBuilder = new CompositionHostBuilder(serviceProvider)
                        .WithOmniSharpAssemblies()
                        .WithAssemblies(assemblyLoader.LoadByAssemblyNameOrPath(plugins.AssemblyNames).ToArray());

                    using (var host = new Host(input, writer, environment, serviceProvider, compositionHostBuilder, loggerFactory, cancellation))
                    {
                        host.Start();
                        cancellation.Token.WaitHandle.WaitOne();
                    }
                }

                return 0;
            });

            return application.Execute(args);
        });
        private static Assembly CurrentDomain_ResolveAssembly(object sender, ResolveEventArgs e)
    {
        // Here, I'm doing my own, automatic binding redirect.
        // (e. g. Newtonsoft.Json 6.0.0.0 to 9.0.0.0).

        var name = e.Name.Substring(0, e.Name.IndexOf(','));

        _additional.TryGetValue(name, out var res);
        return res;
    }

    }
}
