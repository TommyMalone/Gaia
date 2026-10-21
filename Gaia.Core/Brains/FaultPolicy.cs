namespace Gaia.Core.Brains
{
    /// <summary>What a brain does with an exception a component throws during a cycle.</summary>
    /// <remarks>
    ///     A component throwing at runtime is a bug in the game rather than in its configuration, so the only question is
    ///     where it surfaces. Rethrow keeps the stack intact, which is what a debugger, a test run and a development build
    ///     want. Contain keeps one broken character from stopping every other brain the host runs, which is what a shipped
    ///     build wants.
    /// </remarks>
    public enum FaultPolicy
    {
        /// <summary>The exception comes out of Update. The default.</summary>
        Rethrow,

        /// <summary>The brain keeps the exception as its Fault, reports it at Error severity, and disables itself.</summary>
        Contain,
    }
}
