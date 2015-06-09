using System.Collections.Generic;
using System.Xml;
using Mono.Cecil;

namespace PoshBuild
{
    /// <summary>
    /// An implementation of <see cref="IDocSource"/> that delgates to contained <see cref="IDocSource"/>
    /// instances based on a key derived from the <see cref="TypeDefintion"/> being processed.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    abstract class KeyedDocSource<TKey> : IDocSource
    {
        Dictionary<TKey, IDocSource> _sources;

        protected delegate IDocSource CreateDocSourceForKey( TypeDefinition type, TKey key );

        /// <summary>
        /// When implemented by a derived class, returns a key corresponding to the specified <see cref="TypeDefintion"/>.
        /// </summary>
        /// <param name="type">The type for which a key should be generated.</param>
        /// <param name="createDocSource">A function that creates a new <see cref="IDocSource"/> instance. This will be called if the returned key does not exist in the cache.</param>
        /// <returns>The key for the type.</returns>
        protected abstract TKey GetKey( TypeDefinition type, out CreateDocSourceForKey createDocSource );

        public KeyedDocSource()
        {
            _sources = new Dictionary<TKey, IDocSource>();
        }

        /// <summary>
        /// Explicitly add an entry to the cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="source"></param>
        protected void Add( TKey key, IDocSource source )
        {
            _sources.Add( key, source );
        }

        protected IDocSource GetSource( TypeDefinition type )
        {
            CreateDocSourceForKey create;
            IDocSource source;

            var key = GetKey( type, out create );

            if ( !_sources.TryGetValue( key, out source ) )
            {
                source = create( type, key );
                _sources.Add( key, source );
            }

            return source;
        }

        protected IDocSource GetSource( PropertyDefinition property )
        {
            CreateDocSourceForKey create;
            IDocSource source;

            var key = GetKey( property.DeclaringType, out create );

            if ( !_sources.TryGetValue( key, out source ) )
            {
                source = create( property.DeclaringType, key );
                _sources.Add( key, source );
            }

            return source;
        }

        virtual public bool WriteCmdletSynopsis( XmlWriter writer, TypeDefinition cmdlet )
        {
            var source = GetSource( cmdlet );
            return source == null ? false : source.WriteCmdletSynopsis( writer, cmdlet );
        }

        virtual public bool WriteCmdletDescription( XmlWriter writer, TypeDefinition cmdlet )
        {
            var source = GetSource( cmdlet );
            return source == null ? false : source.WriteCmdletDescription( writer, cmdlet );
        }

        virtual public bool WriteParameterDescription( XmlWriter writer, PropertyDefinition property, string parameterSetName, IEnumerable<TypeDefinition> descendantTypes )
        {
            var source = GetSource( property );
            return source == null ? false : source.WriteParameterDescription( writer, property, parameterSetName, descendantTypes );
        }

        virtual public bool WriteCmdletExamples( XmlWriter writer, TypeDefinition cmdlet )
        {
            var source = GetSource( cmdlet );
            return source == null ? false : source.WriteCmdletExamples( writer, cmdlet );
        }

        virtual public bool WriteReturnValueDescription( XmlWriter writer, TypeDefinition cmdlet, string outputTypeName )
        {
            var source = GetSource( cmdlet );
            return source == null ? false : source.WriteReturnValueDescription( writer, cmdlet, outputTypeName );
        }

        virtual public bool WriteInputTypeDescription( XmlWriter writer, TypeDefinition cmdlet, string inputTypeName )
        {
            var source = GetSource( cmdlet );
            return source == null ? false : source.WriteInputTypeDescription( writer, cmdlet, inputTypeName );
        }

        virtual public bool WriteCmdletNotes( XmlWriter writer, TypeDefinition cmdlet )
        {
            var source = GetSource( cmdlet );
            return source == null ? false : source.WriteCmdletNotes( writer, cmdlet );
        }

        virtual public bool WriteCmdletRelatedLinks( XmlWriter writer, TypeDefinition cmdlet )
        {
            var source = GetSource( cmdlet );
            return source == null ? false : source.WriteCmdletRelatedLinks( writer, cmdlet );
        }

        virtual public bool TryGetPropertySupportsGlobbing( PropertyDefinition property, string parameterSetName, out bool supportsGlobbing )
        {
            supportsGlobbing = false;
            var source = GetSource( property );
            return source == null ? false : source.TryGetPropertySupportsGlobbing( property, parameterSetName, out supportsGlobbing );
        }
    }

}
