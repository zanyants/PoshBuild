using System.Collections.Generic;
using System.Xml;
using Mono.Cecil;

namespace PoshBuild
{
    /// <summary>
    /// Common documentation source methods that can be implemented by a variety of sources (eg, reflection, XmlDoc, descriptor).
    /// </summary>
    interface IDocSource
    {
        /// <summary>
        /// Writes a cmdlet synopsis. The writer should be positioned within a <c>&lt;maml:description></c> element.
        /// </summary>
        /// <returns><c>true</c> if synopsis information was written; otherwise <c>false</c>.</returns>
        bool WriteCmdletSynopsis( XmlWriter writer, TypeDefinition cmdlet );

        /// <summary>
        /// Writes a cmdlet description. The writer should be positioned within a <c>&lt;maml:description></c> element.
        /// </summary>
        /// <returns><c>true</c> if description information was written; otherwise <c>false</c>.</returns>
        bool WriteCmdletDescription( XmlWriter writer, TypeDefinition cmdlet );

        /// <summary>
        /// Writes a parameter description. If <see cref="parameterSetName"/> is <c>null</c>, the general parameterset-independent parameter 
        /// description within the &lt;command:parameters> section should be written.
        /// </summary>
        /// <param name="descendantTypes">
        /// The list of types derived from the declaring type of the specified <paramref name="property"/>, in most-derived-first order.
        /// This is supplied to allow documentation for parameters inherited from a base class to be overridden by a derived class.
        /// The implementation of <see cref="IDocSource"/> may ignore this parameter if parameter description overidding is not supported
        /// by the implementation.
        /// </param>
        /// <returns><c>true</c> if description information was written; otherwise <c>false</c>.</returns>
        bool WriteParameterDescription( XmlWriter writer, PropertyDefinition property, string parameterSetName, IEnumerable<TypeDefinition> descendantTypes );

        /// <summary>
        /// Writes a cmdlet examples. The writer should be positioned within a <c>&lt;command:examples></c> element.
        /// </summary>
        /// <returns><c>true</c> if example information was written; otherwise <c>false</c>.</returns>
        bool WriteCmdletExamples( XmlWriter writer, TypeDefinition cmdlet );

        /// <summary>
        /// Writes an cmdlet return value description. The writer should be positioned within a <c>&lt;maml:description></c> element.
        /// </summary>
        /// <returns><c>true</c> if description information was written; otherwise <c>false</c>.</returns>
        bool WriteReturnValueDescription( XmlWriter writer, TypeDefinition cmdlet, string outputTypeName );

        /// <summary>
        /// Writes an cmdlet input type description. The writer should be positioned within a <c>&lt;maml:description></c> element.
        /// </summary>
        /// <returns><c>true</c> if description information was written; otherwise <c>false</c>.</returns>
        bool WriteInputTypeDescription( XmlWriter writer, TypeDefinition cmdlet, string inputTypeName );

        /// <summary>
        /// Writes a cmdlet examples. The writer should be positioned within a <c>&lt;maml:alertSet></c> element.
        /// </summary>
        /// <returns><c>true</c> if note information was written; otherwise <c>false</c>.</returns>
        bool WriteCmdletNotes( XmlWriter writer, TypeDefinition cmdlet );

        /// <summary>
        /// Writes a cmdlet related links. The writer should be positioned within a <c>&lt;maml:relatedLinks></c> element.
        /// </summary>
        /// <returns><c>true</c> if related link information was written; otherwise <c>false</c>.</returns>
        bool WriteCmdletRelatedLinks( XmlWriter writer, TypeDefinition cmdlet );
        
        /// <summary>
        /// Attempts to get a value indicating if a property supports globbing when appearing in a particular parameterset.
        /// </summary>
        /// <returns><c>true</c> if globbing information was found; otherwise <c>false</c>.</returns>
        bool TryGetPropertySupportsGlobbing( PropertyDefinition property, string parameterSetName, out bool supportsGlobbing );
    }
}
