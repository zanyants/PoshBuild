using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;

namespace PoshBuild
{
    /// <summary>
    /// Helper methods related to type naming.
    /// </summary>
    static class TypeNameHelper
    {
        static readonly Dictionary<Type, string> _map;

        static TypeNameHelper()
        {
            _map = new Dictionary<Type, string>()
            {
                { typeof( bool ), "bool" },
                { typeof( byte ), "byte" },
                { typeof( sbyte ), "sbyte" },
                { typeof( char ), "char" },
                { typeof( int ), "int" },
                { typeof( decimal ), "decimal" },
                { typeof( double ), "double" },
                { typeof( float ), "single" },
                { typeof( void ), "void" },
                { typeof( string ), "string" },
                { typeof( Hashtable ), "hashtable" },
                { typeof( Int16 ), "int16" },
                { typeof( Int64 ), "long" },
                { typeof( UInt16 ), "uint16" },
                { typeof( UInt32 ), "uint32" },
                { typeof( UInt64 ), "uint64" },
                { typeof( Regex ), "Regex" },
                { typeof( SwitchParameter ), "SwitchParameter" },
                { typeof( System.Management.Automation.Language.NullString ), "NullString" },
                { typeof( ScriptBlock ), "ScriptBlock" },
            };            
        }

        /// <summary>
        /// Returns a pretty PowerShell-style name for the current type.
        /// </summary>
        public static string GetPSPrettyName( this Type type )
        {
            if ( type == null )
                throw new ArgumentNullException( "type" );

            if ( type.HasElementType )
            {
                var sb = new StringBuilder( GetPSPrettyName( type.GetElementType() ) );

                if ( type.IsArray )
                {
                    var count = type.GetArrayRank() - 1 ;
                    if ( count > 0 )
                    {
                        sb.Append( '[' );
                        sb.Append( ',', count );
                        sb.Append( ']' );
                    }
                    else
                        sb.Append( "[]" );
                }

                if ( type.IsByRef )
                    sb.Append( '&' );

                if ( type.IsPointer )
                    sb.Append( '*' );

                return sb.ToString();
            }
            else if ( type.IsGenericType && !type.ContainsGenericParameters )
            {
                // We only attempt to beautify closed generic types. It's unusual to encouter open generic types in PS cmdlets.
                
                var simpleName = type.Name.Substring(0, type.Name.IndexOf( '`') );

                var sb = new StringBuilder();

                // Skip only system namespace.
                if ( type.Namespace != "System" )
                {
                    sb.Append( type.Namespace );
                    sb.Append( '.' );
                }

                sb.Append( simpleName );

                sb.Append( '[' );
                var genericArgs = type.GetGenericArguments();

                for ( int i = 0; i < genericArgs.Length; ++i )
                {
                    sb.Append( GetPSPrettyName( genericArgs[i] ) );
                    if ( i + 1 < genericArgs.Length )
                        sb.Append( ',' );
                }
                sb.Append( ']' );

                return sb.ToString();
            }
            else
            {
                string name;

                if ( _map.TryGetValue( type, out name ) )
                    return name;

                name = type.Name;

                if ( type.IsSubclassOf( typeof( Attribute ) ) && name.EndsWith( "Attribute" ) && name.Length > 9 )
                    name = name.Substring( 0, name.Length - 9 );

                // System::* => type.Name
                if ( type.Namespace == "System" )
                    return name;

                // System.Management.Automation::PS* => type.Name
                if ( type.Namespace == "System.Management.Automation" && type.Name.StartsWith( "PS" ) )
                    return name;

                return type.FullName;
            }
        }
    }
}
