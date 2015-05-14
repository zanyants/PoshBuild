using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PoshBuild.ComponentModel;

namespace PoshBuild
{
    internal class DisplayFormatPropertiesInfo
    {
        public List<DisplayFormatPropertyInfo> Properties { get; private set; }

        public DisplayFormatPropertiesInfo(Type type, IDisplayFormatDescriptor descriptor)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            var buffer = new List<DisplayFormatPropertyInfo>();
            foreach (var property in type.GetProperties())
            {
                buffer.Add(new DisplayFormatPropertyInfo(property, descriptor));
            }

            var orderedBuffer = buffer.OrderBy(dfpi =>
            {
                if (dfpi.HasColumnAttribute)
                    return dfpi.ColumnAttribute.Index;
                else
                    return -1;
            });

            Properties = new List<DisplayFormatPropertyInfo>();
            Properties.AddRange(orderedBuffer);
        }
    }
}
