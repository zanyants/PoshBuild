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
        struct ResolvedAssemblyInfo
        {
            public string Path;
            public AssemblyDefinition AssemblyDefinition;
        };

        // Redefine this to MessageImportance.High to ease development-time debugging.
        const MessageImportance MessageImportanceLow = MessageImportance.Low;

        /// <summary>
        /// Additional paths to search for assemblies.
        /// </summary>
        List<string> _additionalSearchPaths;

        /// <summary>
        /// Explicitly resolved paths from the build process, keyed on assembly full name.
        /// </summary>
        Dictionary<string, string> _unloadedReferencePaths;

        /// <summary>
        /// Resolved assemblies keyed on assembly full name.
        /// </summary>
        Dictionary<string, ResolvedAssemblyInfo> _resolvedAssemblies;

        /// <summary>
        /// Public types gathered as asemblies are loaded. Value is a single <see cref="TypeDefintion"/> or
        /// a <see cref="List{TypeDefinition}"/> if there's more than one with the same name.
        /// </summary>
        Dictionary<string, object> _publicTypes;

        /// <summary>
        /// Log messages to MSBuild.
        /// </summary>
        TaskLoggingHelper _log;

        /// <summary>
        /// AppDomain used purely for applying assembly binding policy using a supplied app.config-style file.
        /// </summary>
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
            _publicTypes = new Dictionary<string, object>();

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

            _resolvedAssemblies = new Dictionary<string, ResolvedAssemblyInfo>();
        }

        void AddPublicTypes( AssemblyDefinition a )
        {
            if ( a == null )
                throw new ArgumentNullException( "assembly" );

            var publicTypes = 
                a
                .Modules
                .SelectMany( mod => mod.Types )
                .Where( t => t.IsPublic == true );

            foreach ( var type in publicTypes )
            {
                var key = type.FullName;

                object found;

                if ( _publicTypes.TryGetValue( key, out found ) )
                {
                    var list = found as List<TypeDefinition>;

                    if ( list == null )
                    {
                        list = new List<TypeDefinition>( 2 );
                        _publicTypes[ key ] = list;
                        list.Add( found as TypeDefinition );
                    }

                    list.Add( type );
                }
                else
                    _publicTypes.Add( key, type );
            }
        }

        /// <summary>
        /// Returns the path from which an assembly was loaded by this resolver, or <c>null</c> if the assembly was not loaded by this resolver.
        /// </summary>
        public string GetLoadPath( AssemblyDefinition assembly )
        {
            return _resolvedAssemblies.Values.FirstOrDefault( v => ReferenceEquals( v.AssemblyDefinition, assembly ) ).Path;
        }

        IEnumerable<TypeDefinition> _FindPublicTypeCacheOnly( string fullName )
        {
            object found;

            if ( _publicTypes.TryGetValue( fullName, out found ) )
            {
                _log.LogMessage( MessageImportanceLow, "   Found in cache." );

                var td = found as TypeDefinition;

                if ( td != null )
                    return new[] { td };

                var list = found as List<TypeDefinition>;

                if ( list != null )
                {
                    _log.LogMessage( MessageImportanceLow, "   Found multiple results ({0})", list.Count );
                    return list;
                }

                // This should never happen.
                throw new InvalidDataException();
            }

            return null;
        }

        /// <summary>
        /// Find a public type by searching in all known assemblies.
        /// </summary>
        public IEnumerable<TypeDefinition> FindPublicType( string fullName )
        {
            _log.LogMessage( MessageImportanceLow, "FindPublicType {0}", fullName );

            var foundInCache = _FindPublicTypeCacheOnly( fullName );

            if ( foundInCache != null )
                return foundInCache;

            _log.LogMessage( MessageImportanceLow, "   Not found in cache." );
            var parameters = new ReaderParameters();
            parameters.AssemblyResolver = this;

            while ( _unloadedReferencePaths.Count > 0 )
            {
                var kvp = ( ( IEnumerable<KeyValuePair<string, string>> ) _unloadedReferencePaths ).First();
                _unloadedReferencePaths.Remove( kvp.Key );
                _log.LogMessage( MessageImportanceLow, "   Loading from ReferencePath {0}", kvp.Value );
                var rai = new ResolvedAssemblyInfo() { Path = kvp.Value };
                try
                {
                    var assy = AssemblyDefinition.ReadAssembly( kvp.Value, parameters );
                    rai.AssemblyDefinition = assy;
                }
                catch ( Exception e )
                {
                    _log.LogError( "Failed to load assembly from path {0}: {1}", kvp.Value, e.Message );
                }

                _resolvedAssemblies.Add( rai.AssemblyDefinition.FullName, rai );

                if ( rai.AssemblyDefinition != null )
                {
                    AddPublicTypes( rai.AssemblyDefinition );
                }

                foundInCache = _FindPublicTypeCacheOnly( fullName );

                if ( foundInCache != null )
                    return foundInCache;
            }

            return Enumerable.Empty<TypeDefinition>();
        }

        public void AddAssembly( AssemblyDefinition assembly, string loadPath )
        {
            if ( assembly == null )
                throw new ArgumentNullException( "assembly" );

            _resolvedAssemblies.Add( assembly.FullName, new ResolvedAssemblyInfo { AssemblyDefinition = assembly, Path = loadPath } );
            AddPublicTypes( assembly );
        }

        public AssemblyDefinition Resolve( AssemblyNameReference name, ReaderParameters parameters )
        {
            _log.LogMessage( MessageImportanceLow, "Resolving {0}", name );

            parameters.AssemblyResolver = this;

            if ( _policyAppDomain != null )
            {
                var afterPolicyStr = _policyAppDomain.ApplyPolicy( name.FullName );
                _log.LogMessage( MessageImportanceLow, "   After policy: {0}", afterPolicyStr );
                name = AssemblyNameReference.Parse( afterPolicyStr );
            }

            ResolvedAssemblyInfo result;            

            if ( _resolvedAssemblies.TryGetValue( name.FullName, out result ) )
            {
                _log.LogMessage( MessageImportanceLow, "   Found in cache." );
            }
            else
            {
                string fullPath = null;

                if ( _unloadedReferencePaths.TryGetValue( name.FullName, out fullPath ) )
                {
                    _unloadedReferencePaths.Remove( name.FullName );
                    _log.LogMessage( MessageImportanceLow, "   Loading from ReferencePath {0}", fullPath );
                    try
                    {
                        var assy = AssemblyDefinition.ReadAssembly( fullPath, parameters );
                        result.Path = fullPath;
                        result.AssemblyDefinition = assy;
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
                                    result.Path = path;
                                    result.AssemblyDefinition = assy;
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
                if ( result.AssemblyDefinition != null )
                    AddPublicTypes( result.AssemblyDefinition );
            }

            if ( result.AssemblyDefinition == null )
                _log.LogMessage( MessageImportanceLow, "   Not resolved." );
            else
                _log.LogMessage( MessageImportanceLow, "   Resolved to {0}", result.AssemblyDefinition.FullName );

            return result.AssemblyDefinition;
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
