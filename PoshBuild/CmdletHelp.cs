using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Xml;
using PoshBuild.ComponentModel;

namespace PoshBuild
{
    public static class CmdletHelp
    {
        #region Constants
        public const string MshNs = "http://msh";
        public const string MamlNs = "http://schemas.microsoft.com/maml/2004/10";
        public const string CommandNs = "http://schemas.microsoft.com/maml/dev/command/2004/10";
        public const string DevNs = "http://schemas.microsoft.com/maml/dev/2004/10";
        #endregion

        #region Public Convenience Methods
        public static string GetHelpFileName(string snapInAssemblyName)
        {
            return string.Format("{0}.dll-Help.xml", snapInAssemblyName);
        }

        public static IEnumerable<ICmdletHelpDescriptor> GetDescriptors(Assembly descriptorsAssembly)
        {
            var descriptors = new List<ICmdletHelpDescriptor>();
            if (descriptorsAssembly != null)
            {
                var types = descriptorsAssembly.GetExportedTypes();
                var descriptorTypes = new List<Type>();
                foreach (var type in types)
                {
                    if (typeof(ICmdletHelpDescriptor).IsAssignableFrom(type) &&
                        Attribute.IsDefined(type, typeof(CmdletHelpDescriptorAttribute)))
                    {
                        descriptorTypes.Add(type);
                    }
                }

                foreach (var descriptorType in descriptorTypes)
                {
                    var descriptor = (ICmdletHelpDescriptor)Activator.CreateInstance(descriptorType);
                    descriptors.Add(descriptor);
                }
            }
            return descriptors;
        } 
        #endregion

        #region Public Help Generation Methods
        public static void GenerateHelpFile(Assembly snapInAssembly, XmlWriter writer)
        {
            GenerateHelpFile(snapInAssembly, writer, null);
        }

        public static void GenerateHelpFile(Assembly snapInAssembly, XmlWriter writer, Assembly descriptorsAssembly)
        {
            if (snapInAssembly == null)
                throw new ArgumentNullException("snapInAssembly");

            var descriptors = (descriptorsAssembly != null) ? GetDescriptors(descriptorsAssembly) : null;
            var types = snapInAssembly.GetExportedTypes();

            GenerateHelpFile(types, writer, descriptors);
        }

        public static void GenerateHelpFile(IEnumerable<Type> types, XmlWriter writer, IEnumerable<ICmdletHelpDescriptor> descriptors)
        {
            if (types == null)
                throw new ArgumentNullException("types");
            if (writer == null)
                throw new ArgumentNullException("writer");

            var sortedDescriptors = new Dictionary<Type, ICmdletHelpDescriptor>();
            if (descriptors != null)
            {
                foreach (var descriptor in descriptors)
                {
                    if (descriptor == null)
                        throw new ArgumentException("Found a null descriptor in the given descriptor collection.");
                    var descriptorType = descriptor.GetType();
                    var attribute = (CmdletHelpDescriptorAttribute)Attribute.GetCustomAttribute(descriptorType, typeof(CmdletHelpDescriptorAttribute));
                    if (attribute == null)
                        throw new ArgumentException("All descriptors must have the CmdletHelpDescriptorAttribute.");

                    sortedDescriptors.Add(attribute.DescribedType, descriptor);
                }
            }

            GenerateHelpFileStart(writer);

            foreach (Type type in types)
            {
                if (type.IsSubclassOf(typeof(Cmdlet)) &&
                    Attribute.IsDefined(type, typeof(CmdletAttribute)))
                {
                    ICmdletHelpDescriptor descriptor = null;
                    sortedDescriptors.TryGetValue(type, out descriptor);
                    GenerateHelpFileEntry(type, writer, descriptor);
                }
            }

            GenerateHelpFileEnd(writer);
        }

        private static void GenerateHelpFileStart(XmlWriter writer)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("helpItems", MshNs);

            writer.WriteAttributeString("xmlns", "maml", null, MamlNs);
            writer.WriteAttributeString("xmlns", "command", null, CommandNs);
            writer.WriteAttributeString("xmlns", "dev", null, DevNs);
            writer.WriteAttributeString("schema", "maml");
        }

        private static void GenerateHelpFileEnd(XmlWriter writer)
        {
            writer.WriteEndElement();
        }

        public static void GenerateHelpFileEntry(Type cmdletType, XmlWriter writer, ICmdletHelpDescriptor descriptor)
        {
            if (cmdletType == null)
                throw new ArgumentNullException("cmdletType");
            if (writer == null)
                throw new ArgumentNullException("writer");

            if (!cmdletType.IsSubclassOf(typeof(Cmdlet)))
                throw new ArgumentException("The given Cmdlet type does not inherit from Cmdlet.");
            CmdletAttribute cmdletAttribute = (CmdletAttribute)Attribute.GetCustomAttribute(cmdletType, typeof(CmdletAttribute));
            if (cmdletAttribute == null)
                throw new ArgumentException("The given Cmdlet type does not have the CmdletAttribute attribute.");

            CmdletParametersInfo parametersInfo = new CmdletParametersInfo(cmdletType, descriptor);

            writer.WriteStartElement("command", "command", null);
            {
                GenerateCommandDetails(cmdletType, cmdletAttribute, writer, descriptor);
                GenerateCommandDescription(cmdletType, cmdletAttribute, writer);
                GenerateCommandSyntax(cmdletType, cmdletAttribute, parametersInfo, writer);
                GenerateCommandParameters(cmdletType, cmdletAttribute, parametersInfo, writer);
                GenerateCommandReturnValues( cmdletType, cmdletAttribute, writer );
            }
            writer.WriteEndElement();
        } 
        #endregion

