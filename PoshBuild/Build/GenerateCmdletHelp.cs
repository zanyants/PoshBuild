using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using PoshBuild.ComponentModel;

namespace PoshBuild.Build
{
    /// <summary>
    /// An MSBuild task that generates the Cmdlet help file for Cmdlets
    /// contained in a set of assemblies.
    /// </summary>
    public class GenerateCmdletHelp : AppDomainIsolatedTask
    {
        enum DocSourceNames
        {
            Descriptor,
            Reflection,
            XmlDoc
        }

        /// <summary>
        /// The binary module or snapin assembly to reflect Cmdlets from.
        /// </summary>
        [Required]
        public ITaskItem Assembly { get; set; }

        /// <summary>
        /// The compiler-generated XML documentation file. Defaults to %(Assembly.FullPath) with the extension replaced with '.xml'.
        /// </summary>
        public ITaskItem XmlDocumentationFile { get; set; }

        /// <summary>
        /// The help file to generate. Defaults to %(Assembly.FullPath)-Help.xml.
        /// </summary>
        public ITaskItem OutputHelpFile { get; set; }

        /// <summary>
        /// The sources of documentation, in order of precidence (preferred source first). The identity of each item must be Descriptor, Reflection or XmlDoc.
        /// Default is Descriptor, Reflection.
        /// </summary>
        public ITaskItem[] DocSources { get; set; }

        /// <summary>
        /// Assemblies that contain <see cref="ICmdletHelpDescriptor"/>s
        /// giving additional information on Cmdlets. This can be the
        /// same assemblies as the input assemblies.
        /// </summary>
        public ITaskItem[] DescriptorAssemblies { get; set; }

        /// <summary>
        /// The generated files, currently at most one file.
        /// </summary>
        [Output]
        public ITaskItem[] GeneratedFiles { get; set; }

        /// <summary>
        /// Creates the help files out of the given assemblies.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            using ( TaskContext.CreateScope( Log ) )
            {
                IEnumerable<DocSourceNames> docSourceNames = null;

                if ( DocSources == null || DocSources.Length == 0 )
                {
                    docSourceNames = new DocSourceNames[] { DocSourceNames.Descriptor, DocSourceNames.Reflection };
                }
                else
                {
                    try
                    {
                        docSourceNames =
                            DocSources
                            .Select( ds => ( DocSourceNames ) Enum.Parse( typeof( DocSourceNames ), ds.ItemSpec ) );
                    }
                    catch ( Exception e )
                    {
                        Log.LogError( "Failed to parse DocSources items: {0}", e.Message );
                        return false;
                    }
                }

                if ( docSourceNames.Count() != docSourceNames.Distinct().Count() )
                {
                    Log.LogError( "The DocSources items must be unique." );
                    return false;
                }

#if PB_ENABLE_DESCRIPTORS                
                IDictionary<TypeDefinition, ICmdletHelpDescriptor> descriptors = null;

                if ( DescriptorAssemblies != null && docSourceNames.Contains( DocSourceNames.Descriptor ) )
                {
                    var loadedAssemblies =
                            DescriptorAssemblies
                            .Select(
                                item =>
                                {
                                    try
                                    {
                                        return System.Reflection.Assembly.LoadFrom( item.GetMetadata( "FullPath" ) );
                                    }
                                    catch ( Exception e )
                                    {
                                        Log.LogError(
                                            "PoshBuild",
                                            "PB02",
                                            "",
                                            null,
                                            0, 0, 0, 0,
                                            "Failed to load descriptor assembly '{0}': {1}", item.GetMetadata( "FullPath" ), e.Message );

                                        return null;
                                    }
                                } );

                    if ( Log.HasLoggedErrors )
                        return false;

                    descriptors = DescriptorDocSource.GetDescriptors( loadedAssemblies );
                }
#endif

                var assemblyPath = Assembly.GetMetadata( "FullPath" );
                var assemblyName = Assembly.GetMetadata( "Filename" );

                AssemblyDefinition assembly = null;

                try
                {
                    assembly = AssemblyDefinition.ReadAssembly( assemblyPath );
                }
                catch ( Exception e )
                {
                    Log.LogError(
                        "PoshBuild",
                        "PB03",
                        "",
                        null,
                        0, 0, 0, 0,
                        "Failed to load assembly '{0}': {1}", assemblyPath, e.Message );

                    return false;
                }

                var xmlDocPath = XmlDocumentationFile == null ? null : XmlDocumentationFile.GetMetadata( "FullPath" );
                if ( string.IsNullOrEmpty( xmlDocPath ) )
                    xmlDocPath = Path.Combine( Path.GetDirectoryName( assemblyPath ), assemblyName + ".xml" );

                var helpFilePath = OutputHelpFile == null ? null : OutputHelpFile.GetMetadata( "FullPath" );
                if ( string.IsNullOrEmpty( helpFilePath ) )
                    helpFilePath = Path.Combine( Path.GetDirectoryName( assemblyPath ), AssemblyProcessor.GetHelpFileName( assemblyName ) );

                var docSources =
                    docSourceNames
                    .Select(
                        dsn =>
                        {
                            switch ( dsn )
                            {
                                case DocSourceNames.Descriptor:
#if PB_ENABLE_DESCRIPTORS
                                    return ( IDocSource ) new DescriptorDocSource( descriptors );
#else
                                    return null;
#endif
                                case DocSourceNames.Reflection:
                                    return ( IDocSource ) new ReflectionDocSource();
                                case DocSourceNames.XmlDoc:
                                    if ( File.Exists( xmlDocPath ) )
                                        return ( IDocSource ) new XmlDocSource( xmlDocPath, assembly.MainModule );
                                    else
                                    {
                                        Log.LogWarning( "PoshBuild",
                                            "PB01",
                                            "",
                                            null,
                                            0, 0, 0, 0,
                                            "The compiler-generated documentation file '{0}' was not found, XmlDoc will not be used as a documentation source for assembly '{1}'.",
                                            xmlDocPath,
                                            assemblyName );

                                        return null;
                                    }
                                default:
                                    throw new NotImplementedException();
                            }
                        }
                    )
                    .Where( ds => ds != null )
                    .ToList();

                if ( Log.HasLoggedErrors )
                    return false;

                XmlWriterSettings writerSettings = new XmlWriterSettings();
                writerSettings.Indent = true;

                using ( XmlWriter writer = XmlWriter.Create( helpFilePath, writerSettings ) )
                    new AssemblyProcessor( writer, assembly, new FallthroughDocSource( docSources ) ).GenerateHelpFile();

                GeneratedFiles = new ITaskItem[] { new TaskItem( helpFilePath ) };

                return !Log.HasLoggedErrors;
            }
        }
    }
}
