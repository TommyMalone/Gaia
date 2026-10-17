namespace Gaia.Core.Factories
{
    /// <summary>The kind of store a Store attribute names, one of the blackboards.</summary>
    internal enum StoreKind
    {
        /// <summary>The blackboard of the brain being built.</summary>
        Brain,

        /// <summary>The blackboard every character shares.</summary>
        World,
    }
}
