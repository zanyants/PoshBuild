using System;
using System.Threading;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace PoshBuild
{
    /// <summary>
    /// Per-thread MSBuild task context. Notably allows access to the <see cref="TaskLoggingHelper"/> of the current task
    /// to all code called by the task (in the current thread), so well-formatted errors and warnings can be issued by
    /// task-independent code when called in the context of a task.
    /// </summary>
    sealed class TaskContext
    {
        /// <summary>
        /// A disposable context scope lifetime object, intended to be used within a <c>using (...)</c> statement.
        /// </summary>
        sealed class TaskContextScope : IDisposable
        {
            TaskContext _previous;
            TaskContext _thisScope;

            public TaskContextScope( TaskContext context )
            {
                if ( context == null )
                    throw new ArgumentNullException( "context" );

                _thisScope = context;
                _previous = Current;
                Current = context;
            }

            int _disposed;

            void IDisposable.Dispose()
            {
                if ( Interlocked.CompareExchange( ref _disposed, 1, 0 ) == 0 )
                {
                    Current = _previous;
                }
            }
        }

        [ThreadStatic]
        static TaskContext _current;

        TaskContext( TaskLoggingHelper log )
        {
            if ( log == null )
                throw new ArgumentNullException( "log" );

            Log = log;
        }

        /// <summary>
        /// The current <see cref="TaskContext"/>, which will be <c>null</c> if the current thread is not in the context of an MSBuild task.
        /// </summary>
        public static TaskContext Current 
        { 
            get { return _current; } 
            private set { _current = value; } 
        }

        /// <summary>
        /// Create a new task context scope. The returned <see cref="IDisposable"/> must be disposed when the scope is closed,
        /// typically by using <c>using ( TaskContext.CreateScope(...) )</c>.
        /// </summary>
        public static IDisposable CreateScope( TaskLoggingHelper log )
        {
            return new TaskContextScope( new TaskContext( log ) );
        }

        /// <summary>
        /// The <see cref="TaskLoggingHelper"/> of the MSBuild task.
        /// </summary>
        public TaskLoggingHelper Log { get; private set; }

        public AssemblyDefinition PrimaryAssembly { get; private set; }

        public void SetPrimaryAssembly( AssemblyDefinition assembly )
        {
            if ( assembly == null )
                throw new ArgumentNullException( "assembly" );

            if ( PrimaryAssembly != null )
                throw new InvalidOperationException( "The property has already been set." );

            PrimaryAssembly = assembly;
        }

        public PerAssemblyXmlDocSource PerAssemblyXmlDocSource { get; private set; }

        public void SetPerAssemblyXmlDocSource( PerAssemblyXmlDocSource source )
        {
            if ( source == null )
                throw new ArgumentNullException( "source" );

            if ( PerAssemblyXmlDocSource != null )
                throw new InvalidOperationException( "The property has already been set." );

            PerAssemblyXmlDocSource = source;
        }

        public BuildTimeAssemblyResolver BuildTimeAssemblyResolver { get; private set; }

        public void SetBuildTimeAssemblyResolver( BuildTimeAssemblyResolver resolver )
        {
            if ( resolver == null )
                throw new ArgumentNullException( "resolver" );

            if ( BuildTimeAssemblyResolver != null )
                throw new InvalidOperationException( "The property has already been set." );

            BuildTimeAssemblyResolver = resolver;
        }
    }
}
