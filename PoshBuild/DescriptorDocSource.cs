using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using PoshBuild.ComponentModel;

namespace PoshBuild
{
    /// <summary>
    /// A attribute-based <see cref="IDocSource"/> implementation that uses the PoshBuild descriptor system.
    /// </summary>
    sealed class DescriptorDocSource : AttributeBasedDocSource
    {
        #region Helpers

        public static IDictionary<Type, ICmdletHelpDescriptor> GetDescriptors( params Assembly[] descriptorsAssemblies )
        {
            return GetDescriptors( descriptorsAssemblies as IEnumerable<Assembly> );
        }

        public static IDictionary<Type, ICmdletHelpDescriptor> GetDescriptors( IEnumerable<Assembly> descriptorsAssemblies )
        {
            if ( descriptorsAssemblies == null )
                return null;

            return
                descriptorsAssemblies
                .SelectMany( assy => assy.GetExportedTypes() )
                .Where(
                    t => typeof( ICmdletHelpDescriptor ).IsAssignableFrom( t ) &&
                         Attribute.IsDefined( t, typeof( CmdletHelpDescriptorAttribute ) ) )
                .ToDictionary(
                    t => ( ( CmdletHelpDescriptorAttribute ) Attribute.GetCustomAttribute( t, typeof( CmdletHelpDescriptorAttribute ) ) ).DescribedType,
                    t => ( ICmdletHelpDescriptor ) Activator.CreateInstance( t ) );
        }

        #endregion

        IDictionary<Type, ICmdletHelpDescriptor> _descriptors;

        public DescriptorDocSource( IDictionary<Type, ICmdletHelpDescriptor> descriptors )
        {
            _descriptors = descriptors;
        }

        protected override DescriptionAttribute GetDescriptionAttribute( Type cmdlet )
        {
            var descriptor = GetDescriptor( cmdlet );
            return null;
        }

        protected override SynopsisAttribute GetSynposisAttribute( Type cmdlet )
        {
            var descriptor = GetDescriptor( cmdlet );

            if ( descriptor != null )
                return descriptor.GetSynopsis();

            return null;
        }

        protected override GlobbingAttribute GetGlobbingAttribute( PropertyInfo property, string parameterSetName )
        {
            var descriptor = GetDescriptor( property );

            if ( descriptor != null )
                return descriptor.GetGlobbing( property.Name, parameterSetName );

            return null;
        }

        ICmdletHelpDescriptor GetDescriptor( MemberInfo member )
        {
            ICmdletHelpDescriptor descriptor = null;

            if ( _descriptors != null )
            {
                switch ( member.MemberType )
                {
                    case MemberTypes.TypeInfo:
                        _descriptors.TryGetValue( ( Type ) member, out descriptor );
                        break;
                }
            }

            return descriptor;
        }
    }
}
