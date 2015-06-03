using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;
using System.Xml;

namespace PoshBuild
{
    sealed class CmdletParameterProcessor
    {
        XmlWriter _writer;
        CmdletParameterInfo _parameter;
        IDocSource _docSource;
        
        public CmdletParameterProcessor( XmlWriter writer, CmdletParameterInfo parameter, IDocSource docSource )
        {
            if ( writer == null )
                throw new ArgumentNullException( "writer" );

            if ( parameter == null )
                throw new ArgumentNullException( "parameter" );

            if ( docSource == null )
                throw new ArgumentNullException( "docSource" );

            _writer = writer;
            _parameter = parameter;
            _docSource = docSource;
        }

        public void GenerateCommandSyntaxParameter( string parameterSetName )
        {
            GenerateParameter( _parameter.GetParameterSetIndex( parameterSetName ) );
        }

        public void GenerateCommandParameter()
        {
            GenerateParameter( CmdletParameterInfo.NonSpecificParameterSetIndex );
        }
        
        void GenerateParameter( int parameterSetIndex )
        {
            _writer.WriteStartElement( "command", "parameter", null );
            _writer.WriteAttributeString( "required", _parameter.Mandatory( parameterSetIndex ).ToStringLower() );
            _writer.WriteAttributeString( "globbing", _parameter.Globbing( parameterSetIndex ).ToStringLower() );

            string pipelineInput = GetPipelineInputAttributeString( parameterSetIndex );
            _writer.WriteAttributeString( "pipelineInput", pipelineInput );

            string position = GetPositionAttributeString( parameterSetIndex );
            _writer.WriteAttributeString( "position", position );

            if ( _parameter.AliasAttribute != null )
            {
                var aliases = _parameter.AliasAttribute.AliasNames.Aggregate( ( w, n ) => w + "," + n );

                if ( !string.IsNullOrEmpty( aliases ) )
                    _writer.WriteAttributeString( "aliases", aliases );
            }

            _writer.WriteElementString( "maml", "name", null, _parameter.ParameterName );

            _writer.WriteStartElement( "maml", "description", null );

            _docSource.WriteParameterDescription( 
                _writer, 
                _parameter.PropertyDefinition, 
                parameterSetIndex == CmdletParameterInfo.NonSpecificParameterSetIndex ? 
                    null : 
                    _parameter.ParameterAttributes[ parameterSetIndex ].ParameterSetName );

            _writer.WriteEndElement(); // </maml:description>

            if ( _parameter.ParameterType.IsEnum )
            {
                var enumNames = _parameter.ParameterType.GetEnumNames().ToList();

                _writer.WriteStartElement( "command", "parameterValue", null );

                _writer.WriteAttributeString( "required", "true" );
                _writer.WriteAttributeString( "variableLength", "false" );
                _writer.WriteString( string.Join( " | ", enumNames ) );

                _writer.WriteEndElement(); // </command:parameterValue>

                _writer.WriteStartElement( "dev", "possibleValues", null );

                foreach ( var name in enumNames )
                {
                    _writer.WriteStartElement( "dev", "possibleValue", null );

                    _writer.WriteElementString( "dev", "value", null, name );

                    _writer.WriteStartElement( "maml", "description", null );
                    _writer.WriteElementString( "maml", "para", null, string.Empty );
                    _writer.WriteEndElement(); // </maml:description>

                    _writer.WriteEndElement(); // </dev:possibleValue>
                }

                _writer.WriteEndElement(); // </dev:possibleValues>
            }
            else
            {
                _writer.WriteStartElement( "command", "parameterValue", null );

                var tSwitchParameter = _parameter.PropertyDefinition.Module.Import( typeof( SwitchParameter ) );
                var tIEnumerable = _parameter.PropertyDefinition.Module.Import( typeof( IEnumerable ) );

                var isSwitchParameter = _parameter.ParameterType.IsSame( tSwitchParameter, CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion );
                var implementsIEnumerable = _parameter.ParameterType.Interfaces.Any( tr => tr.IsSame( tIEnumerable, CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ) );
                var isVariableLength = implementsIEnumerable || _parameter.ParameterType.IsArray;

                _writer.WriteAttributeString( "required", ( !isSwitchParameter ).ToStringLower() );
                _writer.WriteAttributeString( "variableLength", ( isVariableLength ).ToStringLower() );
                _writer.WriteString( _parameter.ParameterType.GetPSPrettyName() );

                _writer.WriteEndElement(); // </command:parameterValue>
            }

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
