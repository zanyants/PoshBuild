using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace PoshBuild
{
    /// <summary>
    /// An implementation of <see cref="IAssemblyResolver"/> designed for use during the MSBuild build process.
    /// </summary>
    sealed class BuildTimeAssemblyResolver : IAssemblyResolver
    {
        // Redefine this to MessageImportance.High to ease development-time debugging.
        const MessageImportance MessageImportanceLow = MessageImportance.Low;

        // Additional paths to search for assemblies.
        List<string> _additionalSearchPaths;

        // Explicitly resolved paths from the build process, keyed on assembly full name.
        Dictionary<string, string> _unloadedReferencePaths;

        // Resolved assemblies keyed on assembly full name.
        Dictionary<string, AssemblyDefinition> _resolvedAssemblies;

        // Log messages to MSBuild.
        TaskLoggingHelper _log;

        // AppDomain used purely for applying assembly binding policy using a supplied app.config-style file.
        AppDomain _policyAppDomain;

        public static BuildTimeAssemblyResolver Create(
            ITaskItem[] referencePaths,
            ITaskItem[] additionalAssemblySearchPaths,
            ITaskItem hostConfigurationFile,
            TaskLoggingHelper log )
        {
            if ( log == null )
                throw new ArgumentNullException( "log" );

            AppDomain policyAppDomain = null;

            if ( hostConfigurationFile != null )
            {
                var hcfPath = hostConfigurationFile.GetMetadata( "FullPath" );
                if ( !File.Exists( hcfPath ) )
                {
                    log.LogError( "The host configuration file '{0}' was not found.", hcfPath );
                    return null;
                }

                // Create an app domain with the specified app.config-like file. We can then use AppDomain.ApplyPolicy(...)
                // to apply policy logic. No code is ever executed in this appdomain.
                var ads = new AppDomainSetup();
                ads.ConfigurationFile = hostConfigurationFile.GetMetadata( "FullPath" );

                policyAppDomain = AppDomain.CreateDomain( "PoshBuildPolicy", null, ads );
            }

            return new BuildTimeAssemblyResolver( referencePaths, additionalAssemblySearchPaths, policyAppDomain, log );
        }

        BuildTimeAssemblyResolver(
            ITaskItem[] referencePaths,
            ITaskItem[] additionalAssemblySearchPaths,
            AppDomain policyAppDomain,
            TaskLoggingHelper log )
        {
            _log = log;
            _policyAppDomain = policyAppDomain;
            _unloadedReferencePaths = new Dictionary<string, string>();
            if ( referencePaths != null )
            {
                foreach ( var item in referencePaths )
                {
                    var fullPath = item.GetMetadata( "FullPath" );
                    var fusionName = item.GetMetadata( "FusionName" );

                    if ( File.Exists( fullPath ) )
                    {
                        _unloadedReferencePaths.Add( fusionName, fullPath );
                    }
                    else
                    {
                        _log.LogWarning( "An item passed to the ReferencePaths parameter was not found:" );
                        _log.LogWarning( "Assembly: {0}", fusionName );
                        _log.LogWarning( "FullPath: {0}", fullPath );
                    }
                }
            }

            if ( additionalAssemblySearchPaths != null )
                _additionalSearchPaths =
                    additionalAssemblySearchPaths
                    .Select( item => item.GetMetadata( "FullPath" ) )
                    .ToList();
            else
                _additionalSearchPaths = new List<string>();

            _resolvedAssemblies = new Dictionary<string, AssemblyDefinition>();
        }

        public AssemblyDefinition Resolve( AssemblyNameReference name, ReaderParameters parameters )
        {
            _log.LogMessage( MessageImportanceLow, "Resolving {0}", name );
            if ( _policyAppDomain != null )
            {
                var afterPolicyStr = _policyAppDomain.ApplyPolicy( name.FullName );
                _log.LogMessage( MessageImportanceLow, "   After policy: {0}", afterPolicyStr );
                name = AssemblyNameReference.Parse( afterPolicyStr );
            }

            AssemblyDefinition result = null;

            if ( _resolvedAssemblies.TryGetValue( name.FullName, out result ) )
            {
                _log.LogMessage( MessageImportanceLow, "   Found in cache." );
            }
            else
            {
                string fullPath = null;

                if ( _unloadedReferencePaths.TryGetValue( name.FullName, out fullPath ) )
                {
                    _log.LogMessage( MessageImportanceLow, "   Loading from ReferencePath {0}", fullPath );
                    try
                    {
                        result = AssemblyDefinition.ReadAssembly( fullPath, parameters );
                    }
                    catch ( Exception e )
                    {
                        _log.LogError( "Failed to load assembly from path {0}: {1}", fullPath, e.Message );
                    }
                }
                else if ( _additionalSearchPaths.Count > 0 )
                {
                    _log.LogMessage( MessageImportanceLow, "   Searching additional paths:" );

                    // Try search paths
                    var dll = name.Name + ".dll";
                    var exe = name.Name + ".exe";

                    var pathsToTry =
                        _additionalSearchPaths
                        .SelectMany( path => new[] { Path.Combine( path, dll ), Path.Combine( path, exe ) } );

                    foreach ( var path in pathsToTry )
                    {
                        if ( File.Exists( path ) )
                        {
                            _log.LogMessage( MessageImportanceLow, "      Trying to load from path {0}", path );

                            AssemblyDefinition assy = null;

                            try
                            {
                                assy = AssemblyDefinition.ReadAssembly( path, parameters );
                            }
                            catch ( Exception e )
                            {
                                _log.LogError( "Failed to load assembly from path {0}: {1}", fullPath, e.Message );
                            }

                            if ( assy != null )
                            {
                                _log.LogMessage( MessageImportanceLow, "      Loaded {0}", assy.FullName );

                                if ( assy.Name.FullName == name.FullName )
                                {
                                    result = assy;
                                    _log.LogMessage( MessageImportanceLow, "      Is match." );
                                }
                                else
                                    _log.LogMessage( MessageImportanceLow, "      Does not match." );
                            }

                            break;
                        }
                        else
                            _log.LogMessage( MessageImportanceLow, "      Skipping non-existant path {0}", path );
                    }
                }

                _log.LogMessage( MessageImportanceLow, "   Adding result to cache." );
                _resolvedAssemblies.Add( name.FullName, result );
            }

            if ( result == null )
                _log.LogMessage( MessageImportanceLow, "   Not resolved." );
            else
                _log.LogMessage( MessageImportanceLow, "   Resolved to {0}", result.FullName );

            return result;
        }

        public AssemblyDefinition Resolve( string fullName, ReaderParameters parameters )
        {
            if ( fullName == null )
                throw new ArgumentNullException( "fullName" );

            return Resolve( AssemblyNameReference.Parse( fullName ), parameters );
        }

        public AssemblyDefinition Resolve( string fullName )
        {
            return Resolve( fullName, new ReaderParameters() );
        }

        public AssemblyDefinition Resolve( AssemblyNameReference name )
        {
            return Resolve( name, new ReaderParameters() );
        }
    }
}
