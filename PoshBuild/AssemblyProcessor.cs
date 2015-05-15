using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Reflection;
using System.Xml;
using PoshBuild.ComponentModel;

namespace PoshBuild
{
    /// <summary>
    /// Generates MAML for an assembly.
    /// </summary>
    sealed class AssemblyProcessor
    {
        XmlWriter _writer;
        IEnumerable<Type> _types;
        IEnumerable<ICmdletHelpDescriptor> _descriptors;

        #region Constants

        public const string MshNs = "http://msh";
        public const string MamlNs = "http://schemas.microsoft.com/maml/2004/10";
        public const string CommandNs = "http://schemas.microsoft.com/maml/dev/command/2004/10";
        public const string DevNs = "http://schemas.microsoft.com/maml/dev/2004/10";

        #endregion

        #region Public Convenience Methods

        public static string GetHelpFileName( string snapInAssemblyName )
        {
            return string.Format( "{0}.dll-Help.xml", snapInAssemblyName );
        }

        public static IEnumerable<ICmdletHelpDescriptor> GetDescriptors( Assembly descriptorsAssembly )
        {
            var descriptors = new List<ICmdletHelpDescriptor>();
            if ( descriptorsAssembly != null )
            {
                var types = descriptorsAssembly.GetExportedTypes();
                var descriptorTypes = new List<Type>();
                foreach ( var type in types )
                {
                    if ( typeof( ICmdletHelpDescriptor ).IsAssignableFrom( type ) &&
                        Attribute.IsDefined( type, typeof( CmdletHelpDescriptorAttribute ) ) )
                    {
                        descriptorTypes.Add( type );
                    }
                }

                foreach ( var descriptorType in descriptorTypes )
                {
                    var descriptor = ( ICmdletHelpDescriptor ) Activator.CreateInstance( descriptorType );
                    descriptors.Add( descriptor );
                }
            }
            return descriptors;
        }

        #endregion

        #region Constructors

        AssemblyProcessor( IEnumerable<Type> types, XmlWriter writer, IEnumerable<ICmdletHelpDescriptor> descriptors )
        {
            _types = types;
            _writer = writer;
            _descriptors = descriptors;
        }

        #endregion

        #region Public Help Generation Methods

        public static void GenerateHelpFile( Assembly snapInAssembly, XmlWriter writer )
        {
            GenerateHelpFile( snapInAssembly, writer, null );
        }

        public static void GenerateHelpFile( Assembly snapInAssembly, XmlWriter writer, Assembly descriptorsAssembly )
        {
            if ( snapInAssembly == null )
                throw new ArgumentNullException( "snapInAssembly" );

            var descriptors = ( descriptorsAssembly != null ) ? GetDescriptors( descriptorsAssembly ) : null;
            var types = snapInAssembly.GetExportedTypes();

            GenerateHelpFile( types, writer, descriptors );
        }

        public static void GenerateHelpFile( IEnumerable<Type> types, XmlWriter writer, IEnumerable<ICmdletHelpDescriptor> descriptors )
        {
            new AssemblyProcessor( types, writer, descriptors ).GenerateHelpFile();
        }

        #endregion

        void GenerateHelpFile()
        {
            var sortedDescriptors = new Dictionary<Type, ICmdletHelpDescriptor>();
            if ( _descriptors != null )
            {
                foreach ( var descriptor in _descriptors )
                {
                    if ( descriptor == null )
                        throw new ArgumentException( "Found a null descriptor in the given descriptor collection." );
                    var descriptorType = descriptor.GetType();
                    var attribute = ( CmdletHelpDescriptorAttribute ) Attribute.GetCustomAttribute( descriptorType, typeof( CmdletHelpDescriptorAttribute ) );
                    if ( attribute == null )
                        throw new ArgumentException( "All descriptors must have the CmdletHelpDescriptorAttribute." );

                    sortedDescriptors.Add( attribute.DescribedType, descriptor );
                }
            }

            GenerateHelpFileStart();

            foreach ( Type type in _types )
            {
                if ( type.IsSubclassOf( typeof( Cmdlet ) ) &&
                    Attribute.IsDefined( type, typeof( CmdletAttribute ) ) )
                {
                    ICmdletHelpDescriptor descriptor = null;
                    sortedDescriptors.TryGetValue( type, out descriptor );
                    new CmdletTypeProcessor( _writer, type, descriptor ).GenerateHelpFileEntry();
                }
            }

            GenerateHelpFileEnd();
        }

        void GenerateHelpFileStart()
        {
            _writer.WriteStartDocument();
            _writer.WriteStartElement( "helpItems", MshNs );

            _writer.WriteAttributeString( "xmlns", "maml", null, MamlNs );
            _writer.WriteAttributeString( "xmlns", "command", null, CommandNs );
            _writer.WriteAttributeString( "xmlns", "dev", null, DevNs );
            _writer.WriteAttributeString( "schema", "maml" );
        }

        private void GenerateHelpFileEnd()
        {
            _writer.WriteEndElement();
        }
    }
}
