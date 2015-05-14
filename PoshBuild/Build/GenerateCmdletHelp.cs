using System.IO;
using System.Reflection;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System;
using PoshBuild.ComponentModel;

namespace PoshBuild.Build
{
    /// <summary>
    /// An MSBuild task that generates the Cmdlet help file for Cmdlets
    /// contained in a set of assemblies.
    /// </summary>
    public class GenerateCmdletHelp : AppDomainIsolatedTask
    {
        /// <summary>
        /// The assemblies to reflect Cmdlets from.
        /// </summary>
        [Required]
        public ITaskItem[] Assemblies { get; set; }

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
            var sortedTypes = new Dictionary<string, List<Type>>();
            foreach (var assemblyItem in Assemblies)
            {
                string assemblyPath = assemblyItem.GetMetadata("FullPath");
                string assemblyName = assemblyItem.GetMetadata("Filename");

                Assembly assembly = Assembly.LoadFrom(assemblyPath);
                var types = new List<Type>(assembly.GetExportedTypes());

                string helpFile = Path.Combine(Path.GetDirectoryName(assemblyPath), CmdletHelp.GetHelpFileName(assemblyName));
                sortedTypes.Add(helpFile, types);
            }

            var descriptors = new List<ICmdletHelpDescriptor>();
            if (DescriptorAssemblies != null)
            {
                foreach (var assemblyItem in DescriptorAssemblies)
                {
                    Assembly assembly = Assembly.LoadFrom(assemblyItem.GetMetadata("FullPath"));
                    var d = CmdletHelp.GetDescriptors(assembly);
                    descriptors.AddRange(d);
                }
            }

            HelpFiles = new TaskItem[sortedTypes.Count];

            int helpFileIndex = 0;
            foreach (string helpFile in sortedTypes.Keys)
            {
                XmlWriterSettings writerSettings = new XmlWriterSettings();
                writerSettings.Indent = true;
                using (XmlWriter writer = XmlWriter.Create(helpFile, writerSettings))
                {
                    CmdletHelp.GenerateHelpFile(sortedTypes[helpFile], writer, descriptors);
                }

                HelpFiles[helpFileIndex++] = new TaskItem(helpFile);
            }

            return true;
        }
    }
}