        #region Private Methods
        private static void GenerateCommandDetails(Type cmdletType, CmdletAttribute cmdletAttribute, XmlWriter writer, ICmdletHelpDescriptor descriptor)
        {
            writer.WriteStartElement("command", "details", null);
            {
                writer.WriteElementString("command", "name", null, string.Format("{0}-{1}", cmdletAttribute.VerbName, cmdletAttribute.NounName));
                writer.WriteElementString("command", "verb", null, cmdletAttribute.VerbName.ToLowerInvariant());
                writer.WriteElementString("command", "noun", null, cmdletAttribute.NounName.ToLowerInvariant());

                SynopsisAttribute synopsisAttribute = null;
                if (descriptor != null)
                    synopsisAttribute = descriptor.GetSynopsis();
                if (synopsisAttribute == null)
                    synopsisAttribute = (SynopsisAttribute)Attribute.GetCustomAttribute(cmdletType, typeof(SynopsisAttribute));
                if (synopsisAttribute != null)
                {
                    writer.WriteStartElement("maml", "description", null);
                    {
                        writer.WriteElementString("maml", "para", null, synopsisAttribute.Synopsis);
                    }
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }


        private static void GenerateCommandReturnValues(Type cmdletType, CmdletAttribute cmdletAttribute, XmlWriter writer)
        {
            var attributes = cmdletType.GetCustomAttributes( typeof( OutputTypeAttribute ), true ).OfType<OutputTypeAttribute>().ToList();

            if ( attributes.Count > 0 )
            {
                writer.WriteStartElement( "command", "returnValues", null );
                
                foreach ( var attr in attributes )
                {
                    var parameterSetNames = attr.ParameterSetName == null ? null : attr.ParameterSetName.Where( s => !string.IsNullOrWhiteSpace( s ) && s != ParameterAttribute.AllParameterSets ).ToList();
                    
                    foreach ( var type in attr.Type )
                    {
                        writer.WriteStartElement( "command", "returnValue", null );

                        writer.WriteStartElement( "dev", "type", null );
                        
                        writer.WriteElementString( "maml", "name", null, type.Name );
                        writer.WriteElementString( "maml", "uri", null, string.Empty );
                        // This description element is *not* read by Get-Help.
                        writer.WriteElementString( "maml", "description", null, string.Empty );
                        
                        writer.WriteEndElement(); // </dev:type>

                        // This description element *is* read by Get-Help.
                        writer.WriteStartElement( "maml", "description", null );

                        if ( !string.IsNullOrWhiteSpace( attr.ProviderCmdlet ) )
                            writer.WriteElementString( "maml", "para", null, string.Format( "Applies to Provider Cmdlet '{0}'.", attr.ProviderCmdlet ) );

                        if ( parameterSetNames != null && parameterSetNames.Count > 0 )
                        {
                            writer.WriteElementString( "maml", "para", null, "Applies to parameter sets:" );

                            foreach ( var name in parameterSetNames )
                                writer.WriteElementString( "maml", "para", null, "-- " + name );
                        }

                        writer.WriteEndElement(); // </maml:description>

                        writer.WriteEndElement(); // </command:returnValue>
                    }

                }

                writer.WriteEndElement(); // </command:returnValues>
            }
        }

        private static void GenerateCommandDescription(Type cmdletType, CmdletAttribute cmdletAttribute, XmlWriter writer)
        {
            DescriptionAttribute descriptionAttribute = (DescriptionAttribute)Attribute.GetCustomAttribute(cmdletType, typeof(DescriptionAttribute));
            if (descriptionAttribute != null)
            {
                writer.WriteStartElement("maml", "description", null);
                {
                    writer.WriteElementString("maml", "para", null, descriptionAttribute.Description);
                }
                writer.WriteEndElement();
            }
        }

        private static void GenerateCommandSyntax(Type cmdletType, CmdletAttribute cmdletAttribute, CmdletParametersInfo parametersInfo, XmlWriter writer)
        {
            writer.WriteStartElement("command", "syntax", null);
            {
                foreach (string parameterSetName in parametersInfo.ParametersByParameterSet.Keys)
                {
                    writer.WriteStartElement("command", "syntaxItem", null);
                    {
                        writer.WriteElementString("maml", "name", null, string.Format("{0}-{1}", cmdletAttribute.VerbName, cmdletAttribute.NounName));

                        foreach (CmdletParameterInfo parameterInfo in parametersInfo.ParametersByParameterSet[parameterSetName])
                        {
                            GenerateCommandSyntaxParameter(writer, parameterSetName, parameterInfo);
                        }
                    }
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }

        private static void GenerateCommandSyntaxParameter(XmlWriter writer, string parameterSetName, CmdletParameterInfo parameterInfo)
        {
            int parameterSetIndex = parameterInfo.GetParameterSetIndex(parameterSetName);

            writer.WriteStartElement("command", "parameter", null);
            {
                writer.WriteAttributeString("required", parameterInfo.Mandatory(parameterSetIndex).ToString());
                writer.WriteAttributeString("globbing", parameterInfo.Globbing.ToString());

                string pipelineInput = GetPipelineInputAttributeString(parameterInfo, parameterSetIndex);
                writer.WriteAttributeString("pipelineInput", pipelineInput);

                string position = GetPositionAttributeString(parameterInfo, parameterSetIndex);
                writer.WriteAttributeString("position", position);

                writer.WriteElementString("maml", "name", null, parameterInfo.ParameterName);

                writer.WriteStartElement("maml", "description", null);
                {
                    writer.WriteElementString("maml", "para", null, parameterInfo.ParameterAttributes[parameterSetIndex].HelpMessage);
                }
                writer.WriteEndElement();

                writer.WriteStartElement("command", "parameterValue", null);
                {
                    writer.WriteAttributeString("required", (parameterInfo.ParameterType == typeof(SwitchParameter)).ToString());
                    writer.WriteAttributeString("variableLength", (typeof(IEnumerable).IsAssignableFrom(parameterInfo.ParameterType)).ToString());
                    writer.WriteString(parameterInfo.ParameterType.ToString());
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void GenerateCommandParameters(Type cmdletType, CmdletAttribute cmdletAttribute, CmdletParametersInfo parametersInfo, XmlWriter writer)
        {
            writer.WriteStartElement("command", "parameters", null);
            {
                foreach (CmdletParameterInfo parameterInfo in parametersInfo.Parameters)
                {
                    GenerateCommandParameter(cmdletType, cmdletAttribute, parameterInfo, writer);
                }
            }
            writer.WriteEndElement();
        }

        private static void GenerateCommandParameter(Type cmdletType, CmdletAttribute cmdletAttribute, CmdletParameterInfo parameterInfo, XmlWriter writer)
        {
            writer.WriteStartElement("command", "parameter", null);
            {
                writer.WriteAttributeString("required", parameterInfo.Mandatory(0).ToString());
                writer.WriteAttributeString("variableLength", (typeof(IEnumerable).IsAssignableFrom(parameterInfo.ParameterType)).ToString());
                writer.WriteAttributeString("globbing", parameterInfo.Globbing.ToString());

                string pipelineInput = GetPipelineInputAttributeString(parameterInfo, 0);
                writer.WriteAttributeString("pipelineInput", pipelineInput);

                string position = GetPositionAttributeString(parameterInfo, 0);
                writer.WriteAttributeString("position", position);

                writer.WriteElementString("maml", "name", null, parameterInfo.ParameterName);

                writer.WriteStartElement("maml", "description", null);
                {
                    writer.WriteElementString("maml", "para", null, parameterInfo.ParameterAttributes[0].HelpMessage);
                }
                writer.WriteEndElement();

                writer.WriteStartElement("command", "parameterValue", null);
                {
                    writer.WriteAttributeString("required", (parameterInfo.ParameterType == typeof(SwitchParameter)).ToString());
                    writer.WriteAttributeString("variableLength", (typeof(IEnumerable).IsAssignableFrom(parameterInfo.ParameterType)).ToString());

                    writer.WriteString(parameterInfo.ParameterType.ToString());
                }
                writer.WriteEndElement();

                writer.WriteStartElement("dev", "type", null);
                {
                    writer.WriteElementString("maml", "name", null, parameterInfo.ParameterType.ToString());
                }
                writer.WriteEndElement();

                object defaultValue = parameterInfo.DefaultValue;
                writer.WriteElementString("dev", "defaultValue", null, (defaultValue == null) ? "Null" : defaultValue.ToString());
            }
            writer.WriteEndElement();
        }

        private static string GetPositionAttributeString(CmdletParameterInfo parameterInfo, int parameterSetIndex)
        {
            string position = "named";
            if (parameterInfo.Position(parameterSetIndex) >= 0)
                position = parameterInfo.Position(parameterSetIndex).ToString();
            return position;
        }

        private static string GetPipelineInputAttributeString(CmdletParameterInfo parameterInfo, int parameterSetIndex)
        {
            string pipelineInput = "false";
            if (parameterInfo.ValueFromPipeline(parameterSetIndex))
            {
                pipelineInput = "true (ByValue)";
                if (parameterInfo.ValueFromPipelineByPropertyName(parameterSetIndex))
                {
                    pipelineInput = "true (ByValue, ByPropertyName)";
                }
            }
            else if (parameterInfo.ValueFromPipelineByPropertyName(parameterSetIndex))
            {
                pipelineInput = "true (ByPropertyName)";
            }
            return pipelineInput;
        } 
        #endregion
    }
}
