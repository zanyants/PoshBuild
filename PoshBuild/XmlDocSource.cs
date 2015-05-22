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
            public char Kind { get; private set; }
            public string Body { get; private set; }
            public string Type { get; private set; }
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
                string type = null;
                string member = null;
                string overload = null;
                bool isCtor = false;

                switch ( kind )
                {
                    case 'N':
                    case '!':
                        break;
                    case 'T':
                        type = body;
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
                        type = body.Substring( 0, memberSepIdx );
                        member = body.Substring( memberSepIdx + 1, searchPos - memberSepIdx - 1 ).Replace( '#', '.' );
                        isCtor = kind == 'M' && ( member == ".ctor" || member == ".cctor" );
                        break;
                    default:
                        // Bad kind
                        return false;
                }

                result = new XmlDocIdentifier() { Body = body, IsCtor = isCtor, Kind = kind, Member = member, OriginalString = identifier, Overload = overload, Type = type };
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

            // xmldoc identifiers don't qualify nested types in an identifiable way - namespaces, class names and nested class
            // names are all separated with '.' characters. Such identifiers can be genuinely ambiguous, and there's not much
            // we can do about it.

            // We don't want to qualify identifiers local to the current cmdlet (other than ctors)
            string useTypeName =
                !xdiId.IsCtor && xdiDM.Type == xdiId.Type ?
                null :
                GetBestPSPrettyNameForType( xdiId.Type );

            var sb = new StringBuilder();

            if ( xdiId.IsCtor || xdiDM.Type != xdiId.Type )
            {
                sb.Append( GetBestPSPrettyNameForType( xdiId.Type ) );
                if ( !xdiId.IsCtor && xdiId.Member != null )
                    sb.Append( '.' );
            }

            if ( !xdiId.IsCtor && xdiId.Member != null )
                sb.Append( xdiId.Member );

            if ( xdiId.Overload != null )
                sb.Append( PrettifyMemberOverload( xdiId.Overload ) );

            return sb.ToString();
        }

        const string _rxStrMatchOverload = @"
  \(?
  (?<TypeName>.+?)
  (
	   ((?<ByRef>@)|(?<Ptr>\*)|(?<Pinned>\^))?
    (\[.*?\])?
    (
	     (
		      \)(\~(?<ConversionTypeName>.+))?
	     )
	     |
	     ,
	   )
  )
";

        static readonly Regex _rxMatchOverload = new Regex( _rxStrMatchOverload, RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture );

        /// <summary>
        /// Prettifies the portion of an xmldoc identifier in parentheses and thereafter (eg, method arguments)
        /// </summary>
        /// <param name="overload"></param>
        /// <returns></returns>
        static string PrettifyMemberOverload( string overload )
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
                    .Select( ( g, idx ) => new { Group = g, Name = _rxMatchOverload.GroupNameFromNumber( idx ) } )
                    .Skip( 1 )
                    .Where( g => g.Group.Success )
                    .OrderBy( g => g.Group.Index ) )
                {
                    if ( g.Group.Index > lastIndex )
                        sb.Append( overload.Substring( lastIndex, g.Group.Index - lastIndex ) );

                    switch ( g.Name )
                    {
                        case "TypeName":
                        case "ConversionTypeName":
                            // TODO: Handle closed generic types, which would look like "System.Collections.Generic.IEnumerable{System.String}"
                            sb.Append( GetBestPSPrettyNameForType( g.Group.Value ) );
                            break;
                        case "ByRef":
                            sb.Append( "&" );
                            break;
                        case "Ptr":
                            sb.Append( "*" );
                            break;
                        case "Pinned":
                            sb.Append( "^" );
                            break;
                    }

                    lastIndex = g.Group.Index + g.Group.Length;
                }

                if ( m.Index + m.Length == overload.Length )
                    break;
                else
                    m = _rxMatchOverload.Match( overload, m.Index + m.Length );
            }

            if ( lastIndex < overload.Length )
                sb.Append( overload.Substring( lastIndex ) );

            return sb.ToString();
        }

        /// <summary>
        /// Does best attempt to resolve a type and get a pretty name for it.
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        static string GetBestPSPrettyNameForType( string typeName )
        {
            // TODO: Handle closed generic types, which would look like "System.Collections.Generic.IEnumerable{System.String}"
            if ( typeName.Contains( '{') )
            {
                if ( TaskContext.Current != null )
                {
                    TaskContext.Current.Log.LogWarning(
                        "PoshBuild",
                        "XDS05",
                        "",
                        "",
                        0, 0, 0, 0,
                        "Type name '{0}' is a closed generic type, which is not presently supported. It will not be rendered 'pretty' in the generated PowerShell help file.",
                        typeName );
                }

                return typeName;                
            }
            
            Type type = null;

            // xmldoc identifiers don't qualify nested types in an identifiable way - namespaces, class names and nested class
            // names are all separated with '.' characters. Such identifiers can be genuinely ambiguous, and there's not much
            // we can do about it.
            var typeNameParts = typeName.Split( '.' );

            for ( int numPartsToNest = 0; numPartsToNest < typeNameParts.Length; ++numPartsToNest )
            {
                var baseTypeName =
                    string.Join(
                        ".",
                        typeNameParts
                        .Take( typeNameParts.Length - numPartsToNest ) );

                try
                {
                    var baseType = Type.GetType( baseTypeName );
                    
                    if ( baseType != null )
                    {
                        if ( numPartsToNest > 0 )
                        {
                            var partsToNest =
                                typeNameParts
                                .Skip( typeNameParts.Length - numPartsToNest );

                            foreach ( var partToNest in partsToNest )
                            {
                                baseType = baseType.GetNestedType( partToNest, BindingFlags.Public | BindingFlags.NonPublic );
                                if ( baseType == null )
                                    break;
                            }
                        }

                        type = baseType;
                    }
                }
                catch { }

                if ( type != null )
                    break;
            }

            if ( type == null )
            {
                if ( TaskContext.Current != null )
                {
                    TaskContext.Current.Log.LogWarning(
                        "PoshBuild",
                        "XDS02",
                        "",
                        "",
                        0, 0, 0, 0,
                        "Type name '{0}' could not be resolved and will not be rendered 'pretty' in the generated PowerShell help file.",
                        typeName );
                }

                return typeName;
            }
            else
                return type.GetPSPrettyName();
        }
    }
}
