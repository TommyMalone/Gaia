using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;

namespace Gaia.Core.Targets
{
    /// <summary>A fixed position, written into the configuration or set by the host.</summary>
    /// <remarks>
    ///     <see cref="TargetBase.HasEntity" /> is always false and <see cref="TargetBase.IsValid" /> is always true.
    ///     Section 8.5.5 leaves it to the author not to use one where an entity is needed, and HasEntity is how a component
    ///     can check.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.5</seealso>
    public sealed class PositionTarget : TargetBase
    {
        private static readonly DescriptionBuilder Builder = new(ConceptualAbstraction.Targets,
            "A fixed position in the world.");

        private static readonly AttributeDescription PositionAttribute = Builder.Required("Position",
            AttributeKind.Vector3, "Where the target is, as (x, y, z).");

        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>Where this target is.</summary>
        public Vector3 Position { get; set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (!creationData.RefuseChildren())
            {
                return false;
            }

            if (!node.ReadVector3(PositionAttribute.Name, out Vector3 position))
            {
                return false;
            }

            Position = position;
            return true;
        }

        /// <inheritdoc />
        public override bool TryGetPosition(out Vector3 position)
        {
            position = Position;

            return true;
        }
    }
}
