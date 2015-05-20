using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace PoshBuild
{
    /// <summary>
    /// Holds a sequence of other <c>IDocSource</c> instances, tries each in turn.
    /// </summary>
    sealed class FallthroughDocSource : IDocSource
    {
        IEnumerable<IDocSource> _sources;

        public FallthroughDocSource( IEnumerable<IDocSource> sources )
        {
            if ( sources == null )
                throw new ArgumentNullException( "sources" );

            if ( sources.Any( v => v == null ) )
                throw new ArgumentNullException( "sources" );

            _sources = sources;
        }

        public bool WriteCmdletSynopsis( XmlWriter writer, Type cmdlet )
        {
            foreach ( var source in _sources )
            {
                if ( source.WriteCmdletSynopsis( writer, cmdlet ) )
                    return true;
            }
            return false;
        }

        public bool WriteCmdletDescription( XmlWriter writer, Type cmdlet )
        {
            foreach ( var source in _sources )
            {
                if ( source.WriteCmdletDescription( writer, cmdlet ) )
                    return true;
            }
            return false;
        }

        public bool TryGetPropertySupportsGlobbing( PropertyInfo property, string parameterSetName, out bool supportsGlobbing )
        {
            supportsGlobbing = default( bool );

            foreach ( var source in _sources )
            {
                if ( source.TryGetPropertySupportsGlobbing( property, parameterSetName, out supportsGlobbing ) )
                    return true;
            }

            return false;
        }

        public bool WriteParameterDescription( XmlWriter writer, PropertyInfo property, string parameterSetName )
        {
            foreach ( var source in _sources )
            {
                if ( source.WriteParameterDescription( writer, property, parameterSetName ) )
                    return true;
            }
            return false;
        }

        public bool WriteReturnValueDescription( XmlWriter writer, Type cmdlet, string outputTypeName )
        {
            foreach ( var source in _sources )
            {
                if ( source.WriteReturnValueDescription( writer, cmdlet, outputTypeName ) )
                    return true;
            }
            return false;
        }

        public bool WriteCmdletExamples( XmlWriter writer, Type cmdlet )
        {
            foreach ( var source in _sources )
            {
                if ( source.WriteCmdletExamples( writer, cmdlet ) )
                    return true;
            }
            return false;
        }


        public bool WriteInputTypeDescription( XmlWriter writer, Type cmdlet, string inputTypeName )
        {
            foreach ( var source in _sources )
            {
                if ( source.WriteInputTypeDescription( writer, cmdlet, inputTypeName ) )
                    return true;
            }
            return false;
        }
    }
}
