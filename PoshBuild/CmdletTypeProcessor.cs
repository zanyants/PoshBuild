using System;
using System.Linq;
using System.Management.Automation;
using System.Xml;

namespace PoshBuild
{
    /// <summary>
    /// Generates MAML for a Cmdlet type.
    /// </summary>
    sealed class CmdletTypeProcessor
    {
        XmlWriter _writer;
        Type _type;
        IDocSource _docSource;
        CmdletAttribute _cmdletAttribute;
        CmdletParametersInfo _parametersInfo;

        public CmdletTypeProcessor( XmlWriter writer, Type type, IDocSource docSource )
        {
            if ( type == null )
                throw new ArgumentNullException( "type" );

            if ( !type.IsSubclassOf( typeof( Cmdlet ) ) )
                throw new ArgumentException( "The specified Cmdlet type does not inherit from Cmdlet.", "type" );

            _cmdletAttribute = ( CmdletAttribute ) Attribute.GetCustomAttribute( type, typeof( CmdletAttribute ) );

            if ( _cmdletAttribute == null )
                throw new ArgumentException( "The specified Cmdlet type does not have the CmdletAttribute attribute." );

            if ( docSource == null )
                throw new ArgumentNullException( "docSource" );

            _type = type;
            _writer = writer;
            _docSource = docSource;
            _parametersInfo = new CmdletParametersInfo( type, docSource );
        }

        public void GenerateHelpFileEntry()
        {
            _writer.WriteStartElement( "command", "command", null );

            GenerateCommandDetails();
            GenerateCommandDescription();
            GenerateCommandSyntax();
            GenerateCommandParameters();
            GenerateCommandInputTypes();
            GenerateCommandReturnValues();
            GenerateCommandNotes();
            GenerateCommandExamples();
            GenerateCommandRelatedLinks();

            _writer.WriteEndElement(); // </command:command>
        }
        
        void GenerateCommandDetails()
        {
            _writer.WriteStartElement( "command", "details", null );

            _writer.WriteElementString( "command", "name", null, string.Format( "{0}-{1}", _cmdletAttribute.VerbName, _cmdletAttribute.NounName ) );
            _writer.WriteElementString( "command", "verb", null, _cmdletAttribute.VerbName );
            _writer.WriteElementString( "command", "noun", null, _cmdletAttribute.NounName );
            
            _writer.WriteStartElement( "maml", "description", null );
            _docSource.WriteCmdletSynopsis( _writer, _type );
            _writer.WriteEndElement(); // </maml:description>

            _writer.WriteEndElement(); // </command:details>
        }

        void GenerateCommandInputTypes()
        {
            var pipeableParameters = 
                _parametersInfo
                .Parameters
                .Where( cpi => cpi.ParameterAttributes.Any( pa => pa.ValueFromPipeline || pa.ValueFromPipelineByPropertyName ) );

            var byType =
                pipeableParameters
                .GroupBy( cpi => cpi.ParameterType.HasElementType ? cpi.ParameterType.GetElementType() : cpi.ParameterType )
                .Select( group => new { Group = group, PrettyName = TypeNameHelper.GetPSPrettyName( group.Key ) } )
                .OrderBy( item => item.PrettyName )
                .ToList();

            _writer.WriteStartElement( "command", "inputTypes", null );

            if ( byType.Count > 0 )
            {
                foreach ( var item in byType )
                {
                    _writer.WriteStartElement( "command", "inputType", null );

                    _writer.WriteStartElement( "dev", "type", null );

                    _writer.WriteElementString( "maml", "name", null, item.PrettyName );
                    _writer.WriteElementString( "maml", "uri", null, string.Empty );
                    // This description element is *not* read by Get-Help.
                    _writer.WriteElementString( "maml", "description", null, string.Empty );

                    _writer.WriteEndElement(); // </dev:type>

                    // This description element *is* read by Get-Help.
                    _writer.WriteStartElement( "maml", "description", null );

                    if ( !_docSource.WriteInputTypeDescription( _writer, _type, item.Group.Key.FullName ) )
                    {
                        // If no user-supplied documentation, auto-generate a default description.
                        var parameterNames =
                            item
                            .Group
                            .Select( cpi => cpi.ParameterName )
                            .OrderBy( s => s, StringComparer.InvariantCultureIgnoreCase )
                            .ToList();

                        var text = string.Format( 
                            "You can pipe values for the {0} parameter{3} to the {1}-{2} cmdlet.", 
                            parameterNames.JoinWithAnd(), 
                            _cmdletAttribute.VerbName, 
                            _cmdletAttribute.NounName,
                            parameterNames.Count == 1 ? null : "s" );

                        _writer.WriteElementString( "maml", "para", null, text );
                    }

                    _writer.WriteEndElement(); // </maml:description>

                    _writer.WriteEndElement(); // </command:inputType>
                }
            }
            else
            {
                _writer.WriteStartElement( "command", "inputType", null );

                _writer.WriteStartElement( "dev", "type", null );

                _writer.WriteElementString( "maml", "name", null, "None" );
                _writer.WriteElementString( "maml", "uri", null, string.Empty );
                _writer.WriteElementString( "maml", "description", null, string.Empty );

                _writer.WriteEndElement(); // </dev:type>

                // This description element *is* read by Get-Help.
                _writer.WriteStartElement( "maml", "description", null );

                _writer.WriteElementString( "maml", "para", null, "You cannot pipe objects to this cmdlet." );

                _writer.WriteEndElement(); // </maml:description>

                _writer.WriteEndElement(); // </command:inputType>
            }

            _writer.WriteEndElement(); // </command:inputTypes>
        }

