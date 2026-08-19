using Gaia.Core.Entities;
using Gaia.Core.Spatial;
using Gaia.Core.Temp;

namespace Gaia.Core.Targets
{
    /// <summary>The character the AI controls.</summary>
    /// <remarks>
    ///     The chapter's Self target reaches its actor through the brain it was created for. Nothing here has that
    ///     back-reference until a configuration loader supplies it through the creation data, so for now Self is a configured
    ///     property, which is also what a spec sets.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.5</seealso>
    public sealed class SelfTarget : TargetBase
    {
        /// <summary>The actor this AI controls.</summary>
        public Actor? Self { get; internal set; }

        /// <summary>Always true, since this kind of target stands for the actor itself.</summary>
        public override bool HasEntity => true;

        /// <summary>Self, or null if none has been configured.</summary>
        public override EntityBase? Entity => Self;

        /// <summary>True while Self is not null and its IsValid is true.</summary>
        public override bool IsValid => Self is { IsValid: true };

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
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
