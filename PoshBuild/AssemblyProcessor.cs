using System;
using System.Linq;
using System.Management.Automation;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Mono.Cecil;

namespace PoshBuild
{
    /// <summary>
    /// Generates MAML for an assembly.
    /// </summary>
    sealed class AssemblyProcessor
    {
        XmlWriter _finalWriter;
        XmlWriter _writer;
        IDocSource _docSource;
        AssemblyDefinition _assembly;
        
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

        public AssemblyProcessor( XmlWriter writer, AssemblyDefinition assembly, IDocSource docSource )
        {
            if ( writer == null )
                throw new ArgumentNullException( "writer" );

            if ( assembly == null )
                throw new ArgumentNullException( "assembly" );

            if ( docSource == null )
                throw new ArgumentNullException( "docSource" );

            _assembly = assembly;
            _finalWriter = writer;            
            _docSource = docSource;
        }

        public void GenerateHelpFile()
        {
            var xd = new XDocument();

            using ( _writer = xd.CreateWriter() )
            {
                GenerateHelpFileStart();

                foreach ( var module in _assembly.Modules )
                {
                    var tCmdlet = module.Import( typeof( Cmdlet ) );
                    var tCmdletAttribute = module.Import( typeof( CmdletAttribute ) );

                    if ( tCmdlet == null || tCmdletAttribute == null )
                        // The assembly doesn't reference System.Management.Automation, nothing to do.
                        continue;

                    foreach ( var type in module.Types.Where( t => t.IsPublic && !t.IsValueType && !t.IsAbstract ) )
                    {
                        if ( type.CustomAttributes.Any( ca => ca.AttributeType.IsSame( tCmdletAttribute, CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ) ) && type.IsSubclassOf( tCmdlet, CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ) )
                        {
                            new CmdletTypeProcessor( _writer, type, _docSource ).GenerateHelpFileEntry();
                        }
                    }
                }

                GenerateHelpFileEnd();
            }

            // Apply post-processing
            var xslPostProcess = new XslCompiledTransform(
#if DEBUG
                true
#endif
                );

            // Note: The XSL transform is compiled prior to the C# build, and a reference to the compiled assembly
            // is automatically (but transitively) added. This is done by the PoshBuild_CompileXsl target in PoshBuild.csproj.
            // Other than at build-time, Visual Studio may indicate that Xsl.PostProcess could not be found - this
            // "error" can normally be ignored. The uncompiled XSL transform is in Xsl\PostProcess.xsl.
            xslPostProcess.Load( typeof( Xsl.PostProcess ) );
            xslPostProcess.Transform( xd.CreateReader( ReaderOptions.OmitDuplicateNamespaces ), _finalWriter );
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
