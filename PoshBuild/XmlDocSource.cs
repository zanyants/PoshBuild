﻿using System;
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

        public XmlDocSource( string xmlDocFile )
        {
            if ( string.IsNullOrEmpty( xmlDocFile ) )
                throw new ArgumentNullException( "xmlDocFile" );

            if ( !File.Exists( xmlDocFile ) )
                throw new FileNotFoundException( "File not found.", xmlDocFile );

            // Transform the file. This makes the content of the various documentation elements well-structured
            // and well-presented for maml use.
            var xsl = new XslCompiledTransform();

            // Note: The XSL transform is compiled prior to the C# build, and a reference to the compiled assembly
            // is automatically (but transitively) added. This is done by the PoshBuild_CompileXsl target in PoshBuild.csproj.
            // Other than at build-time, Visual Studio may indicate that Xsl.XmlDocToMaml could not be found - this
            // "error" can normally be ignored. The uncompiled XSL transform is in Xsl\XmlDocToMaml.xsl.
            xsl.Load( typeof( Xsl.XmlDocToMaml ) );

            using ( var sw = new StringWriter() )
            {
                using ( var xw = XmlWriter.Create( sw ) )
                    xsl.Transform( xmlDocFile, xw );

                using ( var sr = new StringReader( sw.ToString() ) )
                    _xpd = new XPathDocument( sr );
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
                xe = _xpd.CreateNavigator().SelectSingleNode( string.Format( "/doc/members/member[@name='{0}']/{1}", id, q ) );
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
