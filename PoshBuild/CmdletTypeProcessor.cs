﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation;
using System.Xml;
using PoshBuild.ComponentModel;

namespace PoshBuild
{
    /// <summary>
    /// Generates MAML for a Cmdlet type.
    /// </summary>
    sealed class CmdletTypeProcessor
    {
        XmlWriter _writer;
        Type _type;
        ICmdletHelpDescriptor _descriptor;
        CmdletAttribute _cmdletAttribute;
        CmdletParametersInfo _parametersInfo;

        public CmdletTypeProcessor( XmlWriter writer, Type type, ICmdletHelpDescriptor descriptor )
        {
            if ( type == null )
                throw new ArgumentNullException( "type" );

            if ( !type.IsSubclassOf( typeof( Cmdlet ) ) )
                throw new ArgumentException( "The specified Cmdlet type does not inherit from Cmdlet.", "type" );

            _cmdletAttribute = ( CmdletAttribute ) Attribute.GetCustomAttribute( type, typeof( CmdletAttribute ) );

            if ( _cmdletAttribute == null )
                throw new ArgumentException( "The specified Cmdlet type does not have the CmdletAttribute attribute." );

            _type = type;
            _writer = writer;
            _descriptor = descriptor;
            _parametersInfo = new CmdletParametersInfo( this, type, descriptor );
        }

        public void GenerateHelpFileEntry()
        {
            _writer.WriteStartElement( "command", "command", null );

            GenerateCommandDetails();
            GenerateCommandDescription();
            GenerateCommandSyntax();
            GenerateCommandParameters();
            GenerateCommandReturnValues();
            
            _writer.WriteEndElement(); // </command:command>
        }
        
        void GenerateCommandDetails()
        {
            _writer.WriteStartElement( "command", "details", null );

            _writer.WriteElementString( "command", "name", null, string.Format( "{0}-{1}", _cmdletAttribute.VerbName, _cmdletAttribute.NounName ) );
            _writer.WriteElementString( "command", "verb", null, _cmdletAttribute.VerbName.ToLowerInvariant() );
            _writer.WriteElementString( "command", "noun", null, _cmdletAttribute.NounName.ToLowerInvariant() );

            SynopsisAttribute synopsisAttribute = null;
            
            if ( _descriptor != null )
                synopsisAttribute = _descriptor.GetSynopsis();

            if ( synopsisAttribute == null )
                synopsisAttribute = ( SynopsisAttribute ) Attribute.GetCustomAttribute( _type, typeof( SynopsisAttribute ) );

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

        void GenerateCommandReturnValues()
        {
            var attributes = _type.GetCustomAttributes( typeof( OutputTypeAttribute ), true ).OfType<OutputTypeAttribute>().ToList();

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

                        _writer.WriteElementString( "maml", "name", null, type.Type == null ? type.Name : type.Type.GetPSPrettyName() );
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

        void GenerateCommandDescription()
        {
            DescriptionAttribute descriptionAttribute = ( DescriptionAttribute ) Attribute.GetCustomAttribute( _type, typeof( DescriptionAttribute ) );
            if ( descriptionAttribute != null )
            {
                _writer.WriteStartElement( "maml", "description", null );
                _writer.WriteElementString( "maml", "para", null, descriptionAttribute.Description );
                _writer.WriteEndElement(); // </maml:description>
            }
        }

        void GenerateCommandSyntax()
        {
            _writer.WriteStartElement( "command", "syntax", null );
            foreach ( string parameterSetName in _parametersInfo.ParametersByParameterSet.Keys )
            {
                _writer.WriteStartElement( "command", "syntaxItem", null );
                _writer.WriteElementString( "maml", "name", null, string.Format( "{0}-{1}", _cmdletAttribute.VerbName, _cmdletAttribute.NounName ) );

                foreach ( CmdletParameterInfo parameterInfo in _parametersInfo.ParametersByParameterSet[ parameterSetName ] )
                    new CmdletParameterProcessor( _writer, parameterInfo ).GenerateCommandSyntaxParameter( parameterSetName );

                _writer.WriteEndElement(); // </command:syntaxItem>
            }
            _writer.WriteEndElement(); // </command:syntax>
        }


        void GenerateCommandParameters()
        {
            _writer.WriteStartElement( "command", "parameters", null );

            foreach ( CmdletParameterInfo parameterInfo in _parametersInfo.Parameters )
                new CmdletParameterProcessor( _writer, parameterInfo ).GenerateCommandParameter();

            _writer.WriteEndElement(); // </command:parameters>
        }
    }
}