using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Targets
{
    /// <summary>A fixed position written into the configuration.</summary>
    /// <remarks>
    ///     <see cref="TargetBase.HasEntity" /> is always false and <see cref="TargetBase.IsValid" /> is always true.
    ///     Section 8.5.5 leaves it to the author not to use one where an entity is needed, and HasEntity is how a component
    ///     can check.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.5</seealso>
    public sealed class PositionTarget : TargetBase
    {
        /// <summary>Where this target is.</summary>
        public Vector3 Position { get; internal set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override bool TryGetPosition(out Vector3 position)
        {
            position = Position;

            return true;
        }
    }
}
