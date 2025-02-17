using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.LanguageServerProtocol;
using OmniSharp.Services;
using OmniSharp.Stdio.Eventing;

namespace OmniSharp.Stdio.Driver
{
    internal class Program
    {
        static int Main(string[] args) => HostHelpers.Start(() =>
        {
            #if LOG_COMMAND_ARGS
                FileInfo logfile = new FileInfo("omnisharp.log");
            var stream  =logfile.OpenWrite();
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(args));
            stream.Write(bytes,0, bytes.Length);
            stream.Close();
            stream.Dispose();
            #endif
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
                    var serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(environment, configuration, new StdioEventEmitter(writer));
                    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                    var plugins = application.CreatePluginAssemblies();

                    var assemblyLoader = serviceProvider.GetRequiredService<IAssemblyLoader>();
                    var compositionHostBuilder = new CompositionHostBuilder(serviceProvider)
                        .WithOmniSharpAssemblies()
                        // FIXME .WithAssemblies(assemblyLoader.LoadByAssemblyNameOrPath(plugins.AssemblyNames).ToArray())
                        ;
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
    }
}
