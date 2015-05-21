using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
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
        /// The assemblies to reflect Cmdlets from.
        /// </summary>
        /// <remarks>
        /// Custom item metadata:
        /// 
        ///     %(PoshBuildXmlDocFile)
        ///     The path to the compiler-generated XML documentation file. Defaults to %(FullPath) with the extension replaced with '.xml'.
        ///     
        ///     %(PoshBuildOutputFile)
        ///     The path of the help file to generate. Defaults to %(FullPath)-Help.xml.
        ///     
        /// </remarks>
        [Required]
        public ITaskItem[] Assemblies { get; set; }

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
        /// The produced help files.
        /// </summary>
        [Output]
        public ITaskItem[] HelpFiles { get; set; }

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

                IDictionary<Type, ICmdletHelpDescriptor> descriptors = null;

                if ( DescriptorAssemblies != null && docSourceNames.Contains( DocSourceNames.Descriptor ) )
                {
                    var loadedAssemblies =
                            DescriptorAssemblies
                            .Select(
                                item =>
                                {
                                    try
                                    {
                                        return Assembly.LoadFrom( item.GetMetadata( "FullPath" ) );
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

                var assyInfo =
                    Assemblies
                    .Select(
                        item =>
                        {
                            var assemblyPath = item.GetMetadata( "FullPath" );
                            var assemblyName = item.GetMetadata( "Filename" );

                            Assembly assembly = null;

                            try
                            {
                                assembly = Assembly.LoadFrom( assemblyPath );
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

                                return null;
                            }
                            
                            var xmlDocPath = item.GetMetadata( "PoshBuildXmlDocFile" );
                            if ( string.IsNullOrEmpty( xmlDocPath ) )
                                xmlDocPath = Path.Combine( Path.GetDirectoryName( assemblyPath ), assemblyName + ".xml" );
                            
                            var helpFilePath = item.GetMetadata( "PoshBuildOutputFile" );
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
                                                return ( IDocSource ) new DescriptorDocSource( descriptors );
                                            case DocSourceNames.Reflection:
                                                return ( IDocSource ) new ReflectionDocSource();
                                            case DocSourceNames.XmlDoc:
                                                if ( File.Exists( xmlDocPath ) )
                                                    return ( IDocSource ) new XmlDocSource( xmlDocPath );
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

                            return new
                            {
                                AssemblyPath = assemblyPath,
                                AssemblyName = assemblyName,
                                XmlDocPath = xmlDocPath,
                                HelpFilePath = helpFilePath,
                                Assembly = assembly,
                                Types = assembly.GetExportedTypes(),
                                DocSource = new FallthroughDocSource( docSources )
                            };
                        } );

                if ( Log.HasLoggedErrors )
                    return false;

                var helpFiles = new List<ITaskItem>();
                XmlWriterSettings writerSettings = new XmlWriterSettings();
                writerSettings.Indent = true;

                foreach ( var item in assyInfo )
                {
                    if ( Log.HasLoggedErrors )
                        return false;

                    using ( XmlWriter writer = XmlWriter.Create( item.HelpFilePath, writerSettings ) )
                        new AssemblyProcessor( writer, item.Types, item.DocSource ).GenerateHelpFile();

                    helpFiles.Add( new TaskItem( item.HelpFilePath ) );
                }

                HelpFiles = helpFiles.ToArray();

                return !Log.HasLoggedErrors;
            }
        }
    }
}
