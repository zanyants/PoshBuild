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
        /// If <c>XmlDoc</c> source is being used, it is possible to specify a non-default XmlDoc file path using custom item metadata <c>%(XmlDocFilePath)</c>.
        /// </summary>
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
                descriptors = DescriptorDocSource.GetDescriptors(
                        DescriptorAssemblies
                        .Select( item => Assembly.LoadFrom( item.GetMetadata( "FullPath" ) ) )
                    );
            }

            var assyInfo = 
                Assemblies
                .Select(
                    item =>
                    {
                        var assemblyPath = item.GetMetadata( "FullPath" );
                        var assemblyName = item.GetMetadata( "Filename" );
                        var assembly = Assembly.LoadFrom( assemblyPath );
                        var xmlDocPath = item.GetMetadata( "XmlDocFilePath" );                        
                        if ( string.IsNullOrEmpty( xmlDocPath ) )
                            xmlDocPath = Path.Combine( Path.GetDirectoryName( assemblyPath ), assemblyName + ".xml" );

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
                                            return ( IDocSource ) new XmlDocSource( xmlDocPath );
                                        default:
                                            throw new NotImplementedException();
                                    }
                                }
                            )
                            .ToList();

                        return new
                        {
                            AssemblyPath = assemblyPath,
                            AssemblyName = assemblyName,
                            XmlDocPath = xmlDocPath,
                            HelpFilePath = Path.Combine( Path.GetDirectoryName( assemblyPath ), AssemblyProcessor.GetHelpFileName( assemblyName ) ),
                            Assembly = assembly,
                            Types = assembly.GetExportedTypes(),
                            DocSource = new FallthroughDocSource( docSources )
                        };
                    } );



            var helpFiles = new List<ITaskItem>();
            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.Indent = true;

            foreach ( var item in assyInfo )
            {
                using ( XmlWriter writer = XmlWriter.Create( item.HelpFilePath, writerSettings ) )
                    new AssemblyProcessor( writer, item.Types, item.DocSource ).GenerateHelpFile();

                helpFiles.Add( new TaskItem( item.HelpFilePath ) );
            }

            HelpFiles = helpFiles.ToArray();

            return true;
        }
    }
}
