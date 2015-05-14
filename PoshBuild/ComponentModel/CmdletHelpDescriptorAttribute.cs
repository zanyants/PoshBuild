using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PoshBuild.ComponentModel
{
    /// <summary>
    /// Attribute to be used on an <see cref="ICmdletHelpDescriptor"/>, specifying
    /// the type of the Cmdlet that the descriptor describes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class CmdletHelpDescriptorAttribute : Attribute
    {
        public Type DescribedType { get; private set; }

        public CmdletHelpDescriptorAttribute(Type describedType)
        {
            DescribedType = describedType;
        }
    }
}