        void GenerateCommandReturnValues()
        {
            _writer.WriteStartElement( "command", "returnValues", null );

            var attributes = _type.GetCustomAttributes( typeof( OutputTypeAttribute ), true ).OfType<OutputTypeAttribute>().ToList();

            if ( attributes.Count > 0 )
            {
                foreach ( var attr in attributes )
                {
                    var parameterSetNames = attr.ParameterSetName == null ? null : attr.ParameterSetName.Where( s => !string.IsNullOrWhiteSpace( s ) && s != ParameterAttribute.AllParameterSets ).ToList();

                    foreach ( var type in attr.Type )
                    {
                        _writer.WriteStartElement( "command", "returnValue", null );

                        _writer.WriteStartElement( "dev", "type", null );

                        _writer.WriteElementString( "maml", "name", null, type.Type == null ? type.Name : type.Type.GetPSPrettyName() );
                        _writer.WriteElementString( "maml", "uri", null, string.Empty );
                        // This description element is *not* read by Get-Help.
                        _writer.WriteElementString( "maml", "description", null, string.Empty );

                        _writer.WriteEndElement(); // </dev:type>

                        // This description element *is* read by Get-Help.
                        _writer.WriteStartElement( "maml", "description", null );

                        if ( !_docSource.WriteReturnValueDescription( _writer, _type, type.Type == null ? type.Name : type.Type.FullName ) )
                        {
                            // If no user-supplied documentation, auto-generate a default description.

                            if ( !string.IsNullOrWhiteSpace( attr.ProviderCmdlet ) )
                                _writer.WriteElementString( "maml", "para", null, string.Format( "Applies to Provider Cmdlet '{0}'.", attr.ProviderCmdlet ) );

                            if ( parameterSetNames != null && parameterSetNames.Count > 0 )
                            {
                                _writer.WriteElementString( "maml", "para", null, "Applies to parameter sets:" );

                                foreach ( var name in parameterSetNames )
                                    _writer.WriteElementString( "maml", "para", null, "-- " + name );
                            }
                        }
                        _writer.WriteEndElement(); // </maml:description>

                        _writer.WriteEndElement(); // </command:returnValue>
                    }

                }
            }
            else
            {
                _writer.WriteStartElement( "command", "returnValue", null );

                _writer.WriteStartElement( "dev", "type", null );

                _writer.WriteElementString( "maml", "name", null, "None" );
                _writer.WriteElementString( "maml", "uri", null, string.Empty );
                // This description element is *not* read by Get-Help.
                _writer.WriteElementString( "maml", "description", null, string.Empty );

                _writer.WriteEndElement(); // </dev:type>

                // This description element *is* read by Get-Help.
                _writer.WriteStartElement( "maml", "description", null );

                _writer.WriteElementString( "maml", "para", null, "This cmdlet does not generate any output." );
                
                _writer.WriteEndElement(); // </maml:description>

                _writer.WriteEndElement(); // </command:returnValue>                
            }

            _writer.WriteEndElement(); // </command:returnValues>
        }

        void GenerateCommandDescription()
        {
            _writer.WriteStartElement( "maml", "description", null );
            _docSource.WriteCmdletDescription( _writer, _type );
            _writer.WriteEndElement(); // </maml:description>
        }

        void GenerateCommandSyntax()
        {
            _writer.WriteStartElement( "command", "syntax", null );
            foreach ( string parameterSetName in _parametersInfo.ParametersByParameterSet.Keys )
            {
                _writer.WriteStartElement( "command", "syntaxItem", null );
                _writer.WriteComment( string.Format( " ParameterSetName '{0}' ", parameterSetName ) );
                _writer.WriteElementString( "maml", "name", null, string.Format( "{0}-{1}", _cmdletAttribute.VerbName, _cmdletAttribute.NounName ) );

                foreach ( CmdletParameterInfo parameterInfo in _parametersInfo.ParametersByParameterSet[ parameterSetName ] )
                    new CmdletParameterProcessor( _writer, parameterInfo, _docSource ).GenerateCommandSyntaxParameter( parameterSetName );

                _writer.WriteEndElement(); // </command:syntaxItem>
            }
            _writer.WriteEndElement(); // </command:syntax>
        }


        void GenerateCommandParameters()
        {
            _writer.WriteStartElement( "command", "parameters", null );

            foreach ( CmdletParameterInfo parameterInfo in _parametersInfo.Parameters )
                new CmdletParameterProcessor( _writer, parameterInfo, _docSource ).GenerateCommandParameter();

            _writer.WriteEndElement(); // </command:parameters>
        }

        void GenerateCommandExamples()
        {
            _writer.WriteStartElement( "command", "examples", null );
            _docSource.WriteCmdletExamples( _writer, _type );
            _writer.WriteEndElement(); // </command:examples>
        }

        void GenerateCommandNotes()
        {
            _writer.WriteStartElement( "maml", "alertSet", null );
            _docSource.WriteCmdletNotes( _writer, _type );
            _writer.WriteEndElement(); // </maml:alertSet>
        }

        void GenerateCommandRelatedLinks()
        {
            _writer.WriteStartElement( "maml", "relatedLinks", null );
            _docSource.WriteCmdletRelatedLinks( _writer, _type );
            _writer.WriteEndElement(); // </maml:alertSet>
        }
    
    }
}
