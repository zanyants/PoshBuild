using System.Xml;
using Mono.Cecil;

namespace PoshBuild
{
    /// <summary>
    /// Base that provides a default implementation for each <see cref="IDocSource"/> member.
    /// </summary>
    abstract class DocSource : IDocSource
    {
        virtual public bool WriteCmdletSynopsis( XmlWriter writer, TypeDefinition cmdlet )
        {
            return false;
        }

        virtual public bool WriteCmdletDescription( XmlWriter writer, TypeDefinition cmdlet )
        {
            return false;
        }

        virtual public bool TryGetPropertySupportsGlobbing( PropertyDefinition property, string parameterSetName, out bool supportsGlobbing )
        {
            supportsGlobbing = default( bool );
            return false;
        }

        virtual public bool WriteParameterDescription( XmlWriter writer, PropertyDefinition property, string parameterSetName )
        {
            return false;
        }

        virtual public bool WriteReturnValueDescription( XmlWriter writer, TypeDefinition cmdlet, string outputTypeName )
        {
            return false;
        }

        virtual public bool WriteCmdletExamples( XmlWriter writer, TypeDefinition cmdlet )
        {
            return false;
        }

        virtual public bool WriteInputTypeDescription( XmlWriter writer, TypeDefinition cmdlet, string inputTypeName )
        {
            return false;
        }

        virtual public bool WriteCmdletNotes( XmlWriter writer, TypeDefinition cmdlet )
        {
            return false;
        }

        virtual public bool WriteCmdletRelatedLinks( XmlWriter writer, TypeDefinition cmdlet )
        {
            return false;
        }
    }
}
