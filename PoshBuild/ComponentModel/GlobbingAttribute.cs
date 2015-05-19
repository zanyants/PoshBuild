using System;
using System.Management.Automation;

namespace PoshBuild.ComponentModel
{
    /// <summary>
    /// An attribute that specifies whether the parameter of a Cmdlet supports wildcards.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class GlobbingAttribute : Attribute
    {
        public bool SupportsGlobbing { get; private set; }

        public string ParameterSetName { get; set; }

        public GlobbingAttribute( bool supportsGlobbing )
        {
            ParameterSetName = ParameterAttribute.AllParameterSets;
            SupportsGlobbing = supportsGlobbing;
        }
    }
}
