using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PoshBuild.ComponentModel
{
    /// <summary>
    /// Interface for a display format descriptor.
    /// </summary>
    /// <remarks>
    /// A display format descriptor gives information about a type for the purposes
    /// of being displayed in a Powershell host (console). This prevents the type developer
    /// from having to use PoshBuild attributes directly on the type, which would
    /// create a mandatory reference to PoshBuild.dll. Instead he can describe his type
    /// in a separate class, or even a separate assembly, so that he can distribute his
    /// type without PoshBuild.
    /// </remarks>
    public interface IDisplayFormatDescriptor
    {
        /// <summary>
        /// Gets the display format information for the type.
        /// </summary>
        /// <returns></returns>
        DisplayFormatAttribute GetDisplayFormatAttribute();

        /// <summary>
        /// Gets the display format information for a given property.
        /// </summary>
        /// <param name="propertyName">The name of the property</param>
        /// <returns></returns>
        DisplayFormatColumnAttribute GetDisplayFormatColumnAttribute(string propertyName);
    }
}
