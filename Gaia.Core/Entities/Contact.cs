using Gaia.Core.Blackboards;

namespace Gaia.Core.Entities
{
    /// <summary>What one character believes about another, which may or may not be the truth.</summary>
    /// <remarks>
    ///     A contact adds no members to an entity. What makes it a contact is that it lives on the
    ///     <see cref="BrainBlackboard" /> of the brain that holds the belief.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.3.4</seealso>
    public class Contact : EntityBase
    {
    }
}
