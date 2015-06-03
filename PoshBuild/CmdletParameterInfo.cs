using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation;
using Mono.Cecil;

namespace PoshBuild
{
    sealed class CmdletParameterInfo
    {
        public const int NonSpecificParameterSetIndex = -1;

        IDocSource _docSource;
        public PropertyDefinition PropertyDefinition { get; private set; }
        public string ParameterName { get; private set; }
        public TypeDefinition ParameterType { get; private set; }
        public IList<ParameterAttribute> ParameterAttributes { get; private set; }
        public DefaultValueAttribute DefaultValueAttribute { get; private set; }
        public AliasAttribute AliasAttribute { get; private set; }

        public int ParameterSetCount { get { return ParameterAttributes.Count; } }
        public object DefaultValue { get { return DefaultValueAttribute != null ? DefaultValueAttribute.Value : string.Empty; } }

        public CmdletParameterInfo( PropertyDefinition property, IDocSource docSource)
        {
            if ( docSource == null )
                throw new ArgumentNullException( "docSource" );

            if ( property == null )
                throw new ArgumentNullException( "propertyInfo" );

            _docSource = docSource;
            
            PropertyDefinition = property;
            ParameterName = property.Name;
            ParameterType = property.PropertyType.Resolve();

            ParameterAttributes = property.GetRealCustomAttributesOfType<ParameterAttribute>( CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ).ToList();
            
            if ( ParameterAttributes.Count == 0 )
                throw new ArgumentException( "The specified property doesn't have a ParameterAttribute attribute." );

            DefaultValueAttribute = property.GetRealCustomAttributesOfType<DefaultValueAttribute>( CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ).FirstOrDefault();
            AliasAttribute = property.GetRealCustomAttributesOfType<AliasAttribute>( CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ).FirstOrDefault();
        }

        public int GetParameterSetIndex( string parameterSetName )
        {
            int allSetsIndex = -1;
            int index = 0;
            foreach ( var parameterAttribute in ParameterAttributes )
            {
                if ( parameterAttribute.ParameterSetName == parameterSetName )
                    return index;

                if ( parameterAttribute.ParameterSetName == ParameterAttribute.AllParameterSets )
                    allSetsIndex = index;

                index++;
            }

            if ( allSetsIndex != -1 )
                return allSetsIndex;

            throw new KeyNotFoundException( "The parameter set name was specified for this parameter." );
        }

        public bool Globbing( int parameterSetIndex )
        {
            if ( parameterSetIndex == NonSpecificParameterSetIndex )
                return Enumerable.Range( 0, ParameterAttributes.Count ).Any( i => Globbing( i ) );

            bool supportsGlobbing = false;
            if ( _docSource.TryGetPropertySupportsGlobbing( PropertyDefinition, ParameterAttributes[ parameterSetIndex ].ParameterSetName, out supportsGlobbing ) )
                return supportsGlobbing;
            else
                return false;
        }

        public bool Mandatory( int parameterSetIndex )
        {
            if ( parameterSetIndex == NonSpecificParameterSetIndex )
                return Enumerable.Range( 0, ParameterAttributes.Count ).Any( i => Mandatory( i ) );

            return ParameterAttributes[ parameterSetIndex ].Mandatory;
        }

        public int Position( int parameterSetIndex )
        {
            if ( parameterSetIndex == NonSpecificParameterSetIndex )
                return Enumerable.Range( 0, ParameterAttributes.Count ).Min( i => Position( i ) );

            return ParameterAttributes[ parameterSetIndex ].Position;
        }

        public bool ValueFromPipeline( int parameterSetIndex )
        {
            if ( parameterSetIndex == NonSpecificParameterSetIndex )
                return Enumerable.Range( 0, ParameterAttributes.Count ).Any( i => ValueFromPipeline( i ) );

            return ParameterAttributes[ parameterSetIndex ].ValueFromPipeline;
        }

        public bool ValueFromPipelineByPropertyName( int parameterSetIndex )
        {
            if ( parameterSetIndex == NonSpecificParameterSetIndex )
                return Enumerable.Range( 0, ParameterAttributes.Count ).Any( i => ValueFromPipelineByPropertyName( i ) );

            return ParameterAttributes[ parameterSetIndex ].ValueFromPipelineByPropertyName;
        }

        public bool ValueFromRemaingArguments( int parameterSetIndex )
        {
            if ( parameterSetIndex == NonSpecificParameterSetIndex )
                return Enumerable.Range( 0, ParameterAttributes.Count ).Any( i => ValueFromRemaingArguments( i ) );

            return ParameterAttributes[ parameterSetIndex ].ValueFromRemainingArguments;
        }

        public string[] GetParameterSetNames()
        {
            if ( ParameterAttributes.Count == 1 &&
                string.IsNullOrEmpty( ParameterAttributes[ 0 ].ParameterSetName ) )
                return new string[ 1 ] { ParameterAttribute.AllParameterSets };

            List<string> parameterSets = new List<string>();
            foreach ( var parameterAttribute in ParameterAttributes )
            {
                parameterSets.Add( parameterAttribute.ParameterSetName );
            }
            return parameterSets.ToArray();
        }
    }
}
