using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Xml;
using PoshBuild.ComponentModel;
using System.ComponentModel;

namespace PoshBuild
{
    public static class DisplayFormat
    {
        #region Public Convenience Methods
        public static IEnumerable<IDisplayFormatDescriptor> GetDescriptors(Assembly descriptorsAssembly)
        {
            var descriptors = new List<IDisplayFormatDescriptor>();
            if (descriptorsAssembly != null)
            {
                var types = descriptorsAssembly.GetExportedTypes();
                var descriptorTypes = new List<Type>();
                foreach (var type in types)
                {
                    if (typeof(IDisplayFormatDescriptor).IsAssignableFrom(type) &&
                        Attribute.IsDefined(type, typeof(DisplayFormatDescriptorAttribute)))
                    {
                        descriptorTypes.Add(type);
                    }
                }

                foreach (var descriptorType in descriptorTypes)
                {
                    var descriptor = (IDisplayFormatDescriptor)Activator.CreateInstance(descriptorType);
                    descriptors.Add(descriptor);
                }
            }
            return descriptors;
        } 
        #endregion

        #region Public Display Format Generation Methods
        public static void GenerateDisplayFormatFile(Assembly assembly, XmlWriter writer)
        {
            GenerateDisplayFormatFile(assembly, writer, null);
        }

        public static void GenerateDisplayFormatFile(Assembly assembly, XmlWriter writer, Assembly descriptorsAssembly)
        {
            var descriptors = GetDescriptors(descriptorsAssembly);
            GenerateDisplayFormatFile(assembly.GetExportedTypes(), writer, descriptors);
        }

        public static void GenerateDisplayFormatFile(IEnumerable<Type> types, XmlWriter writer)
        {
            GenerateDisplayFormatFile(types, writer, new IDisplayFormatDescriptor[] { });
        }

        public static void GenerateDisplayFormatFile(IEnumerable<Type> types, XmlWriter writer, IEnumerable<IDisplayFormatDescriptor> descriptors)
        {
            var sortedDescriptors = new Dictionary<Type, IDisplayFormatDescriptor>();
            foreach (var descriptor in descriptors)
            {
                if (descriptor == null)
                    throw new ArgumentException("Found a null descriptor in the given descriptor collection.");
                var descriptorType = descriptor.GetType();
                var attribute = (DisplayFormatDescriptorAttribute)Attribute.GetCustomAttribute(descriptorType, typeof(DisplayFormatDescriptorAttribute));
                if (attribute == null)
                    throw new ArgumentException("All descriptors must have the DisplayFormatDescriptorAttribute.");

                sortedDescriptors.Add(attribute.DescribedType, descriptor);
            }

            writer.WriteStartDocument();
            writer.WriteStartElement("Configuration");
            GenerateViewDefinitions(types, writer, sortedDescriptors);
            writer.WriteEndElement();
        } 
        #endregion

        #region Private Methods
        private static void GenerateViewDefinitions(IEnumerable<Type> types, XmlWriter writer, IDictionary<Type, IDisplayFormatDescriptor> descriptors)
        {
            writer.WriteStartElement("ViewDefinitions");
            foreach (var type in types)
            {
                GenerateViewDefinition(type, writer, descriptors);
            }
            writer.WriteEndElement();
        }

        private static void GenerateViewDefinition(Type type, XmlWriter writer, IDictionary<Type, IDisplayFormatDescriptor> descriptors)
        {
            IDisplayFormatDescriptor descriptor = null;
            descriptors.TryGetValue(type, out descriptor);

            DisplayFormatAttribute attribute = null;
            if (descriptor != null)
                attribute = descriptor.GetDisplayFormatAttribute();
            if (attribute == null)
                attribute = (from object a in TypeDescriptor.GetAttributes(type)
                             where a is DisplayFormatAttribute
                             select a).FirstOrDefault() as DisplayFormatAttribute;
            if (attribute == null)
                return;

            DisplayFormatPropertiesInfo propertiesInfo = new DisplayFormatPropertiesInfo(type, descriptor);

            writer.WriteStartElement("View");
            {
                writer.WriteElementString("Name", attribute.Name ?? type.FullName);

                writer.WriteStartElement("ViewSelectedBy");
                {
                    writer.WriteElementString("TypeName", type.FullName);
                }
                writer.WriteEndElement();

                if (attribute.HasGroupByProperties)
                {
                    writer.WriteStartElement("GroupBy");
                    {
                        if (attribute.GroupByLabel != null)
                            writer.WriteElementString("Label", attribute.GroupByLabel);
                        if (attribute.GroupByPropertyName != null)
                            writer.WriteElementString("PropertyName", attribute.GroupByPropertyName);
                        if (attribute.GroupByScriptBlock != null)
                            writer.WriteElementString("ScriptBlock", attribute.GroupByScriptBlock);
                        if (attribute.GroupByCustomControlName != null)
                            writer.WriteElementString("CustomControlName", attribute.GroupByCustomControlName);
                    }
                    writer.WriteEndElement();
                }

                writer.WriteStartElement("TableControl");
                {
                    writer.WriteStartElement("TableHeaders");
                    {
                        foreach (var propertyInfo in propertiesInfo.Properties)
                        {
                            if (propertyInfo.HasColumnAttribute)
                            {
                                writer.WriteStartElement("TableColumnHeader");
                                {
                                    if (propertyInfo.ColumnAttribute.Label != null)
                                        writer.WriteElementString("Label", propertyInfo.ColumnAttribute.Label);
                                    if (propertyInfo.ColumnAttribute.Width > 0)
                                        writer.WriteElementString("Width", propertyInfo.ColumnAttribute.Width.ToString());
                                    if (propertyInfo.ColumnAttribute.Alignment != DisplayFormatColumnAlignment.None)
                                        writer.WriteElementString("Alignment", propertyInfo.ColumnAttribute.Alignment.ToString());
                                }
                                writer.WriteEndElement();
                            }
                        }
                    }
                    writer.WriteEndElement();

                    writer.WriteStartElement("TableRowEntries");
                    {
                        writer.WriteStartElement("TableRowEntry");
                        {
                            writer.WriteStartElement("TableColumnItems");
                            {
                                foreach (var propertyInfo in propertiesInfo.Properties)
                                {
                                    if (propertyInfo.HasColumnAttribute)
                                    {
                                        writer.WriteStartElement("TableColumnItem");
                                        {
                                            if (propertyInfo.ColumnAttribute.ScriptBlock != null)
                                            {
                                                writer.WriteElementString("ScriptBlock", propertyInfo.ColumnAttribute.ScriptBlock);
                                            }
                                            else
                                            {
                                                writer.WriteElementString("PropertyName", propertyInfo.PropertyInfo.Name);
                                            }
                                        }
                                        writer.WriteEndElement();
                                    }
                                }
                            }
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        } 
        #endregion
    }
}
