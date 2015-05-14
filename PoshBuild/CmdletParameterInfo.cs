using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management.Automation;
using System.Reflection;
using PoshBuild.ComponentModel;

namespace PoshBuild
{
    internal class CmdletParameterInfo
    {
        public string ParameterName { get; private set; }
        public Type ParameterType { get; private set; }
        public IList<ParameterAttribute> ParameterAttributes { get; private set; }
        public GlobbingAttribute GlobbingAttribute { get; private set; }
        public DefaultValueAttribute DefaultValueAttribute { get; private set; }

        public int ParameterSetCount { get { return ParameterAttributes.Count; } }
        public bool Globbing { get { return GlobbingAttribute != null ? GlobbingAttribute.SupportsGlobbing : false; } }
        public object DefaultValue { get { return DefaultValueAttribute != null ? DefaultValueAttribute.Value : string.Empty; } }

        public CmdletParameterInfo(PropertyInfo propertyInfo, ICmdletHelpDescriptor descriptor)
        {
            ParameterName = propertyInfo.Name;
            ParameterType = propertyInfo.PropertyType;

            ParameterAttributes = new List<ParameterAttribute>();
            Attribute[] parameterAttributes = Attribute.GetCustomAttributes(propertyInfo, typeof(ParameterAttribute));
            foreach (var parameterAttribute in parameterAttributes)
            {
                ParameterAttributes.Add((ParameterAttribute)parameterAttribute);
            }
            if (ParameterAttributes.Count == 0)
                throw new ArgumentException("The given property doesn't have a ParameterAttribute attribute.");

            if (descriptor != null)
                GlobbingAttribute = descriptor.GetGlobbing(propertyInfo.Name);
            if (GlobbingAttribute == null)
                GlobbingAttribute = (GlobbingAttribute)Attribute.GetCustomAttribute(propertyInfo, typeof(GlobbingAttribute));

            DefaultValueAttribute = (DefaultValueAttribute)Attribute.GetCustomAttribute(propertyInfo, typeof(DefaultValueAttribute));
        }

        public int GetParameterSetIndex(string parameterSetName)
        {
            int index = 0;
            foreach (var parameterAttribute in ParameterAttributes)
            {
                if (parameterAttribute.ParameterSetName == parameterSetName)
                    return index;
                index++;
            }
            throw new KeyNotFoundException("The parameter set name was specified for this parameter.");
        }

        public bool Mandatory(int parameterSetIndex)
        {
            return ParameterAttributes[parameterSetIndex].Mandatory;
        }

        public int Position(int parameterSetIndex)
        {
            return ParameterAttributes[parameterSetIndex].Position;
        }

        public bool ValueFromPipeline(int parameterSetIndex)
        {
            return ParameterAttributes[parameterSetIndex].ValueFromPipeline;
        }

        public bool ValueFromPipelineByPropertyName(int parameterSetIndex)
        {
            return ParameterAttributes[parameterSetIndex].ValueFromPipelineByPropertyName;
        }

        public bool ValueFromRemaingArguments(int parameterSetIndex)
        {
            return ParameterAttributes[parameterSetIndex].ValueFromRemainingArguments;
        }

        public string[] GetParameterSetNames()
        {
            if (ParameterAttributes.Count == 1 &&
                string.IsNullOrEmpty(ParameterAttributes[0].ParameterSetName))
                return new string[1] { ParameterAttribute.AllParameterSets };

            List<string> parameterSets = new List<string>();
            foreach (var parameterAttribute in ParameterAttributes)
            {
                parameterSets.Add(parameterAttribute.ParameterSetName);
            }
            return parameterSets.ToArray();
        }
    }
}
