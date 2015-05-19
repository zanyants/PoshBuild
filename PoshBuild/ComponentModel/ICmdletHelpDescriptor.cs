namespace PoshBuild.ComponentModel
{
    /// <summary>
    /// Interface for a Cmdlet help descriptor.
    /// </summary>
    /// <remarks>
    /// A Cmdlet help descriptor gives information about a Cmdlet type for the purposes
    /// of the help file generation for that Cmdlet. This prevents the Cmdlet developer
    /// from having to use PoshBuild attributes directly on the Cmdlet type, which would
    /// create a mandatory reference to PoshBuild.dll. Instead he can describe his type
    /// in a separate class, or even a separate assembly, so that he can distribute his
    /// Cmdlet without PoshBuild.
    /// </remarks>
    public interface ICmdletHelpDescriptor
    {
        /// <summary>
        /// Get a synopsis for the described Cmdlet.
        /// </summary>
        /// <returns></returns>
        SynopsisAttribute GetSynopsis();

        /// <summary>
        /// Gets whether a given property of the described Cmdlet accepts wildcards.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        GlobbingAttribute GetGlobbing( string propertyName, string parameterSetName );
    }
}
