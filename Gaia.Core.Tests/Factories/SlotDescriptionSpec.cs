using Gaia.Core.Factories;
using Gaia.Core.Strings;

namespace Gaia.Core.Tests.Factories;

/// <summary>Covers <see cref="SlotDescription" />, the one thing its constructors refuse.</summary>
/// <remarks>What the shipped slots say is covered in DescriptionSpec.</remarks>
[TestFixture]
public class SlotDescriptionSpec
{
    [Test]
    public void RefusesASpellingThatDoesNotSpellTheName()
    {
        AiString name = "Reasoner";

        Assert.That(() => new SlotDescription(name, "Reasoners", ConceptualAbstraction.Reasoners, 1, 1),
            Throws.ArgumentException);
    }
}
