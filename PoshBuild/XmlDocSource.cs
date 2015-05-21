using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace PoshBuild
{
    /// <summary>
    /// An <see cref="IDocSource"/> implementation that retrieves documentation from compiler-generated XML documentation files.
    /// </summary>
    sealed class XmlDocSource : DocSource
    {
        XPathDocument _xpd;
        static readonly XmlNamespaceManager _namespaceResolver;

        static XmlDocSource()
        {
            _namespaceResolver = new XmlNamespaceManager( new NameTable() );
            _namespaceResolver.AddNamespace( "msh", "http://msh" );
            _namespaceResolver.AddNamespace( "maml", "http://schemas.microsoft.com/maml/2004/10" );
            _namespaceResolver.AddNamespace( "command", "http://schemas.microsoft.com/maml/dev/command/2004/10" );
            _namespaceResolver.AddNamespace( "dev", "http://schemas.microsoft.com/maml/dev/2004/10" );
        }

        public XmlDocSource( string xmlDocFile )
        {
            if ( string.IsNullOrEmpty( xmlDocFile ) )
                throw new ArgumentNullException( "xmlDocFile" );

            if ( !File.Exists( xmlDocFile ) )
                throw new FileNotFoundException( "File not found.", xmlDocFile );

            // Transform the file. This makes the content of the various documentation elements well-structured
            // and well-presented for maml use.
            var xslXmlDocToMaml = new XslCompiledTransform();
            var xslNormalizeWhitespace = new XslCompiledTransform();

            // Note: The XSL transform is compiled prior to the C# build, and a reference to the compiled assembly
            // is automatically (but transitively) added. This is done by the PoshBuild_CompileXsl target in PoshBuild.csproj.
            // Other than at build-time, Visual Studio may indicate that Xsl.XmlDocToMaml could not be found - this
            // "error" can normally be ignored. The uncompiled XSL transform is in Xsl\XmlDocToMaml.xsl.
            xslXmlDocToMaml.Load( typeof( Xsl.XmlDocToMaml ) );
            xslNormalizeWhitespace.Load( typeof( Xsl.NormalizeWhitespace ) );

            try
            {
                using ( var sw = new StringWriter() )
                {
                    using ( var xw = XmlWriter.Create( sw ) )
                        xslXmlDocToMaml.Transform( xmlDocFile, xw );

                    using ( var sw2 = new StringWriter() )
                    {
                        using ( var sr = new StringReader( sw.ToString() ) )
                        using ( var xr = XmlReader.Create( sr ) )
                        using ( var xw2 = XmlWriter.Create( sw2 ) )
                            xslNormalizeWhitespace.Transform( xr, xw2 );

                        using ( var sr2 = new StringReader( sw2.ToString() ) )
                            _xpd = new XPathDocument( sr2 );
                    }                        
                }
            }
            catch ( XsltException e )
            {
                if ( TaskContext.Current != null )
                {
                    TaskContext.Current.Log.LogError(
                        "PoshBuild",
                        "XDS01",
                        "",
                        xmlDocFile,
                        e.LineNumber,
                        e.LinePosition,
                        e.LineNumber,
                        e.LinePosition,
                        e.Message );
                }
                else
                    throw;
            }
        }

        bool WriteDescription( XmlWriter writer, MemberInfo member, string elementName )
        {
            return WriteDescriptionEx( writer, member, "ps" + elementName, elementName );
        }

        bool WriteDescriptionEx( XmlWriter writer, MemberInfo member, params string[] subQueries )
        {
            var id = GetIdentifier( member );

            XPathNavigator xe = null;

            foreach ( var q in subQueries )
            {
                xe = _xpd.CreateNavigator().SelectSingleNode( string.Format( "/doc/members/member[@name='{0}']/{1}", id, q ), _namespaceResolver );
                if ( xe != null )
                    break;
            }

            if ( xe != null && xe.HasChildren )
            {
                foreach ( var child in xe.SelectChildren( XPathNodeType.Element ).OfType<XPathNavigator>() )
                    writer.WriteNode( child, false );

                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Writes a member synopsis, typically taken from the <c>&lt;summary></c> tag. The writer should be
        /// positioned within a <c>&lt;maml:description></c> element.
        /// </summary>
        /// <returns><c>true</c> if synopsis information was written; otherwise <c>false</c>.</returns>
        override public bool WriteCmdletSynopsis( XmlWriter writer, Type cmdlet )
        {
            return WriteDescription( writer, cmdlet, "summary" );
        }

        /// <summary>
        /// Writes a member synopsis, typically taken from the <c>&lt;remarks></c> tag. The writer should be
        /// positioned within a <c>&lt;maml:description></c> element.
        /// </summary>
        /// <returns><c>true</c> if synopsis information was written; otherwise <c>false</c>.</returns>
        override public bool WriteCmdletDescription( XmlWriter writer, Type cmdlet )
        {
            return WriteDescription( writer, cmdlet, "remarks" );
        }

        public override bool WriteParameterDescription( XmlWriter writer, PropertyInfo property, string parameterSetName )
        {
            return WriteDescription( writer, property, "summary" );
        }

        public override bool WriteReturnValueDescription( XmlWriter writer, Type cmdlet, string outputTypeName )
        {
            return WriteDescriptionEx( writer, cmdlet, string.Format( "psoutput[@type='{0}']", outputTypeName ) );
        }

        public override bool WriteInputTypeDescription( XmlWriter writer, Type cmdlet, string inputTypeName )
        {
            return WriteDescriptionEx( writer, cmdlet, string.Format( "psinput[@type='{0}']", inputTypeName ) );
        }

        public override bool WriteCmdletExamples( XmlWriter writer, Type cmdlet )
        {
            var id = GetIdentifier( cmdlet );

            var examples = _xpd.CreateNavigator().Select( string.Format( "/doc/members/member[@name='{0}']/example/command:example", id ), _namespaceResolver );

            bool didWrite = false;

            foreach ( var example in examples )
            {
                writer.WriteNode( ( XPathNavigator ) example, false );
                didWrite = true;
            }

            return didWrite;
        }

        public override bool WriteCmdletNotes( XmlWriter writer, Type cmdlet )
        {
            var id = GetIdentifier( cmdlet );

            var notes = _xpd.CreateNavigator().Select( string.Format( "/doc/members/member[@name='{0}']/psnote", id ) ).OfType<XPathNavigator>();

            bool didWrite = false;

            foreach ( var xe in notes.SelectMany( xpn => xpn.SelectChildren( XPathNodeType.Element ).OfType<XPathNavigator>() ) )
            {
                writer.WriteNode( xe, false );
                didWrite = true;
            }

            return didWrite;
        }

        public override bool WriteCmdletRelatedLinks( XmlWriter writer, Type cmdlet )
        {
            var id = GetIdentifier( cmdlet );

            var notes = _xpd.CreateNavigator().Select( string.Format( "/doc/members/member[@name='{0}']/psrelated", id ) ).OfType<XPathNavigator>();

            bool didWrite = false;

            foreach ( var xe in notes.SelectMany( xpn => xpn.SelectChildren( XPathNodeType.Element ).OfType<XPathNavigator>() ) )
            {
                writer.WriteNode( xe, false );
                didWrite = true;
            }

            return didWrite;
        }

        public override bool TryGetPropertySupportsGlobbing( PropertyInfo property, string parameterSetName, out bool supportsGlobbing )
        {
            supportsGlobbing = default( bool );
            var id = GetIdentifier( property );
                        
            var xe =
                // Exact match
                _xpd.CreateNavigator().SelectSingleNode( string.Format( "/doc/members/member[@name='{0}']/psparameter[@globbing and @parametersetname='{1}']/@globbing", id, parameterSetName ) )
                ??
                // Match when parametersetname not specified (equivalent to __AllParameterSets)
                _xpd.CreateNavigator().SelectSingleNode( string.Format( "/doc/members/member[@name='{0}']/psparameter[@globbing and not( @parametersetname )]/@globbing", id ) );

            if ( xe != null && !string.IsNullOrWhiteSpace( xe.Value ) )
            {
                supportsGlobbing = xe.ValueAsBoolean;
                return true;
            }
            else
                return false;
        }

        static string GetIdentifier( MemberInfo member )
        {
            switch ( member.MemberType )
            {
                case MemberTypes.TypeInfo:
                    return string.Format( "T:{0}", ( ( Type ) member ).FullName );
                case MemberTypes.Property:
                    return string.Format( "P:{0}.{1}", member.DeclaringType.FullName, ( ( PropertyInfo ) member ).Name );
                case MemberTypes.Field:
                    return string.Format( "F:{0}.{1}", member.DeclaringType.FullName, ( ( PropertyInfo ) member ).Name );
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
