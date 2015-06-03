using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Mono.Cecil;

namespace PoshBuild
{
    sealed class CmdletParametersInfo
    {
        public IList<CmdletParameterInfo> Parameters { get; private set; }
        public IDictionary<string, IList<CmdletParameterInfo>> ParametersByParameterSet { get; private set; }

        public CmdletParametersInfo( TypeDefinition cmdletType, IDocSource docSource )
        {
            if ( cmdletType == null )
                throw new ArgumentNullException( "cmdletType" );

            var tParameterAttribute = cmdletType.Module.Import( typeof( ParameterAttribute ) );

            Parameters = new List<CmdletParameterInfo>();

            foreach ( PropertyDefinition property in cmdletType.Properties )
            {
                if ( property.GetMethod.IsPublic && property.SetMethod.IsPublic )
                {
                    if ( property.CustomAttributes.Any( ca => ca.AttributeType.IsSame( tParameterAttribute, CecilExtensions.TypeComparisonFlags.MatchAllExceptVersion ) ) )
                    {
                        Parameters.Add( new CmdletParameterInfo( property, docSource ) );
                    }
                }
            }

            ParametersByParameterSet = new Dictionary<string, IList<CmdletParameterInfo>>();
            List<CmdletParameterInfo> parametersInAllSets = new List<CmdletParameterInfo>();
            foreach ( var parameterInfo in Parameters )
            {
                foreach ( string parameterSetName in parameterInfo.GetParameterSetNames() )
                {
                    if ( parameterSetName == ParameterAttribute.AllParameterSets )
                    {
                        parametersInAllSets.Add( parameterInfo );
                    }
                    else
                    {
                        if ( !ParametersByParameterSet.ContainsKey( parameterSetName ) )
                            ParametersByParameterSet.Add( parameterSetName, new List<CmdletParameterInfo>() );

                        ParametersByParameterSet[ parameterSetName ].Add( parameterInfo );
                    }
                }
            }
            foreach ( var parameterInfo in parametersInAllSets )
            {
                foreach ( IList<CmdletParameterInfo> parameterInfos in ParametersByParameterSet.Values )
                {
                    parameterInfos.Add( parameterInfo );
                }
            }
            if ( ParametersByParameterSet.Count == 0 )
            {
                ParametersByParameterSet.Add( ParameterAttribute.AllParameterSets, new List<CmdletParameterInfo>() );
                foreach ( var parameterInfo in parametersInAllSets )
                {
                    ParametersByParameterSet[ ParameterAttribute.AllParameterSets ].Add( parameterInfo );
                }
            }
        }
    }
}
