using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Xml;
using PoshBuild.ComponentModel;

namespace PoshBuild
{
    public sealed class CmdletHelp
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

        CmdletHelp( IEnumerable<Type> types, XmlWriter writer, IEnumerable<ICmdletHelpDescriptor> descriptors )
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
            new CmdletHelp( types, writer, descriptors ).GenerateHelpFile();
        }

        #endregion

        #region Private Methods

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
                    GenerateHelpFileEntry( type, descriptor );
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

        void GenerateHelpFileEntry( Type cmdletType, ICmdletHelpDescriptor descriptor )
        {
            if ( cmdletType == null )
                throw new ArgumentNullException( "cmdletType" );

            if ( !cmdletType.IsSubclassOf( typeof( Cmdlet ) ) )
                throw new ArgumentException( "The given Cmdlet type does not inherit from Cmdlet." );

            CmdletAttribute cmdletAttribute = ( CmdletAttribute ) Attribute.GetCustomAttribute( cmdletType, typeof( CmdletAttribute ) );

            if ( cmdletAttribute == null )
                throw new ArgumentException( "The given Cmdlet type does not have the CmdletAttribute attribute." );

            CmdletParametersInfo parametersInfo = new CmdletParametersInfo( cmdletType, descriptor );

            _writer.WriteStartElement( "command", "command", null );

            GenerateCommandDetails( cmdletType, cmdletAttribute, descriptor );
            GenerateCommandDescription( cmdletType, cmdletAttribute );
            GenerateCommandSyntax( cmdletType, cmdletAttribute, parametersInfo );
            GenerateCommandParameters( cmdletType, cmdletAttribute, parametersInfo );
            GenerateCommandReturnValues( cmdletType, cmdletAttribute );
            
            _writer.WriteEndElement(); // </command:command>
        }

        
        void GenerateCommandDetails( Type cmdletType, CmdletAttribute cmdletAttribute, ICmdletHelpDescriptor descriptor )
        {
            _writer.WriteStartElement( "command", "details", null );

            _writer.WriteElementString( "command", "name", null, string.Format( "{0}-{1}", cmdletAttribute.VerbName, cmdletAttribute.NounName ) );
            _writer.WriteElementString( "command", "verb", null, cmdletAttribute.VerbName.ToLowerInvariant() );
            _writer.WriteElementString( "command", "noun", null, cmdletAttribute.NounName.ToLowerInvariant() );

            SynopsisAttribute synopsisAttribute = null;
            
            if ( descriptor != null )
                synopsisAttribute = descriptor.GetSynopsis();

            if ( synopsisAttribute == null )
                synopsisAttribute = ( SynopsisAttribute ) Attribute.GetCustomAttribute( cmdletType, typeof( SynopsisAttribute ) );

            if ( synopsisAttribute != null )
            {
                _writer.WriteStartElement( "maml", "description", null );
                {
                    _writer.WriteElementString( "maml", "para", null, synopsisAttribute.Synopsis );
                }
                _writer.WriteEndElement(); // </maml:description>
            }

            _writer.WriteEndElement(); // </command:details>
        }


        void GenerateCommandReturnValues( Type cmdletType, CmdletAttribute cmdletAttribute )
        {
            var attributes = cmdletType.GetCustomAttributes( typeof( OutputTypeAttribute ), true ).OfType<OutputTypeAttribute>().ToList();

            if ( attributes.Count > 0 )
            {
                _writer.WriteStartElement( "command", "returnValues", null );

                foreach ( var attr in attributes )
                {
                    var parameterSetNames = attr.ParameterSetName == null ? null : attr.ParameterSetName.Where( s => !string.IsNullOrWhiteSpace( s ) && s != ParameterAttribute.AllParameterSets ).ToList();

                    foreach ( var type in attr.Type )
                    {
                        _writer.WriteStartElement( "command", "returnValue", null );

                        _writer.WriteStartElement( "dev", "type", null );

                        _writer.WriteElementString( "maml", "name", null, type.Name );
                        _writer.WriteElementString( "maml", "uri", null, string.Empty );
                        // This description element is *not* read by Get-Help.
                        _writer.WriteElementString( "maml", "description", null, string.Empty );

                        _writer.WriteEndElement(); // </dev:type>

                        // This description element *is* read by Get-Help.
                        _writer.WriteStartElement( "maml", "description", null );

                        if ( !string.IsNullOrWhiteSpace( attr.ProviderCmdlet ) )
                            _writer.WriteElementString( "maml", "para", null, string.Format( "Applies to Provider Cmdlet '{0}'.", attr.ProviderCmdlet ) );

                        if ( parameterSetNames != null && parameterSetNames.Count > 0 )
                        {
                            _writer.WriteElementString( "maml", "para", null, "Applies to parameter sets:" );

                            foreach ( var name in parameterSetNames )
                                _writer.WriteElementString( "maml", "para", null, "-- " + name );
                        }

                        _writer.WriteEndElement(); // </maml:description>

                        _writer.WriteEndElement(); // </command:returnValue>
                    }

                }

                _writer.WriteEndElement(); // </command:returnValues>
            }
        }

        void GenerateCommandDescription( Type cmdletType, CmdletAttribute cmdletAttribute )
        {
            DescriptionAttribute descriptionAttribute = ( DescriptionAttribute ) Attribute.GetCustomAttribute( cmdletType, typeof( DescriptionAttribute ) );
            if ( descriptionAttribute != null )
            {
                _writer.WriteStartElement( "maml", "description", null );
                _writer.WriteElementString( "maml", "para", null, descriptionAttribute.Description );
                _writer.WriteEndElement(); // </maml:description>
            }
        }

        void GenerateCommandSyntax( Type cmdletType, CmdletAttribute cmdletAttribute, CmdletParametersInfo parametersInfo )
        {
            _writer.WriteStartElement( "command", "syntax", null );
            foreach ( string parameterSetName in parametersInfo.ParametersByParameterSet.Keys )
            {
                _writer.WriteStartElement( "command", "syntaxItem", null );
                _writer.WriteElementString( "maml", "name", null, string.Format( "{0}-{1}", cmdletAttribute.VerbName, cmdletAttribute.NounName ) );

                foreach ( CmdletParameterInfo parameterInfo in parametersInfo.ParametersByParameterSet[ parameterSetName ] )
                    GenerateCommandSyntaxParameter( parameterSetName, parameterInfo );

                _writer.WriteEndElement(); // </command:syntaxItem>
            }
            _writer.WriteEndElement(); // </command:syntax>
        }

        void GenerateCommandSyntaxParameter( string parameterSetName, CmdletParameterInfo parameterInfo )
        {
            int parameterSetIndex = parameterInfo.GetParameterSetIndex( parameterSetName );

            _writer.WriteStartElement( "command", "parameter", null );
            _writer.WriteAttributeString( "required", parameterInfo.Mandatory( parameterSetIndex ).ToString() );
            _writer.WriteAttributeString( "globbing", parameterInfo.Globbing.ToString() );

            string pipelineInput = GetPipelineInputAttributeString( parameterInfo, parameterSetIndex );
            _writer.WriteAttributeString( "pipelineInput", pipelineInput );

            string position = GetPositionAttributeString( parameterInfo, parameterSetIndex );
            _writer.WriteAttributeString( "position", position );

            _writer.WriteElementString( "maml", "name", null, parameterInfo.ParameterName );

            _writer.WriteStartElement( "maml", "description", null );
            _writer.WriteElementString( "maml", "para", null, parameterInfo.ParameterAttributes[ parameterSetIndex ].HelpMessage );
            _writer.WriteEndElement(); // </maml:description>

            _writer.WriteStartElement( "command", "parameterValue", null );
            
            _writer.WriteAttributeString( "required", ( parameterInfo.ParameterType == typeof( SwitchParameter ) ).ToString() );
            _writer.WriteAttributeString( "variableLength", ( typeof( IEnumerable ).IsAssignableFrom( parameterInfo.ParameterType ) ).ToString() );
            _writer.WriteString( parameterInfo.ParameterType.ToString() );

            _writer.WriteEndElement(); // </command:parameterValue>
            
            _writer.WriteEndElement(); // </command:parameter>
        }

        void GenerateCommandParameters( Type cmdletType, CmdletAttribute cmdletAttribute, CmdletParametersInfo parametersInfo )
        {
            _writer.WriteStartElement( "command", "parameters", null );

            foreach ( CmdletParameterInfo parameterInfo in parametersInfo.Parameters )
                GenerateCommandParameter( cmdletType, cmdletAttribute, parameterInfo );

            _writer.WriteEndElement(); // </command:parameters>
        }

        void GenerateCommandParameter( Type cmdletType, CmdletAttribute cmdletAttribute, CmdletParameterInfo parameterInfo )
        {
            _writer.WriteStartElement( "command", "parameter", null );
            _writer.WriteAttributeString( "required", parameterInfo.Mandatory( 0 ).ToString() );
            _writer.WriteAttributeString( "variableLength", ( typeof( IEnumerable ).IsAssignableFrom( parameterInfo.ParameterType ) ).ToString() );
            _writer.WriteAttributeString( "globbing", parameterInfo.Globbing.ToString() );

            string pipelineInput = GetPipelineInputAttributeString( parameterInfo, 0 );
            _writer.WriteAttributeString( "pipelineInput", pipelineInput );

            string position = GetPositionAttributeString( parameterInfo, 0 );
            _writer.WriteAttributeString( "position", position );

            _writer.WriteElementString( "maml", "name", null, parameterInfo.ParameterName );

            _writer.WriteStartElement( "maml", "description", null );
            
            _writer.WriteElementString( "maml", "para", null, parameterInfo.ParameterAttributes[ 0 ].HelpMessage );
            
            _writer.WriteEndElement(); // </maml:description>

            _writer.WriteStartElement( "command", "parameterValue", null );
            
            _writer.WriteAttributeString( "required", ( parameterInfo.ParameterType == typeof( SwitchParameter ) ).ToString() );
            _writer.WriteAttributeString( "variableLength", ( typeof( IEnumerable ).IsAssignableFrom( parameterInfo.ParameterType ) ).ToString() );
            _writer.WriteString( parameterInfo.ParameterType.ToString() );

            _writer.WriteEndElement(); // </command:parameterValue>

            _writer.WriteStartElement( "dev", "type", null );
            _writer.WriteElementString( "maml", "name", null, parameterInfo.ParameterType.ToString() );
            _writer.WriteEndElement(); // </dev:type>

            object defaultValue = parameterInfo.DefaultValue;
            _writer.WriteElementString( "dev", "defaultValue", null, ( defaultValue == null ) ? "Null" : defaultValue.ToString() );

            _writer.WriteEndElement(); // </command:parameter>
        }

        string GetPositionAttributeString( CmdletParameterInfo parameterInfo, int parameterSetIndex )
        {
            string position = "named";
            if ( parameterInfo.Position( parameterSetIndex ) >= 0 )
                position = parameterInfo.Position( parameterSetIndex ).ToString();
            return position;
        }

        string GetPipelineInputAttributeString( CmdletParameterInfo parameterInfo, int parameterSetIndex )
        {
            string pipelineInput = "false";
            if ( parameterInfo.ValueFromPipeline( parameterSetIndex ) )
            {
                pipelineInput = "true (ByValue)";
                if ( parameterInfo.ValueFromPipelineByPropertyName( parameterSetIndex ) )
                {
                    pipelineInput = "true (ByValue, ByPropertyName)";
                }
            }
            else if ( parameterInfo.ValueFromPipelineByPropertyName( parameterSetIndex ) )
            {
                pipelineInput = "true (ByPropertyName)";
            }
            return pipelineInput;
        }

        #endregion
    }
}
