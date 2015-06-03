using System.Collections.Generic;
using System.Linq;

namespace PoshBuild
{
    static class FormattingExtensions
    {
        public static string ToStringLower( this bool value )
        {
            return value ? "true" : "false";
        }

        public static string JoinWithAnd( this IEnumerable<string> list, string separator = ", ", string lastSeparator = " and " )
        {
            return list.Count() > 1 ? string.Join( separator, list.Take( list.Count() - 1 ) ) + lastSeparator + list.Last() : list.FirstOrDefault();
        }
    }
}
