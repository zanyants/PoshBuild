using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Mono.Cecil;

namespace PoshBuild
{
    /// <summary>
    /// A specialization of <see cref="KeyedByAssemblyDocSource"/> that creates per-assembly <see cref="XmlDocSource"/>
    /// instances.
    /// </summary>
    sealed class PerAssemblyXmlDocSource : KeyedByAssemblyDocSource
    {
        protected override IDocSource CreateDocSource( TypeDefinition type, string key )
        {
            IDocSource result = null;
            var resolver = ( BuildTimeAssemblyResolver ) type.Module.AssemblyResolver;
            var loadPath = resolver.GetLoadPath( type.Module.Assembly );

            if ( loadPath == null )
            {
                if ( TaskContext.Current != null )
                    TaskContext.Current.Log.LogWarning(
                        "PoshBuild",
                        "KBA01",
                        "",
                        null, 0, 0, 0, 0,
                        "Failed to get LoadPath for {0}",
                        type.Module.Assembly.FullName );
            }
            else
            {
                // Try to find sibling doc file
                var docFile = Path.ChangeExtension( loadPath, ".xml" );
                if ( File.Exists( docFile ) )
                {
                    try
                    {
                        result = new XmlDocSource( docFile, type.Module );
                    }
                    catch ( Exception e )
                    {
                        if ( TaskContext.Current != null )
                            TaskContext.Current.Log.LogWarning(
                                "PoshBuild",
                                "KBA01",
                                "",
                                null, 0, 0, 0, 0,
                                "Failed to load documentation file {0} for assembly {1}: {2}",
                                docFile,
                                type.Module.Assembly.FullName,
                                e.Message );
                    }
                }
                else
                {
                    if ( TaskContext.Current != null )
                        TaskContext.Current.Log.LogMessage(
                            MessageImportance.High,
                            "No documentation file was found for assembly {0} at path {1}.",
                            type.Module.Assembly.FullName,
                            docFile );
                }
            }

            return result;
        }

        public override bool WriteParameterDescription( XmlWriter writer, PropertyDefinition property, string parameterSetName, IEnumerable<TypeDefinition> descendantTypes )
        {
            if ( descendantTypes != null )
            {
                foreach ( var descendantType in descendantTypes )
                {
                    var source = GetSource( descendantType ) as XmlDocSource;
                    if ( source != null && source.WriteParameterDescription( writer, property, parameterSetName, descendantType ) )
                        return true;
                }
            }

            return base.WriteParameterDescription( writer, property, parameterSetName, null );
        }
    }
}
