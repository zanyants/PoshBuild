using System;

namespace PoshBuild.ComponentModel
{
    /// <summary>
    /// An attribute that specifies a synopsis for a Cmdlet.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class SynopsisAttribute : Attribute
    {
        public string Synopsis { get; private set; }

        public SynopsisAttribute(string synopsis)
        {
            Synopsis = synopsis;
        }
    }
}
