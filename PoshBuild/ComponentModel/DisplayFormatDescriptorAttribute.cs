using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PoshBuild.ComponentModel
{
    /// <summary>
    /// Attribute to be used on an <see cref="IDisplayFormatDescriptor"/>, specifying
    /// the type that the descriptor describes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DisplayFormatDescriptorAttribute : Attribute
    {
        public Type DescribedType { get; private set; }

        public DisplayFormatDescriptorAttribute(Type describedType)
        {
            DescribedType = describedType;
        }
    }
}
