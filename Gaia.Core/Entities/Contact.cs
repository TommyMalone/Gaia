using Gaia.Core.Blackboards;

namespace Gaia.Core.Entities
{
    /// <summary>What one character believes about another, which may or may not be the truth.</summary>
    /// <remarks>
    ///     <para>
    ///         A contact adds nothing to an entity structurally. What makes it a contact is whose it is, in the same way
    ///         that what separates a <see cref="BrainBlackboard" /> from the <see cref="WorldBlackboard" /> is scope rather
    ///         than contents. Contacts live on the blackboard of the brain that holds the belief, and a loaded configuration
    ///         addresses only its own brain's blackboard and the world blackboard, so no other character's loaded components
    ///         read them.
    ///     </para>
    ///     <para>
    ///         Section 8.4.3.4 gives the reason for keeping beliefs apart from the actors they describe. A player who steals
    ///         a uniform is still an enemy on their own actor, but the guard's contact for them can say otherwise, and the
    ///         guard acts on the contact.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.3.4</seealso>
    public class Contact : EntityBase
    {
    }
}
