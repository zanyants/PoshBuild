using System;
using Mono.Cecil;

namespace PoshBuild
{
    /// <summary>
    /// An abstract specialization of <see cref="KeyedDocSource{string}"/> keyed on the full name of the
    /// assembly in which a type is defined.
    /// </summary>
    abstract class KeyedByAssemblyDocSource : KeyedDocSource<string>
    {
        protected override string GetKey( TypeDefinition type, out CreateDocSourceForKey createDocSource )
        {
            if ( type == null )
                throw new ArgumentNullException( "type" );

            createDocSource = CreateDocSource;
            return type.Module.Assembly.FullName;
        }

        /// <summary>
        /// Explicitly add an entry to the cache.
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="docSource"></param>
        public void Add( AssemblyDefinition assembly, IDocSource docSource )
        {
            base.Add( assembly.FullName, docSource );
        }

        protected abstract IDocSource CreateDocSource( TypeDefinition type, string key );
    }
}
