using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Reflection;
using System.Xml;
using PoshBuild.ComponentModel;

namespace PoshBuild.Build
{
    /// <summary>
    /// An MSBuild task that generates the display format file that
    /// describes, for Powershell, the public types of a given set
    /// of assemblies.
    /// </summary>
    public class GenerateDisplayFormat : AppDomainIsolatedTask
    {
        /// <summary>
        /// The assemblies to reflect types from.
        /// </summary>
        [Required]
        public ITaskItem[] Assemblies { get; set; }

        /// <summary>
        /// The assemblies that contain <see cref="IDisplayFormatDescriptor"/>s
        /// giving additional information on types. This can be the same assemblies
        /// as the input assemblies.
        /// </summary>
        public ITaskItem[] DescriptorAssemblies { get; set; }

        /// <summary>
        /// The path of the format file to generate.
        /// </summary>
        [Required]
        [Output]
        public ITaskItem FormatFilePath { get; set; }

        /// <summary>
        /// Generates the format file.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            var types = new List<Type>();
            foreach (var assemblyItem in Assemblies)
            {
                Assembly assembly = Assembly.LoadFrom(assemblyItem.GetMetadata("FullPath"));
                types.AddRange(assembly.GetExportedTypes());
            }

            var descriptors = new List<IDisplayFormatDescriptor>();
            if (DescriptorAssemblies != null)
            {
                foreach (var assemblyItem in DescriptorAssemblies)
                {
                    Assembly assembly = Assembly.LoadFrom(assemblyItem.GetMetadata("FullPath"));
                    var d = DisplayFormat.GetDescriptors(assembly);
                    descriptors.AddRange(d);
                }
            }

            string formatFile = FormatFilePath.GetMetadata("FullPath");

            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.Indent = true;
            using (XmlWriter writer = XmlWriter.Create(formatFile, writerSettings))
            {
                DisplayFormat.GenerateDisplayFormatFile(types, writer, descriptors);
            }

            return true;
        }
    }
}
