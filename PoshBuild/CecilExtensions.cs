using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Mono.Cecil;

namespace PoshBuild
{
    /// <summary>
    /// Extension methods related to types in the <see cref="N:Mono.Cecil"/> namespace.
    /// </summary>
    static class CecilExtensions
    {
        /// <summary>
        /// Returns <c>true</c> if the current property appears to be a Cmdlet parameter.
        /// </summary>
        /// <remarks>
        /// Must have public get and set, and at least one [Parameter] attribute.
        /// </remarks>
        public static bool IsCmdletParameter( this PropertyDefinition property )
        {
            var tParameterAttribute = property.Module.Import( typeof( ParameterAttribute ) );

            return
                property.GetMethod != null && property.GetMethod.IsPublic &&
                property.SetMethod != null && property.SetMethod.IsPublic &&
                property
                .CustomAttributes
                .Any( ca => ca.AttributeType.IsSame( tParameterAttribute, CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ) );
        }

        /// <summary>
        /// Returns all the properties of the current type and its base types. Base types at or below <see cref="Cmdlet"/> or <see cref="PSCmdlet"/>
        /// are not considered.
        /// </summary>
        public static IEnumerable<PropertyDefinition> GetCmdletAndBaseProperties( this TypeDefinition cmdletType )
        {
            var tCmdlet = cmdletType.Module.Import( typeof( Cmdlet ) );
            var tPSCmdlet = cmdletType.Module.Import( typeof( PSCmdlet ) );

            return
                cmdletType
                .SelfAndBaseTypes()
                .TakeWhile( t => !t.IsSame( tCmdlet, TypeComparisonFlags.MatchAllExceptVersion ) && !t.IsSame( tPSCmdlet, TypeComparisonFlags.MatchAllExceptVersion ) )
                .SelectMany( t => t.Properties );
        }

        /// <summary>
        /// Returns all the [Parameter] properties of the current type and its base types. Base types at or below <see cref="Cmdlet"/> or <see cref="PSCmdlet"/>
        /// are not considered.
        /// </summary>
        public static IEnumerable<PropertyDefinition> GetCmdletAndBaseParameterProperties( this TypeDefinition cmdletType )
        {
            return cmdletType.GetCmdletAndBaseProperties().Where( p => p.IsCmdletParameter() );
        }

        /// <summary>
        /// Returns <c>true</c> if a specified type has a <see cref="CmdletAttribute"/> attribute and derives <see cref="Cmdlet"/>.
        /// </summary>
        public static bool IsCmdlet( this TypeDefinition type )
        {
            if ( type == null )
                throw new ArgumentNullException( "type" );

            var tCmdlet = type.Module.Import( typeof( Cmdlet ) );
            var tCmdletAttribute = type.Module.Import( typeof( CmdletAttribute ) );

            if ( tCmdlet == null || tCmdletAttribute == null )
                return false;

            return
                type
                    .CustomAttributes
                    .Any( ca => ca.AttributeType.IsSame( tCmdletAttribute, CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ) )
                    &&
                type.IsSubclassOf( tCmdlet, CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion );
        }

