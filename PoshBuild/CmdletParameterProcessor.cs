using System;
using System.Collections;
using System.Management.Automation;
using System.Xml;

namespace PoshBuild
{
    sealed class CmdletParameterProcessor
    {
        XmlWriter _writer;
        CmdletParameterInfo _parameter;

        public CmdletParameterProcessor( XmlWriter writer, CmdletParameterInfo parameter )
        {
            if ( writer == null )
                throw new ArgumentNullException( "writer" );

            if ( parameter == null )
                throw new ArgumentNullException( "parameter" );

            _writer = writer;
            _parameter = parameter;
        }

        public void GenerateCommandSyntaxParameter( string parameterSetName )
        {
            int parameterSetIndex = _parameter.GetParameterSetIndex( parameterSetName );

            _writer.WriteStartElement( "command", "parameter", null );
            _writer.WriteAttributeString( "required", _parameter.Mandatory( parameterSetIndex ).ToString() );
            _writer.WriteAttributeString( "globbing", _parameter.Globbing.ToString() );

            string pipelineInput = GetPipelineInputAttributeString( parameterSetIndex );
            _writer.WriteAttributeString( "pipelineInput", pipelineInput );

            string position = GetPositionAttributeString( parameterSetIndex );
            _writer.WriteAttributeString( "position", position );

            _writer.WriteElementString( "maml", "name", null, _parameter.ParameterName );

            _writer.WriteStartElement( "maml", "description", null );
            _writer.WriteElementString( "maml", "para", null, _parameter.ParameterAttributes[ parameterSetIndex ].HelpMessage );
            _writer.WriteEndElement(); // </maml:description>

            _writer.WriteStartElement( "command", "parameterValue", null );

            _writer.WriteAttributeString( "required", ( _parameter.ParameterType == typeof( SwitchParameter ) ).ToString() );
            _writer.WriteAttributeString( "variableLength", ( typeof( IEnumerable ).IsAssignableFrom( _parameter.ParameterType ) ).ToString() );
            _writer.WriteString( _parameter.ParameterType.GetPSPrettyName() );

            _writer.WriteEndElement(); // </command:parameterValue>

            _writer.WriteEndElement(); // </command:parameter>
        }

        public void GenerateCommandParameter()
        {
            _writer.WriteStartElement( "command", "parameter", null );
            _writer.WriteAttributeString( "required", _parameter.Mandatory( 0 ).ToString() );
            _writer.WriteAttributeString( "variableLength", ( typeof( IEnumerable ).IsAssignableFrom( _parameter.ParameterType ) ).ToString() );
            _writer.WriteAttributeString( "globbing", _parameter.Globbing.ToString() );

            string pipelineInput = GetPipelineInputAttributeString( 0 );
            _writer.WriteAttributeString( "pipelineInput", pipelineInput );

            string position = GetPositionAttributeString( 0 );
            _writer.WriteAttributeString( "position", position );

            _writer.WriteElementString( "maml", "name", null, _parameter.ParameterName );

            _writer.WriteStartElement( "maml", "description", null );

            _writer.WriteElementString( "maml", "para", null, _parameter.ParameterAttributes[ 0 ].HelpMessage );

            _writer.WriteEndElement(); // </maml:description>

            _writer.WriteStartElement( "command", "parameterValue", null );

            _writer.WriteAttributeString( "required", ( _parameter.ParameterType == typeof( SwitchParameter ) ).ToString() );
            _writer.WriteAttributeString( "variableLength", ( typeof( IEnumerable ).IsAssignableFrom( _parameter.ParameterType ) ).ToString() );
            _writer.WriteString( _parameter.ParameterType.GetPSPrettyName() );

            _writer.WriteEndElement(); // </command:parameterValue>

            _writer.WriteStartElement( "dev", "type", null );
            _writer.WriteElementString( "maml", "name", null, _parameter.ParameterType.GetPSPrettyName() );
            _writer.WriteEndElement(); // </dev:type>

            object defaultValue = _parameter.DefaultValue;
            _writer.WriteElementString( "dev", "defaultValue", null, ( defaultValue == null ) ? "Null" : defaultValue.ToString() );

            _writer.WriteEndElement(); // </command:parameter>
        }

        string GetPositionAttributeString( int parameterSetIndex )
        {
            string position = "named";
            if ( _parameter.Position( parameterSetIndex ) >= 0 )
                position = _parameter.Position( parameterSetIndex ).ToString();
            return position;
        }

        string GetPipelineInputAttributeString( int parameterSetIndex )
        {
            string pipelineInput = "false";
            if ( _parameter.ValueFromPipeline( parameterSetIndex ) )
            {
                pipelineInput = "true (ByValue)";
                if ( _parameter.ValueFromPipelineByPropertyName( parameterSetIndex ) )
                {
                    pipelineInput = "true (ByValue, ByPropertyName)";
                }
            }
            else if ( _parameter.ValueFromPipelineByPropertyName( parameterSetIndex ) )
            {
                pipelineInput = "true (ByPropertyName)";
            }
            return pipelineInput;
        }
    }
}
