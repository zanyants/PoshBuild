using System;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Xml;
using PoshBuild.ComponentModel;
using MoreLinq;

namespace PoshBuild
{
    /// <summary>
    /// A attribute-based <see cref="IDocSource"/> implementation that uses reflection of Cmdlet types.
    /// </summary>
    sealed class ReflectionDocSource : AttributeBasedDocSource
    {
        override protected SynopsisAttribute GetSynposisAttribute( Type cmdlet )
        {
            return ( SynopsisAttribute ) Attribute.GetCustomAttribute( cmdlet, typeof( SynopsisAttribute ) );
        }

        override protected DescriptionAttribute GetDescriptionAttribute( Type cmdlet )
        {
            return ( DescriptionAttribute ) Attribute.GetCustomAttribute( cmdlet, typeof( DescriptionAttribute ) );
        }

        protected override GlobbingAttribute GetGlobbingAttribute( PropertyInfo property, string parameterSetName )
        {
            var attrs = property.GetCustomAttributes( typeof( GlobbingAttribute ), true ).OfType<GlobbingAttribute>().ToList();
            
            if ( attrs.Count == 0 )
                return null;

            return attrs.First( a => a.ParameterSetName == parameterSetName ) ?? attrs.First( a => a.ParameterSetName == ParameterAttribute.AllParameterSets );
        }

        public override bool WriteParameterDescription( XmlWriter writer, PropertyInfo property, string parameterSetName )
        {
            var attrs = property.GetCustomAttributes( typeof( ParameterAttribute ), true ).OfType<ParameterAttribute>().ToList();

            if ( attrs.Count == 0 )
                return false;

            var attr = 
                parameterSetName == null ?
                    // For general parameter description, choose the longest HelpMessage.
                    attrs.MaxBy( a => a.HelpMessage == null ? 0 : a.HelpMessage.Length ) :
                    // For specific paramterset, look for exact parameterset match, then consider AllParameterSets set.
                    attrs.First( a => a.ParameterSetName == parameterSetName ) ?? attrs.First( a => a.ParameterSetName == ParameterAttribute.AllParameterSets );

            if ( attr != null && !string.IsNullOrWhiteSpace( attr.HelpMessage) )
            {                
                writer.WriteElementString( "maml", "para", null, attr.HelpMessage );
                return true;
            }

            return false;
        }        
    }
}
