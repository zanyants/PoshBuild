using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PoshBuild.ComponentModel
{
    /// <summary>
    /// An attribute that gives display format information for a given type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class DisplayFormatAttribute : Attribute
    {
        public string Name { get; private set; }

        public string GroupByLabel { get; set; }
        public string GroupByCustomControlName { get; set; }
        public string GroupByPropertyName { get; set; }
        public string GroupByScriptBlock { get; set; }

        internal bool HasGroupByProperties
        {
            get
            {
                return GroupByLabel != null ||
                       GroupByCustomControlName != null ||
                       GroupByPropertyName != null ||
                       GroupByScriptBlock != null;
            }
        }

        public DisplayFormatAttribute()
        {
        }

        public DisplayFormatAttribute(string name)
        {
            Name = name;
        }
    }
}
