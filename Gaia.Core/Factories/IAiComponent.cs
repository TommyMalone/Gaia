namespace Gaia.Core.Factories
{
    /// <summary>
    ///     What every factory-created component can do, load its own configuration from creation data. Conceptual
    ///     abstraction bases implement this, which is what lets one <see cref="ComponentFactory{TBase}" /> construct and
    ///     initialize any of them.
    /// </summary>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public interface IAiComponent
    {
        /// <summary>Loads the component's configuration from the given creation data.</summary>
        /// <returns>True if the configuration was loaded successfully.</returns>
        bool Init(AiCreationData creationData);
    }
}
