using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Targets
{
    /// <summary>The character the AI controls.</summary>
    /// <remarks>
    ///     The chapter's Self target reaches its actor through the brain it was created for. Init reads Self from
    ///     <see cref="AiCreationData.Self" />, and a host or a test sets it directly.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.5</seealso>
    public sealed class SelfTarget : TargetBase
    {
        internal static readonly ComponentDescription Description =
            new DescriptionBuilder(ConceptualAbstraction.Targets, "The character itself.").Build();

        /// <summary>The actor this AI controls.</summary>
        public Actor? Self { get; set; }

        /// <summary>Always true, since this kind of target stands for the actor itself.</summary>
        public override bool HasEntity => true;

        /// <summary>Self, or null if none has been configured.</summary>
        public override EntityBase? Entity => Self;

        /// <summary>True while Self is not null and its IsValid is true.</summary>
        public override bool IsValid => Self is { IsValid: true };

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            if (!creationData.RefuseChildren())
            {
                return false;
            }

            Self = creationData.Self;
            return true;
        }

        /// <inheritdoc />
        public override bool TryGetPosition(out Vector3 position)
        {
            if (Self == null)
            {
                position = Vector3.Zero;

                return false;
            }

            position = Self.Position;

            return true;
        }
    }
}
