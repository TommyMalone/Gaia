namespace Gaia.Core.Factories
{
    /// <summary>
    ///     What every factory-created component can do, load its own configuration from creation data. A conceptual
    ///     abstraction is one of the kinds of component a brain is composed from, reasoner, option, consideration, action,
    ///     weight function, target and region, each with an abstract base and a factory of its own. The abstract bases
    ///     implement this, which is what lets one <see cref="ComponentFactory{TBase}" /> construct and initialize any of them.
    /// </summary>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public interface IAiComponent
    {
        /// <summary>Loads the component's configuration from the given creation data.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        bool Init(AiCreationData creationData);
    }
}
