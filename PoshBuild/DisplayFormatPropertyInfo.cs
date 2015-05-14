using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using PoshBuild.ComponentModel;

namespace PoshBuild
{
    internal class DisplayFormatPropertyInfo
    {
        public PropertyInfo PropertyInfo { get; private set; }
        public DisplayFormatColumnAttribute ColumnAttribute { get; private set; }
        public bool HasColumnAttribute { get { return ColumnAttribute != null; } }

        public DisplayFormatPropertyInfo(PropertyInfo propertyInfo, IDisplayFormatDescriptor descriptor)
        {
            PropertyInfo = propertyInfo;
            if (descriptor != null)
                ColumnAttribute = descriptor.GetDisplayFormatColumnAttribute(propertyInfo.Name);
            if (ColumnAttribute == null)
                ColumnAttribute = (DisplayFormatColumnAttribute)Attribute.GetCustomAttribute(propertyInfo, typeof(DisplayFormatColumnAttribute));
        }
    }
}
