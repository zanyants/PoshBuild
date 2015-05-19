using System;
using System.Reflection;
using System.Xml;

namespace PoshBuild
{
    /// <summary>
    /// Base that provides a default implementation for each <see cref="IDocSource"/> member.
    /// </summary>
    abstract class DocSource : IDocSource
    {
        virtual public bool WriteCmdletSynopsis( XmlWriter writer, Type cmdlet )
        {
            return false;
        }

        virtual public bool WriteCmdletDescription( XmlWriter writer, Type cmdlet )
        {
            return false;
        }

        virtual public bool TryGetPropertySupportsGlobbing( PropertyInfo property, string parameterSetName, out bool supportsGlobbing )
        {
            supportsGlobbing = default( bool );
            return false;
        }

        virtual public bool WriteParameterDescription( XmlWriter writer, PropertyInfo property, string parameterSetName )
        {
            return false;
        }

        virtual public bool WriteReturnValueDescription( XmlWriter writer, Type cmdlet, string outputTypeName )
        {
            return false;
        }
    }
}
