using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Xml;

namespace PoshBuild
{
    /// <summary>
    /// Generates MAML for an assembly.
    /// </summary>
    sealed class AssemblyProcessor
    {
        XmlWriter _writer;
        IEnumerable<Type> _types;
        IDocSource _docSource;
        
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

        #endregion

        public AssemblyProcessor( XmlWriter writer, IEnumerable<Type> types, IDocSource docSource )
        {
            if ( writer == null )
                throw new ArgumentNullException( "writer" );

            if ( types == null )
                throw new ArgumentNullException( "types" );

            if ( docSource == null )
                throw new ArgumentNullException( "docSource" );

            _types = types;
            _writer = writer;
            _docSource = docSource;
        }

        public void GenerateHelpFile()
        {
            GenerateHelpFileStart();

            foreach ( Type type in _types )
            {
                if ( type.IsSubclassOf( typeof( Cmdlet ) ) &&
                    Attribute.IsDefined( type, typeof( CmdletAttribute ) ) )
                {
                    new CmdletTypeProcessor( _writer, type, _docSource).GenerateHelpFileEntry();
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
