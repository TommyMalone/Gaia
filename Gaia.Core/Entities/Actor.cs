using Gaia.Core.Brains;

namespace Gaia.Core.Entities
{
    /// <summary>A character in the game, as the game itself knows it.</summary>
    /// <remarks>
    ///     The game writes everything the AI may need about the character here, and the components of any brain read it
    ///     back. A component reaches it through a target, or as <see cref="Factories.AiCreationData.Self" /> when the actor is
    ///     the brain's own, since no brain file names an actor as a store. A game's own variable consideration can also be
    ///     handed one through <see cref="Considerations.VariableConsiderationBase.DataStore" />. What an actor records is the
    ///     truth, as opposed to a contact, which is one character's belief about another.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.3.3</seealso>
    public class Actor : EntityBase
    {
        /// <summary>The decision-making logic for this character, or null if nothing decides for it.</summary>
        /// <remarks>
        ///     Section 8.4.3.3 allows an actor for every character whether the AI controls it or not, so a player's actor may
        ///     have no brain at all, or a brain that is disabled while the player is in control.
        /// </remarks>
        public Brain? Brain { get; set; }

        /// <summary>Runs one decision cycle of <see cref="Brain" />, if there is one and it is enabled.</summary>
        public void Update()
        {
            Brain?.Update();
        }
    }
}
