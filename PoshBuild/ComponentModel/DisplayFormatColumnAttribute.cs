using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PoshBuild.ComponentModel
{
    public enum DisplayFormatColumnAlignment
    {
        None = 0,
        Right,
        Centered,
        Left
    }

    /// <summary>
    /// An attribute that gives display format information for a given property of a type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class DisplayFormatColumnAttribute : Attribute
    {
        public int Index { get; private set; }
        public string Label { get; private set; }
        public int Width { get; set; }
        public DisplayFormatColumnAlignment Alignment { get; set; }
        public string ScriptBlock { get; set; }

        public DisplayFormatColumnAttribute(int columnIndex)
        {
            Index = columnIndex;
        }

        public DisplayFormatColumnAttribute(int columnIndex, string label)
            : this(columnIndex)
        {
            Label = label;
        }
    }
}
