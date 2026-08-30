using Gaia.Core.Brains;
using Gaia.Core.Spatial;

namespace Gaia.Core.Entities
{
    /// <summary>A character in the game, as the game itself knows it.</summary>
    /// <remarks>
    ///     The game writes everything the AI may need about the character here, and the components of any brain read it
    ///     back. What an actor records is the truth, as opposed to a contact, which is one character's belief about another.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.3.3</seealso>
    public class Actor : EntityBase
    {
        /// <summary>Which way this character is facing.</summary>
        /// <remarks>
        ///     Kept off <see cref="EntityBase" /> on purpose. A contact, a threat or a stimulus has a position but often no
        ///     meaningful facing, and a rotation that every entity had to carry would sit unwritten at its default most of the
        ///     time.
        /// </remarks>
        public Rotation Rotation { get; set; } = Rotation.Identity;

        /// <summary>The decision-making logic for this character, or null if nothing decides for it.</summary>
        /// <remarks>
        ///     Section 8.4.3.3 allows an actor for every character whether the AI controls it or not, so a player's actor may
        ///     have no brain at all, or a brain that is disabled while the player is in control. Calling Update in either state
        ///     does nothing.
        /// </remarks>
        public Brain? Brain { get; set; }

        /// <summary>Runs one decision cycle of <see cref="Brain" />, if there is one and it is enabled.</summary>
        public void Update()
        {
            Brain?.Update();
        }
    }
}
