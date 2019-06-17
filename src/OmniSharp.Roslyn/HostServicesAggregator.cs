using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;

namespace OmniSharp
{
    [Export]
    public class HostServicesAggregator
    {
        private ImmutableArray<Assembly> _assemblies;
        private ILogger<HostServicesAggregator> _logger;

        [ImportingConstructor]
        public HostServicesAggregator(
            [ImportMany] IEnumerable<IHostServicesProvider> hostServicesProviders, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HostServicesAggregator>();
            var builder = ImmutableHashSet.CreateBuilder<Assembly>();

            // We always include the default Roslyn assemblies, which includes:
            //
            //   * Microsoft.CodeAnalysis.Workspaces
            //   * Microsoft.CodeAnalysis.CSharp.Workspaces
            //   * Microsoft.CodeAnalysis.VisualBasic.Workspaces

            foreach (var assembly in MefHostServices.DefaultAssemblies)
            {
                builder.Add(assembly);
            }

            foreach (var provider in hostServicesProviders)
            {
                foreach (var assembly in provider.Assemblies)
                {
                    try
                    {
                        var exportedTypes = assembly.ExportedTypes;
                        builder.Add(assembly);
                        _logger.LogTrace("Added {assembly} to host service assemblies references.", assembly.FullName);
                    }
                    catch (Exception ex)
                    {
                        // if we can't see exported types, it means that the assembly cannot participate
                        // in MefHostServices. Most likely cause is that one or more of its dependencies (typically a Visual Studio or GACed DLL) are missing
                        _logger.LogWarning("Expected to use {assembly} in host services but the assembly cannot be loaded due to an exception: {exceptionMessage}.", assembly.FullName, ex.Message);
                    }
                }
            }

            _assemblies = builder.ToImmutableArray();
        }

        public HostServices CreateHostServices()
        {
            HostServices services = null;
            try { 
                services = MefHostServices.Create(_assemblies);
            }
            catch (Exception ex)
            {
               _logger.LogError($"host services cannot be all loaded due to an exception: {ex.Message}.");
               return MefHostServices.DefaultHost;
            }
            return services;
        }
    }
}
