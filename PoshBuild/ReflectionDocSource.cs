using System.ComponentModel;
using System.Linq;
using System.Management.Automation;
using System.Xml;
using Mono.Cecil;
using MoreLinq;
using PoshBuild.ComponentModel;

namespace PoshBuild
{
    /// <summary>
    /// A attribute-based <see cref="IDocSource"/> implementation that uses reflection of Cmdlet types.
    /// </summary>
    sealed class ReflectionDocSource : AttributeBasedDocSource
    {
        override protected SynopsisAttribute GetSynposisAttribute( TypeDefinition cmdlet )
        {
            var tSynopsisAttribute = cmdlet.Module.Import( typeof( SynopsisAttribute ) );
            var attr = cmdlet.CustomAttributes.FirstOrDefault( ca => ca.AttributeType.FullName == tSynopsisAttribute.FullName );
            return attr == null ? null : attr.ConstructRealAttributeOfType<SynopsisAttribute>();
        }

        override protected DescriptionAttribute GetDescriptionAttribute( TypeDefinition cmdlet )
        {
            var tDescriptionAttribute = cmdlet.Module.Import( typeof( DescriptionAttribute ) );
            var attr = cmdlet.CustomAttributes.FirstOrDefault( ca => ca.AttributeType.FullName == tDescriptionAttribute.FullName );
            return attr == null ? null : attr.ConstructRealAttributeOfType<DescriptionAttribute>();
        }

        protected override GlobbingAttribute GetGlobbingAttribute( PropertyDefinition property, string parameterSetName )
        {
            var attrs = property.GetRealCustomAttributesOfType<GlobbingAttribute>().ToList();
            
            if ( attrs.Count == 0 )
                return null;

            return attrs.First( a => a.ParameterSetName == parameterSetName ) ?? attrs.First( a => a.ParameterSetName == ParameterAttribute.AllParameterSets );
        }

        public override bool WriteParameterDescription( XmlWriter writer, PropertyDefinition property, string parameterSetName )
        {
            var attrs = property.GetRealCustomAttributesOfType<ParameterAttribute>().ToList();

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
