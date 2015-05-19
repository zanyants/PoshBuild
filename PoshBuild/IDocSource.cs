using System;
using System.Reflection;
using System.Xml;

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
        bool WriteCmdletSynopsis( XmlWriter writer, Type cmdlet );

        /// <summary>
        /// Writes a cmdlet description. The writer should be positioned within a <c>&lt;maml:description></c> element.
        /// </summary>
        /// <returns><c>true</c> if description information was written; otherwise <c>false</c>.</returns>
        bool WriteCmdletDescription( XmlWriter writer, Type cmdlet );

        /// <summary>
        /// Writes a parameter description. If <see cref="parameterSetName"/> is <c>null</c>, the general parameterset-independent parameter 
        /// description within the &lt;command:parameters> section should be written.
        /// </summary>
        /// <returns><c>true</c> if description information was written; otherwise <c>false</c>.</returns>
        bool WriteParameterDescription( XmlWriter writer, PropertyInfo property, string parameterSetName );

        /// <summary>
        /// Writes an cmdlet return value description. The writer should be positioned within a <c>&lt;maml:description></c> element.
        /// </summary>
        /// <returns><c>true</c> if description information was written; otherwise <c>false</c>.</returns>
        bool WriteReturnValueDescription( XmlWriter writer, Type cmdlet, string outputTypeName );
        
        /// <summary>
        /// Attempts to get a value indicating if a property supports globbing when appearing in a particular parameterset.
        /// </summary>
        /// <returns><c>true</c> if globbing information was found; otherwise <c>false</c>.</returns>
        bool TryGetPropertySupportsGlobbing( PropertyInfo property, string parameterSetName, out bool supportsGlobbing );
    }
}
