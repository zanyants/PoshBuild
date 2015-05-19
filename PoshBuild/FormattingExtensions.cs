using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PoshBuild
{
    static class FormattingExtensions
    {
        public static string ToStringLower( this bool value )
        {
            return value ? "true" : "false";
        }
    }
}
