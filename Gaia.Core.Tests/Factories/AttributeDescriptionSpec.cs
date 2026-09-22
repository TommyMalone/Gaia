using Gaia.Core.Factories;
using Gaia.Core.Strings;

namespace Gaia.Core.Tests.Factories;

/// <summary>Covers <see cref="AttributeDescription" />, the one thing its constructor refuses.</summary>
/// <remarks>
///     What the shipped descriptions say is covered in DescriptionSpec. A spelling is a second literal beside the
///     <see cref="AiString" /> field it spells, so the constructor is what keeps the two from disagreeing.
/// </remarks>
[TestFixture]
public class AttributeDescriptionSpec
{
    [Test]
    public void RefusesASpellingThatDoesNotSpellTheName()
    {
        AiString name = "Duration";

        Assert.That(() => AttributeDescription.Required(name, "Durration", AttributeKind.Float, "How long to wait."),
            Throws.ArgumentException);
    }

    [Test]
    public void TakesASpellingInAnotherCasingOfTheName()
    {
        // A name matches in any casing, so a spelling differs from a field only in its letters and never in identity.
        AiString name = "duration";

        AttributeDescription described = AttributeDescription.Required(name, "Duration", AttributeKind.Float,
            "How long to wait.");

        Assert.Multiple(() =>
        {
            Assert.That(described.Name, Is.EqualTo(name));
            Assert.That(described.Spelling, Is.EqualTo("Duration"));
        });
    }
}
