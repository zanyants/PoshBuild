using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace PoshBuild
{
    /// <summary>
    /// Helper methods related to type naming.
    /// </summary>
    static class TypeNameHelper
    {
        static readonly Dictionary<Type, string> _map;
        static readonly Dictionary<string, string> _mapByFullName;

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
                { typeof( IEnumerable ), "IEnumerable" }
            };

            _mapByFullName = _map.ToDictionary( kvp => kvp.Key.FullName, kvp => kvp.Value );
        }

        /// <summary>
        /// Returns a pretty PowerShell-style name for a specified full name of a type (eg, <c>System.String</c>).
        /// </summary>
        /// <param name="typeFullName">
        /// The full name of the type, without assembly qualification. Generic types must use the backtick form.
        /// Only plain type names are supported - generic type parameters and arguments, element notation (arrays)
        /// and type modifiers (pointer, out, ref, pinned) must not be specified. <see cref="IEnumerable`1"/> is
        /// a special case and may be specified without the <c>`1</c> suffix, in which case the string <c>IEnumerable</c>
        /// will be returned.</param>
        /// <returns>The pretty name of the type.</returns>
        public static string GetPSPrettyName( string typeFullName, ModuleDefinition contextModule )
        {
            if ( string.IsNullOrWhiteSpace( typeFullName ) )
                return typeFullName;

            if ( typeFullName.IndexOfAny( "[]{},".ToCharArray() ) != -1 )
                throw new ArgumentException( "Generic parameter and element notation is not permitted.", "typeFullName" );

            if ( typeFullName.IndexOfAny( "^*&@".ToCharArray() ) != -1 )
                throw new ArgumentException( "Type modifier notation is not permitted.", "typeFullName" );

            Type type = null;
            bool includeGenericParameters = true;

            if ( typeFullName == "System.Collections.Generic.IEnumerable`1" )
            {
                type = typeof( IEnumerable<> );
            }
            else if ( typeFullName == "System.Collections.Generic.IEnumerable" )
            {
                type = typeof( IEnumerable<> );
                includeGenericParameters = false;
            }
            else
            {
                // The only other types we prettify come from either mscorlib or System.Management.Automation, so try to
                // resolve from those locations.
                type =
                    Type.GetType( typeFullName ) ??
                    typeof( PSObject ).Assembly.GetType( typeFullName );
            }

            if ( type == null )
                return typeFullName;
            else
                return _GetPSPrettyName( contextModule.Import( type ), includeGenericParameters );
        }

        /// <summary>
        /// Returns a pretty PowerShell-style name for the current type.
        /// </summary>
        public static string GetPSPrettyName( this TypeReference type )
        {
            return _GetPSPrettyName( type, true );
        }

        /// <summary>
        /// Returns a pretty PowerShell-style name for the current type.
        /// </summary>
        static string _GetPSPrettyName( TypeReference typeRef, bool includeGenericParameters )
        {
            if ( typeRef == null )
                throw new ArgumentNullException( "typeRef" );

            var type = typeRef.Resolve();

            var sb = new StringBuilder();

            if ( typeRef.IsArray || typeRef.IsByReference || typeRef.IsPinned || typeRef.IsPointer )
            {
                sb.Append( _GetPSPrettyName( typeRef.GetElementType(), true ) );

                if ( typeRef.IsArray )
                {
                    var array = ( ArrayType ) typeRef;
                    var count = array.Rank - 1;
                    if ( count > 0 )
                    {
                        sb.Append( '[' );
                        sb.Append( ',', count );
                        sb.Append( ']' );
                    }
                    else
                        sb.Append( "[]" );
                }

                if ( typeRef.IsByReference )
                    sb.Append( '&' );

                if ( typeRef.IsPointer )
                    sb.Append( '*' );

                if ( typeRef.IsPinned )
                    sb.Append( '^' );
            }
            else
            {
                if ( typeRef.IsGenericInstance || typeRef.HasGenericParameters )
                {
                    var genericInstance = typeRef.IsGenericInstance ? ( GenericInstanceType ) typeRef : null;

                    var tIEnumerable1 = typeRef.Module.Import( typeof( IEnumerable<> ) );

                    var simpleName = type.Name.Substring( 0, type.Name.IndexOf( '`' ) );

                    // Skip namespace for IEnumerable<>, and any class in System namespace
                    if ( !type.IsSame( tIEnumerable1, CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ) && type.Namespace != "System" )
                    {
                        sb.Append( type.Namespace );
                        sb.Append( '.' );
                    }

                    sb.Append( simpleName );

                    if ( includeGenericParameters )
                    {
                        sb.Append( '[' );

                        if ( typeRef.IsGenericInstance )
                        {
                            sb.Append( string.Join( ",", genericInstance.GenericArguments.Select( ga => GetPSPrettyName( ga ) ) ) );
                        }
                        else
                        {
                            sb.Append( string.Join( ",", typeRef.GenericParameters.Select( gp => gp.Name ) ) );
                        }

                        sb.Append( ']' );
                    }
                }
                else
                {
                    string name;

                    if ( _mapByFullName.TryGetValue( typeRef.FullName, out name ) )
                        sb.Append( name );
                    else
                    {
                        name = type.Name;

                        if ( type.IsSubclassOf( type.Module.Import( typeof( Attribute ) ), CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ) && name.EndsWith( "Attribute" ) && name.Length > 9 )
                            name = name.Substring( 0, name.Length - 9 );

                        // System::* => type.Name
                        if ( type.Namespace == "System" )
                            sb.Append( name );
                        else
                        {
                            // System.Management.Automation::PS* => type.Name
                            if ( type.Namespace == "System.Management.Automation" && type.Name.StartsWith( "PS" ) )
                                sb.Append( name );
                            else
                                sb.Append( type.FullName );
                        }
                    }
                }
            }

            return sb.ToString();
        }
    }
}
