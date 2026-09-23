using Gaia.Core.DataStores;
using Gaia.Core.Factories;
using Gaia.Core.Strings;

namespace Gaia.Core.Tests.Factories;

/// <summary>Covers <see cref="DescriptionBuilder" />, the tie between a component's name fields and its description.</summary>
/// <remarks>
///     A component declares its attribute and slot fields through a builder and its description as the builder's
///     Build, so what is asserted here is that the description holds what was made through it, in that order, and that a
///     builder which has handed its description out takes nothing more.
/// </remarks>
[TestFixture]
public class DescriptionBuilderSpec
{
    [SetUp]
    public void StartADescription()
    {
        _builder = new DescriptionBuilder(ConceptualAbstraction.Regions, "A ball.");
    }

    private DescriptionBuilder _builder;

    [Test]
    public void HoldsTheAttributesItMadeInTheOrderItMadeThem()
    {
        _builder.Optional("Origin", AttributeKind.Vector3, "(0, 0, 0)", "Where the middle is.");
        _builder.Required("Radius", AttributeKind.Float, "How far it reaches.");

        ComponentDescription description = _builder.Build();

        Assert.Multiple(() =>
        {
            Assert.That(description.Attributes[0].Spelling, Is.EqualTo("Origin"));
            Assert.That(description.Attributes[1].Spelling, Is.EqualTo("Radius"));
            Assert.That(description.Attributes, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void HoldsTheSlotsItMadeInTheOrderItMadeThem()
    {
        _builder.Slot("OriginTarget", ConceptualAbstraction.Targets, 0, 1);
        _builder.Slot("Corner", new DescriptionBuilder("One corner.").Build(), 3, SlotDescription.Unbounded);

        ComponentDescription description = _builder.Build();

        Assert.Multiple(() =>
        {
            Assert.That(description.Slots[0].Spelling, Is.EqualTo("OriginTarget"));
            Assert.That(description.Slots[1].Spelling, Is.EqualTo("Corner"));
            Assert.That(description.Slots, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void MakesTheNameAComponentReadsByFromTheSpelling()
    {
        AttributeDescription radius = _builder.Required("Radius", AttributeKind.Float, "How far it reaches.");

        Assert.That(radius.Name, Is.EqualTo(new AiString("Radius")));
    }

    [Test]
    public void HandsBackTheAttributeItMadeForTheFieldThatNamesIt()
    {
        AttributeDescription variable =
            _builder.VariableName("Variable", DataStoreValueType.Float, "The name of the value to read.");

        Assert.Multiple(() =>
        {
            Assert.That(variable.Kind, Is.EqualTo(AttributeKind.VariableName));
            Assert.That(variable.VariableKind, Is.EqualTo(DataStoreValueType.Float));
            Assert.That(_builder.Build().Attributes[0], Is.SameAs(variable));
        });
    }

    [Test]
    public void KeepsAPieceAnotherTypeOwnsAmongItsOwn()
    {
        AttributeDescription shared = AttributeDescription.Optional("Store", "Store", AttributeKind.String, "Brain",
            "Which blackboard holds the value.");

        _builder.Required("Radius", AttributeKind.Float, "How far it reaches.");
        _builder.Add(shared);

        ComponentDescription description = _builder.Build();

        Assert.Multiple(() =>
        {
            Assert.That(description.Attributes[1], Is.SameAs(shared));
            Assert.That(description.Attributes, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void CarriesTheAbstractionAndSummaryItWasStartedWith()
    {
        ComponentDescription description = _builder.Build();

        Assert.Multiple(() =>
        {
            Assert.That(description.Abstraction, Is.EqualTo(ConceptualAbstraction.Regions));
            Assert.That(description.Summary, Is.EqualTo("A ball."));
        });
    }

    [Test]
    public void DescribesAStructuralElementUnderNoAbstraction()
    {
        DescriptionBuilder keyframe = new("One point on the curve.");

        Assert.That(keyframe.Build().Abstraction, Is.Null);
    }

    [Test]
    public void RefusesToBuildASecondDescription()
    {
        _builder.Build();

        Assert.That(() => _builder.Build(), Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void RefusesAnAttributeAfterItsDescriptionIsBuilt()
    {
        _builder.Build();

        Assert.That(() => _builder.Required("Radius", AttributeKind.Float, "How far it reaches."),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void RefusesASlotAfterItsDescriptionIsBuilt()
    {
        _builder.Build();

        Assert.That(() => _builder.Slot("OriginTarget", ConceptualAbstraction.Targets, 0, 1),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void RefusesASharedPieceAfterItsDescriptionIsBuilt()
    {
        AttributeDescription shared = AttributeDescription.Optional("Store", "Store", AttributeKind.String, "Brain",
            "Which blackboard holds the value.");

        _builder.Build();

        Assert.That(() => _builder.Add(shared), Throws.InstanceOf<InvalidOperationException>());
    }
}
