using System;

namespace PoshBuild.ComponentModel
{
    /// <summary>
    /// An attribute that specifies whether the parameter of a Cmdlet supports wildcards.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class GlobbingAttribute : Attribute
    {
        public bool SupportsGlobbing { get; private set; }

        public GlobbingAttribute(bool supportsGlobbing)
        {
            SupportsGlobbing = supportsGlobbing;
        }
    }
}
