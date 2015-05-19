using System;
using System.ComponentModel;
using System.Reflection;
using System.Xml;
using PoshBuild.ComponentModel;

namespace PoshBuild
{
    /// <summary>
    /// Base class for <see cref="IDocSource"/> implementations based on attributes.
    /// </summary>
    abstract class AttributeBasedDocSource : DocSource
    {
        abstract protected SynopsisAttribute GetSynposisAttribute( Type cmdlet );
        abstract protected DescriptionAttribute GetDescriptionAttribute( Type cmdlet );
        abstract protected GlobbingAttribute GetGlobbingAttribute( PropertyInfo property, string parameterSetName );

        override public bool WriteCmdletSynopsis( XmlWriter writer, Type cmdlet )
        {
            var synopsisAttribute = GetSynposisAttribute( cmdlet );

            if ( synopsisAttribute != null )
            {
                writer.WriteElementString( "maml", "para", null, synopsisAttribute.Synopsis );
                return true;
            }

            return false;
        }

        override public bool WriteCmdletDescription( XmlWriter writer, Type cmdlet )
        {
            var descriptionAttribute = GetDescriptionAttribute( cmdlet );

            if ( descriptionAttribute != null )
            {
                writer.WriteElementString( "maml", "para", null, descriptionAttribute.Description );
                return true;
            }

            return false;
        }

        public override bool TryGetPropertySupportsGlobbing( PropertyInfo property, string parameterSetName, out bool supportsGlobbing )
        {
            supportsGlobbing = default( bool );

            var globbingAttribute = GetGlobbingAttribute( property, parameterSetName );

            if ( globbingAttribute != null )
            {
                supportsGlobbing = globbingAttribute.SupportsGlobbing;
                return true;
            }

            return false;
        }

        
    }
}