        /// <summary>
        /// Returns <c>true</c> if a specified type has a <see cref="CmdletAttribute"/> attribute and derives <see cref="Cmdlet"/>,
        /// and provides a real <see cref="CmdletAttribute"/> instance.
        /// </summary>
        public static bool IsCmdlet( this TypeDefinition type, out CmdletAttribute cmdletAttribute )
        {
            if ( type == null )
                throw new ArgumentNullException( "type" );

            cmdletAttribute = null;

            var tCmdlet = type.Module.Import( typeof( Cmdlet ) );
            var tCmdletAttribute = type.Module.Import( typeof( CmdletAttribute ) );

            if ( tCmdlet == null || tCmdletAttribute == null )
                return false;
            
            var attr =                 
                type
                .CustomAttributes
                .FirstOrDefault( ca => ca.AttributeType.IsSame( tCmdletAttribute, CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ) );
            if( attr != null && type.IsSubclassOf( tCmdlet, CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ) )
            {
                cmdletAttribute = attr.ConstructRealAttributeOfType<CmdletAttribute>();
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Returns an <see cref="AssemblyNameReference"/> for the scope of a specified <see cref="TypeReference"/>. This is the assembly
        /// in which the type referenced by the <see cref="TypeRef"/> is defined.
        /// </summary>
        public static AssemblyNameReference GetScopeAssemblyNameReference( this TypeReference type )
        {
            switch ( type.Scope.MetadataScopeType )
            {
                case MetadataScopeType.AssemblyNameReference:
                    return ( ( AssemblyNameReference ) type.Scope );
                case MetadataScopeType.ModuleDefinition:
                    return ( ( ModuleDefinition ) type.Scope ).Assembly.Name;
                case MetadataScopeType.ModuleReference:
                    // I'm assuming that a module reference must be within the same assembly as the TypeReference itself.
                    return type.Module.Assembly.Name;
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Flags used to control comparsion of types.
        /// </summary>
        [Flags]
        public enum TypeComparisonFlags
        {
            /// <summary>
            /// If set, all string-based comparisons are case insensitive; otherwise all string-based comparsions are case-sensitive.
            /// </summary>
            IgnoreCase = 1 << 0,

            /// <summary>
            /// Match the simple name of the type.
            /// </summary>
            MatchName = 1 << 1,

            /// <summary>
            /// Match the namespace of the type.
            /// </summary>
            MatchNamespace = 1 << 2,

            /// <summary>
            /// Match the name of the assembly in which the type is defined.
            /// </summary>
            MatchAssemblyName = 1 << 3,

            /// <summary>
            /// Match the public key token of the assembly in which the type is defined.
            /// </summary>
            MatchAssemblyPublicKeyToken = 1 << 4,

            /// <summary>
            /// Match the major (1st) part of the version of the assembly in which the type is defined.
            /// </summary>
            MatchAssemblyVersionMajor = 1 << 6,

            /// <summary>
            /// Match the minor (2nd) part of the version of the assembly in which the type is defined.
            /// </summary>
            MatchAssemblyVersionMinor = 1 << 7,

            /// <summary>
            /// Match the build (3rd) part of the version of the assembly in which the type is defined.
            /// </summary>
            MatchAssemblyVersionBuild = 1 << 8,

            /// <summary>
            /// Match the revision (4th) part of the version of the assembly in which the type is defined.
            /// </summary>
            MatchAssemblyVersionRevision = 1 << 9,

            /// <summary>
            /// Match the all parts of the version of the assembly in which the type is defined.
            /// </summary>
            MatchAssemblyVersion = MatchAssemblyVersionMajor | MatchAssemblyVersionMinor | MatchAssemblyVersionBuild | MatchAssemblyVersionRevision,

            /// <summary>
            /// Match all characteristics.
            /// </summary>
            MatchAll = MatchName | MatchNamespace | MatchAssemblyPublicKeyToken | MatchAssemblyVersion,

            /// <summary>
            /// Match all characteristics except the version of the assembly where the type is defined.
            /// </summary>
            MatchAllExceptVersion = MatchName | MatchNamespace | MatchAssemblyPublicKeyToken,
        }
        
        /// <summary>
        /// Compare two <see cref="TypeReference"/> objects for semantic equivalence.
        /// </summary>
        public static bool IsSame( this TypeReference type, TypeReference other, TypeComparisonFlags flags = TypeComparisonFlags.MatchAll )
        {
            if ( type == null )
                throw new ArgumentNullException( "type" );

            if ( other == null )
                throw new ArgumentNullException( "other" );

            var ignoreCase = flags.HasFlag( TypeComparisonFlags.IgnoreCase );
            var comp = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

            if ( flags.HasFlag( TypeComparisonFlags.MatchName ) && comp.Compare( type.Name, other.Name ) != 0 )
                return false;

            if ( flags.HasFlag( TypeComparisonFlags.MatchNamespace ) && comp.Compare( type.Namespace, other.Namespace ) != 0 )
                return false;

            var assy = type.GetScopeAssemblyNameReference();
            var otherAssy = other.GetScopeAssemblyNameReference();

            if ( flags.HasFlag( TypeComparisonFlags.MatchAssemblyName ) && comp.Compare( assy.Name, otherAssy.Name ) != 0 )
                return false;

            if ( flags.HasFlag( TypeComparisonFlags.MatchAssemblyPublicKeyToken ) )
            {
                if ( ( assy.PublicKeyToken == null ) != ( otherAssy.PublicKeyToken == null ) )
                    return false;

                if ( assy.PublicKeyToken != null && !assy.PublicKeyToken.SequenceEqual( otherAssy.PublicKeyToken ) )
                    return false;
            }

            if ( flags.HasFlag( TypeComparisonFlags.MatchAssemblyVersionMajor ) && assy.Version.Major != otherAssy.Version.Major )
                return false;

            if ( flags.HasFlag( TypeComparisonFlags.MatchAssemblyVersionMinor ) && assy.Version.Minor != otherAssy.Version.Minor )
                return false;

            if ( flags.HasFlag( TypeComparisonFlags.MatchAssemblyVersionBuild ) && assy.Version.Build != otherAssy.Version.Build )
                return false;

            if ( flags.HasFlag( TypeComparisonFlags.MatchAssemblyVersionRevision ) && assy.Version.Revision != otherAssy.Version.Revision )
                return false;

            return true;
        }

        /// <summary>
        /// Enumerates the base types of the current type.
        /// </summary>
        public static IEnumerable<TypeDefinition> BaseTypes( this TypeDefinition type )
        {
            if ( type == null )
                throw new ArgumentNullException( "type" );

            var refBase = type.BaseType;

            while ( refBase != null )
            {
                var tdBase = refBase.Resolve();

                if ( tdBase != null )
                {
                    refBase = tdBase.BaseType;
                    yield return tdBase;
                }
                else
                {
                    // Could not resolve base type
                    if ( TaskContext.Current != null )
                        TaskContext.Current.Log.LogWarning(
                            "PoshBuild",
                            "CE01A",
                            "",
                            null, 0, 0, 0, 0,
                            "Failed to resolve base type reference {0}, {1}",
                            refBase.FullName,
                            refBase.GetScopeAssemblyNameReference().FullName );
                    break;
                }
            }
        }

        /// <summary>
        /// Enumerates the current type and its base types.
        /// </summary>
        public static IEnumerable<TypeDefinition> SelfAndBaseTypes( this TypeDefinition type )
        {
            if ( type == null )
                throw new ArgumentNullException( "type" );

            yield return type;

            var refBase = type.BaseType;

            while ( refBase != null )
            {
                var tdBase = refBase.Resolve();

                if ( tdBase != null )
                {
                    refBase = tdBase.BaseType;
                    yield return tdBase;
                }
                else
                {
                    // Could not resolve base type
                    if ( TaskContext.Current != null )
                        TaskContext.Current.Log.LogWarning(
                            "PoshBuild",
                            "CE01B",
                            "",
                            null, 0, 0, 0, 0,
                            "Failed to resolve base type reference {0}, {1}",
                            refBase.FullName,
                            refBase.GetScopeAssemblyNameReference().FullName );
                    break;
                }
            }
        }

        /// <summary>
        /// Determine if the current type derives from <paramref name="baseType"/> using the specified type comparison flags.
        /// </summary>
        public static bool IsSubclassOf( this TypeDefinition type, TypeReference baseType, TypeComparisonFlags typeComparisonFlags = TypeComparisonFlags.MatchAll )
        {
            return type.BaseTypes().Any( t => t.IsSame( baseType, typeComparisonFlags ) );
        }

        /// <summary>
        /// Construct a real .NET attribute using the constructor arguments, properties and fields from a Cecil <see cref="CustomAttribute"/>.
        /// Constructions involving arguments, properties or fields of type <see cref="System.Type"/> are not supported.
        /// </summary>
        public static TAttribute ConstructRealAttributeOfType<TAttribute>( this CustomAttribute attribute )
        {
            return ( TAttribute ) ConstructRealAttribute( attribute, typeof( TAttribute ) );
        }

        /// <summary>
        /// Construct a real .NET attribute using the constructor arguments, properties and fields from a Cecil <see cref="CustomAttribute"/>.
        /// Constructions involving arguments, properties or fields of type <see cref="System.Type"/> are not supported.
        /// </summary>
        public static object ConstructRealAttribute( this CustomAttribute attribute, Type realAttributeType )
        {
            if ( attribute == null )
                throw new ArgumentNullException( "attribute" );

            if ( realAttributeType == null )
                throw new ArgumentNullException( "realAttributeType" );

            var ctorParameterTypes = attribute.ConstructorArguments.Select( ca => Type.GetType( ca.Type.FullName ) ).ToArray();

            if ( ctorParameterTypes.Any( t => ( t.HasElementType ? t.GetElementType() : t ) == typeof( Type ) ) )
                throw new InvalidOperationException( "Constructor parameters of type System.Type are not supported." );

            var ctor = realAttributeType.GetConstructor( ctorParameterTypes );

            if ( ctor == null )
                throw new InvalidOperationException( "The specified realAttributeType has no constuctor that takes arguments of the types present in the specified attribute." );
            
            var ctorParameterValues =
                attribute
                .ConstructorArguments
                .Select( 
                    ( ca, idx ) => 
                    {
                        var v = ca.Value;
                        var paramsArray = v as CustomAttributeArgument[];
                        if ( paramsArray != null )
                        {
                            var array = Array.CreateInstance( ctorParameterTypes[ idx ].GetElementType(), paramsArray.Length );
                            for ( int arrayIndex = 0; arrayIndex < paramsArray.Length; ++arrayIndex )
                            {
                                array.SetValue( paramsArray[ arrayIndex ].Value, arrayIndex );
                            }
                            return array;
                        }
                        else
                            return v;
                    } );

            var ctorParameterValuesArray = ctorParameterValues.ToArray();

            object obj;

            try
            {
                obj = ctor.Invoke( ctorParameterValuesArray );
            }
            catch ( System.Reflection.TargetInvocationException e )
            {
                throw e.InnerException;
            }

            foreach ( var propertyParam in attribute.Properties )
            {
                var prop = realAttributeType.GetProperty( propertyParam.Name );

                if ( prop == null || !prop.CanWrite )
                    throw new InvalidOperationException( string.Format( "The specified realAttributeType has no writeable property named '{0}'.", propertyParam.Name ) );

                if ( prop.PropertyType == typeof( System.Type ) )
                    throw new InvalidOperationException( "Properties of type System.Type are not supported." );

                prop.SetValue( obj, propertyParam.Argument.Value, null );
            }

            foreach ( var fieldParam in attribute.Fields )
            {
                var field = realAttributeType.GetField( fieldParam.Name );

                if ( field == null || field.IsInitOnly )
                    throw new InvalidOperationException( string.Format( "The specified realAttributeType has no writeable field named '{0}'.", fieldParam.Name ) );

                if ( field.FieldType == typeof( System.Type ) )
                    throw new InvalidOperationException( "Fields of type System.Type are not supported." );

                field.SetValue( obj, fieldParam.Argument.Value );
            }

            return obj;
        }

        /// <summary>
        /// Construct real .NET attributes using the constructor arguments, properties and fields from a the Cecil <see cref="CustomAttribute"/> of matching type
        /// from the specified <see cref="Mono.Cecil.ICustomAttributeProvider"/>.
        /// </summary>
        public static IEnumerable<TAttribute> GetRealCustomAttributesOfType<TAttribute>( this MemberReference member, TypeComparisonFlags typeComparisonFlags = TypeComparisonFlags.MatchAll )
        {
            var cap = member as ICustomAttributeProvider;

            if ( cap == null )
                throw new ArgumentException( "Does not implement ICustomAttributeProvider.", "member" );

            var tAttribute = member.Module.Import( typeof( TAttribute ) );

            return
                cap
                .CustomAttributes
                .Where( ca => ca.AttributeType.IsSame( tAttribute, typeComparisonFlags ) )
                .Select( ca => ca.ConstructRealAttributeOfType<TAttribute>() );
        }

        /// <summary>
        /// Returns the names of an enum type.
        /// </summary>
        public static IEnumerable<string> GetEnumNames( this TypeDefinition enumType )
        {
            if ( enumType == null )
                throw new ArgumentNullException( "enumType" );

            if ( !enumType.IsEnum )
                throw new ArgumentException( "Must be an enum type.", "enumType" );

            return enumType.Fields.Where( fi => fi.IsStatic && fi.IsPublic && !fi.IsSpecialName ).Select( fi => fi.Name );
        }
    }
}
