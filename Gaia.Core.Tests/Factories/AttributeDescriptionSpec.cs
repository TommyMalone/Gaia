using Gaia.Core.Actions;
using Gaia.Core.Factories;
using Gaia.Core.Strings;

namespace Gaia.Core.Tests.Factories;

/// <summary>Covers <see cref="AttributeDescription" />, the one thing its constructor refuses and the copy it makes.</summary>
/// <remarks>
///     What the shipped descriptions say is covered in DescriptionSpec. A caller that names an attribute without a
///     <see cref="DescriptionBuilder" /> writes the name and the spelling separately, so the constructor is what keeps the
///     two from disagreeing.
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

    [Test]
    public void CopiesItselfWithAnotherDefaultAndEveryOtherFieldKept()
    {
        AttributeDescription doneWhen = AttributeDescription.Enumeration<SubreasonerCompletion>("DoneWhen", "DoneWhen",
            nameof(SubreasonerCompletion.SelectedOptionDone), "When the action reports done.");

        AttributeDescription copy = doneWhen.WithDefault(nameof(SubreasonerCompletion.ReasonerDone));

        Assert.Multiple(() =>
        {
            Assert.That(copy.DefaultText, Is.EqualTo(nameof(SubreasonerCompletion.ReasonerDone)));
            Assert.That(doneWhen.DefaultText, Is.EqualTo(nameof(SubreasonerCompletion.SelectedOptionDone)),
                "the original keeps the default it was made with");
            Assert.That(copy.Name, Is.EqualTo(doneWhen.Name));
            Assert.That(copy.Spelling, Is.EqualTo(doneWhen.Spelling));
            Assert.That(copy.Kind, Is.EqualTo(doneWhen.Kind));
            Assert.That(copy.IsRequired, Is.EqualTo(doneWhen.IsRequired));
            Assert.That(copy.Summary, Is.EqualTo(doneWhen.Summary));
            Assert.That(copy.Choices, Is.EqualTo(doneWhen.Choices));
            Assert.That(copy.VariableKind, Is.EqualTo(doneWhen.VariableKind));
        });
    }
}
