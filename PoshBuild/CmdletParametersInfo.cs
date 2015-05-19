using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Reflection;

namespace PoshBuild
{
    sealed class CmdletParametersInfo
    {
        public IList<CmdletParameterInfo> Parameters { get; private set; }
        public IDictionary<string, IList<CmdletParameterInfo>> ParametersByParameterSet { get; private set; }

        public CmdletParametersInfo( Type cmdletType, IDocSource docSource )
        {
            if ( cmdletType == null )
                throw new ArgumentNullException( "cmdletType" );

            Parameters = new List<CmdletParameterInfo>();
            foreach ( PropertyInfo propertyInfo in cmdletType.GetProperties() )
            {
                if ( Attribute.IsDefined( propertyInfo, typeof( ParameterAttribute ) ) )
                {
                    Parameters.Add( new CmdletParameterInfo( propertyInfo, docSource ) );
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
