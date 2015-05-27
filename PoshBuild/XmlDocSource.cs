using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace PoshBuild
{
    // XML doc output file description
    // https://msdn.microsoft.com/en-us/library/fsbx0t7x.aspx

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

        class XslExtensions
        {
            Func<string, string, string> _getPrettyNameForIdentifier;

            public XslExtensions( Func<string,string,string> getPrettyNameForIdentifier )
            {
                _getPrettyNameForIdentifier = getPrettyNameForIdentifier;
            }

            public string GetPrettyNameForIdentifier( string declaringMemberIdentifier, string identifier )
            {
                return _getPrettyNameForIdentifier( declaringMemberIdentifier, identifier );
            }
        }

        public XmlDocSource( string xmlDocFile )
        {
            if ( string.IsNullOrEmpty( xmlDocFile ) )
                throw new ArgumentNullException( "xmlDocFile" );

            if ( !File.Exists( xmlDocFile ) )
                throw new FileNotFoundException( "File not found.", xmlDocFile );

            // Transform the file. This makes the content of the various documentation elements well-structured
            // and well-presented for maml use.
            var xslXmlDocToMaml = new XslCompiledTransform(
#if DEBUG
                    true
#endif
                );
            var xslNormalizeWhitespace = new XslCompiledTransform(
#if DEBUG
                    true
#endif                
                );
            var xslWrapBareText = new XslCompiledTransform(
#if DEBUG
                    true
#endif                
                );

            // Note: The XSL transform is compiled prior to the C# build, and a reference to the compiled assembly
            // is automatically (but transitively) added. This is done by the PoshBuild_CompileXsl target in PoshBuild.csproj.
            // Other than at build-time, Visual Studio may indicate that Xsl.XmlDocToMaml could not be found - this
            // "error" can normally be ignored. The uncompiled XSL transform is in Xsl\XmlDocToMaml.xsl.
            xslXmlDocToMaml.Load( typeof( Xsl.XmlDocToMaml ) );
            xslNormalizeWhitespace.Load( typeof( Xsl.NormalizeWhitespace ) );
            xslWrapBareText.Load( typeof( Xsl.WrapBareText ) );

            var xslArgs = new XsltArgumentList();
            xslArgs.AddExtensionObject( "urn:poshbuild", new XslExtensions( GetPSPrettyNameForIdentifier ) );
            
            try
            {
                using ( var sw = new StringWriter() )
                {
                    using ( var xw = XmlWriter.Create( sw ) )
                        xslXmlDocToMaml.Transform( xmlDocFile, xslArgs, xw );

                    using ( var sw2 = new StringWriter() )
                    {
                        using ( var sr = new StringReader( sw.ToString() ) )
                        using ( var xr = XmlReader.Create( sr ) )
                        using ( var xw2 = XmlWriter.Create( sw2 ) )
                            xslNormalizeWhitespace.Transform( xr, xw2 );

                        using ( var sw3 = new StringWriter() )
                        {
                            using ( var sr2 = new StringReader( sw2.ToString() ) )
                            using ( var xr2 = XmlReader.Create( sr2 ) )
                            using ( var xw3 = XmlWriter.Create( sw3 ) )
                                xslWrapBareText.Transform( xr2, xw3 );

                            using ( var sr3 = new StringReader( sw3.ToString() ) )
                                _xpd = new XPathDocument( sr3 );
                        }
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

        struct XmlDocIdentifier
        {
            // Note: xmldoc identifiers don't qualify nested types in an identifiable way - namespaces, class names and nested class
            // names are all separated with '.' characters. Such identifiers can be genuinely ambiguous, and there's not much
            // we can do about it. At this level the concept of nested classes is ignored - we interpret symbols as
            // Namespace.ClassName[.MemberName]

            public char Kind { get; private set; }
            public string Body { get; private set; }
            public string TypeFullName { get; private set; }
            public string TypeName { get; private set; }
            public string Namespace { get; private set; }
            public string Member { get; private set; }
            public string Overload { get; private set; }
            public string OriginalString { get; private set; }
            public bool IsCtor { get; private set; }

            public static bool TryParse( string identifier, out XmlDocIdentifier result )
            {
                result = default( XmlDocIdentifier );

                if ( string.IsNullOrWhiteSpace( identifier ) )
                    return false;

                if ( identifier.Length < 3 || identifier[ 1 ] != ':' )
                    return false;

                var kind = identifier[ 0 ];
                var body = identifier.Substring( 2 );
                string typeFullName = null;
                string member = null;
                string overload = null;
                string ns = null;
                string typeName = null;

                bool isCtor = false;

                switch ( kind )
                {
                    case 'N':
                    case '!':
                        break;
                    case 'T':
                        typeFullName = body;
                        break;
                    case 'F':
                    case 'P':
                    case 'E':
                    case 'M':
                        int searchPos = body.IndexOf( '(' );

                        if ( searchPos == -1 )
                            searchPos = body.Length;
                        else
                            overload = body.Substring( searchPos );

                        int memberSepIdx = body.LastIndexOf( '.', searchPos - 1 );

                        if ( memberSepIdx == -1 )
                            // Malformed.
                            return false;

                        typeFullName = body.Substring( 0, memberSepIdx );
                        member = body.Substring( memberSepIdx + 1, searchPos - memberSepIdx - 1 ).Replace( '#', '.' );
                        isCtor = kind == 'M' && ( member == ".ctor" || member == ".cctor" );
                        break;
                    default:
                        // Bad kind
                        return false;
                }

                if ( typeFullName != null )
                {
                    var splitTypeIdx = typeFullName.LastIndexOf( '.' );

                    if ( splitTypeIdx != -1 && splitTypeIdx != typeFullName.Length - 1 )
                    {
                        ns = typeFullName.Substring( 0, splitTypeIdx );
                        typeName = typeFullName.Substring( splitTypeIdx + 1 );
                    }
                    else
                    {
                        ns = string.Empty;
                        typeName = typeFullName;
                    }
                }

                result = new XmlDocIdentifier() 
                { 
                    Body = body, 
                    IsCtor = isCtor, 
                    Kind = kind, 
                    Member = member, 
                    Namespace = ns,
                    OriginalString = identifier, 
                    Overload = overload, 
                    TypeFullName = typeFullName,
                    TypeName = typeName
                };

                return true;
            }
        }

        static string GetPSPrettyNameForIdentifier( string declaringMemberIdentifier, string identifier )
        {
            if ( string.IsNullOrWhiteSpace( identifier ) )
                return string.Empty;

            XmlDocIdentifier xdiId;

            if ( !XmlDocIdentifier.TryParse( identifier, out xdiId ) )
            {
                if ( TaskContext.Current != null )
                {
                    TaskContext.Current.Log.LogWarning(
                        "PoshBuild",
                        "XDS03",
                        "",
                        "",
                        0, 0, 0, 0,
                        "The identifier '{0}' is malformed.",
                        identifier );
                }

                return identifier;
            }

            XmlDocIdentifier xdiDM;

            if ( !XmlDocIdentifier.TryParse( declaringMemberIdentifier, out xdiDM ) )
            {
                if ( TaskContext.Current != null )
                {
                    TaskContext.Current.Log.LogWarning(
                        "PoshBuild",
                        "XDS06",
                        "",
                        "",
                        0, 0, 0, 0,
                        "The declaringMemberIdentifier '{0}' is malformed.",
                        declaringMemberIdentifier );
                }
            }

            switch ( xdiId.Kind )
            {
                case 'N':
                    // Namespace
                    return xdiId.Body;
                case '!':
                    // Error string
                    if ( TaskContext.Current != null )
                    {
                        TaskContext.Current.Log.LogWarning(
                            "PoshBuild",
                            "XDS04",
                            "",
                            "",
                            0, 0, 0, 0,
                            "The identifier '{0}' represents a link that could not be resolved by the generating compiler and will be repeated as such in the generated PowerShell help file.",
                            xdiId.OriginalString );
                    }

                    return xdiId.OriginalString;
            }

            var sb = new StringBuilder();

            if ( xdiId.IsCtor || xdiDM.TypeFullName != xdiId.TypeFullName )
            {
                var prettyName = TypeNameHelper.GetPSPrettyName( xdiId.TypeFullName );
                
                // If GetPSPrettyName didn't transform the full typename, and this is a local reference, just use the type name, not the full name.
                if ( prettyName == xdiId.TypeFullName && xdiDM.TypeFullName == xdiId.TypeFullName )
                    prettyName = xdiId.TypeName;

                sb.Append( prettyName );

                if ( !xdiId.IsCtor && xdiId.Member != null )
                    sb.Append( '.' );
            }

            if ( !xdiId.IsCtor && xdiId.Member != null )
                sb.Append( xdiId.Member );

            if ( xdiId.Overload != null )
                sb.Append( PrettifyMemberOverload2( xdiId.Overload ) );
            else if ( xdiId.IsCtor )
                sb.Append( "()" );

            return sb.ToString();
        }

        const string _rxStrMatchOverload =
@"
[\(~]?
(?<TypeName>.+?)
[\@\*\^\&]?
( [\)\[\]\{\}\,\~]+ | $ )
";

        static readonly Regex _rxMatchOverload = new Regex( _rxStrMatchOverload, RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant );

        /// <summary>
        /// Appends part of an overload string that's not a regex-matched type name. Certain characters are replaced with PowerShell and C# style equivalents.
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="part"></param>
        static void AppendRawOverloadPart( StringBuilder sb, string part )
        {
            foreach ( var ch in part )
            {
                var ch2 = ch;

                switch ( ch )
                {
                    case '@':
                        // Use C#-style 'by ref' as this is more widely understood.
                        ch2 = '&';
                        break;
                    case '{':
                        // PowerShell uses square brackets for generic type args.
                        ch2 = '[';
                        break;
                    case '}':
                        // PowerShell uses square brackets for generic type args.
                        ch2 = ']';
                        break;
                }

                sb.Append( ch2 );
            }
        }

        /// <summary>
        /// Prettifies the portion of an xmldoc identifier in parentheses and thereafter (eg, method arguments)
        /// </summary>
        static string PrettifyMemberOverload2( string overload )
        {
            if ( string.IsNullOrWhiteSpace( overload ) )
                return overload;

            StringBuilder sb = new StringBuilder();

            int lastIndex = 0;

            var m = _rxMatchOverload.Match( overload );

            while ( m.Success )
            {
                foreach (
                    var g in
                    m
                    .Groups
                    .OfType<Group>()
                    .Skip( 1 )
                    .Where( g => g.Success )
                    .OrderBy( g => g.Index ) )
                {
                    if ( g.Index > lastIndex )
                        AppendRawOverloadPart( sb, overload.Substring( lastIndex, g.Index - lastIndex ) );

                    sb.Append( TypeNameHelper.GetPSPrettyName( g.Value ) );

                    lastIndex = g.Index + g.Length;
                }

                if ( m.Index + m.Length == overload.Length )
                    break;
                else
                    m = _rxMatchOverload.Match( overload, m.Index + m.Length );
            }

            if ( lastIndex < overload.Length )
                AppendRawOverloadPart( sb, overload.Substring( lastIndex ) );

            return sb.ToString();
        }
    }
}
