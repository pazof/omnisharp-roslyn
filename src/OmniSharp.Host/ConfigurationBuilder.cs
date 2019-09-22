using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using OmniSharp.Internal;
using OmniSharp.Utilities;

namespace OmniSharp
{
    public class ConfigurationBuilder : IConfigurationBuilder
    {
        private readonly IOmniSharpEnvironment _environment;
        private readonly IConfigurationBuilder _builder;

        public ConfigurationBuilder(IOmniSharpEnvironment environment)
        {
            _environment = environment;
            _builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(Constants.ConfigFile, optional: true);
        }

        public IConfigurationBuilder Add(IConfigurationSource source)
        {
            _builder.Add(source);
            return this;
        }

        public IConfigurationRoot Build()
        {
            var configBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(Constants.ConfigFile, optional: true)
                .AddEnvironmentVariables("OMNISHARP_");

            if (_environment.AdditionalArguments?.Length > 0)
            {
                configBuilder.AddCommandLine(_environment.AdditionalArguments);
            }

            // Use the global omnisharp config if there's any in the shared path
            configBuilder.CreateAndAddGlobalOptionsFile(_environment);

            // Use the local omnisharp config if there's any in the root path
            var dirinfo = new DirectoryInfo(_environment.TargetDirectory);

            var confinfo = new FileInfo(Path.Combine(_environment.TargetDirectory, Constants.OptionsFile));

            if (!confinfo.Exists) {
                while (!confinfo.Exists && dirinfo != null)
                {
                    dirinfo = dirinfo.Parent;
                    if (dirinfo != null)
                    confinfo = new FileInfo(Path.Combine(dirinfo.FullName, Constants.OptionsFile));
                }

                if (confinfo !=null && confinfo.Exists)
                    configBuilder.AddJsonFile(
                        new PhysicalFileProvider(dirinfo.FullName).WrapForPolling(),
                        Constants.OptionsFile,
                        optional: true,
                        reloadOnChange: true);
            }
            else configBuilder.AddJsonFile(
                new PhysicalFileProvider(_environment.TargetDirectory).WrapForPolling(),
                Constants.OptionsFile,
                optional: true,
                reloadOnChange: true);

            return configBuilder.Build();
        }

        public IDictionary<string, object> Properties => _builder.Properties;
        public IList<IConfigurationSource> Sources => _builder.Sources;
    }
}
