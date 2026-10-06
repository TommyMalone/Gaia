using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Factories;

/// <summary>
///     Covers <see cref="ComponentFactory{TBase}" />, what it builds, the failures it reports, the check it makes
///     against a type's description before and after Init, and what it says it holds.
/// </summary>
/// <remarks>
///     The registered components are <see cref="LoadableComponent" /> and <see cref="UnloadableComponent" />, whose
///     Inits succeed and fail on purpose, so the factory is exercised without depending on how any Gaia component loads.
///     Most tests register under <see cref="WidgetDescription" />, which names nothing. A test of the description check
///     registers under a widget whose name says what it takes. Nodes are parsed through
///     <see cref="XmlSpecificationReader" />, and reports are observed through a <see cref="RecordingLogSink" /> filed
///     under the source name. The element name Widget is arbitrary. The factory picks a type by the Type attribute alone
///     and uses the element name only in its reports.
/// </remarks>
[TestFixture]
public class ComponentFactorySpec
{
    [SetUp]
    public void BuildFactory()
    {
        _logSink = new RecordingLogSink();
        _factories = new FactorySet();
        _factory = new ComponentFactory<IAiComponent>();
    }

    private const string SourceName = "factory.xml";

    private static readonly ComponentDescription WidgetDescription = new("A widget.",
        Array.Empty<AttributeDescription>(), Array.Empty<SlotDescription>());

    /// <summary>
    ///     An inline element that takes nothing. The Bolt and Gear slots below hold it, as does every Part slot but the
    ///     bolted one.
    /// </summary>
    private static readonly ComponentDescription BareDescription = new("A piece that holds nothing.",
        Array.Empty<AttributeDescription>(), Array.Empty<SlotDescription>());

    /// <summary>An inline Part whose own Bolt slot takes at least one child.</summary>
    private static readonly ComponentDescription BoltedPartDescription = new("A part that needs a bolt.",
        Array.Empty<AttributeDescription>(),
        new[] { new SlotDescription("Bolt", "Bolt", BareDescription, 1, SlotDescription.Unbounded) });

    private static readonly ComponentDescription WidgetWithOnePart = Describe(
        new SlotDescription("Part", "Part", BareDescription, 0, 1));

    private static readonly ComponentDescription WidgetWithARequiredPart = Describe(
        new SlotDescription("Part", "Part", BareDescription, 1, 1));

    private static readonly ComponentDescription WidgetWithABoltedPart = Describe(
        new SlotDescription("Part", "Part", BoltedPartDescription, 0, 1));

    private static readonly ComponentDescription WidgetWithTwoGears = Describe(
        new SlotDescription("Gear", "Gear", BareDescription, 0, 2));

    private static readonly ComponentDescription WidgetWithThreeGears = Describe(
        new SlotDescription("Gear", "Gear", BareDescription, 3, SlotDescription.Unbounded));

    private static readonly ComponentDescription WidgetWithAnyGears = Describe(
        new SlotDescription("Gear", "Gear", BareDescription, 0, SlotDescription.Unbounded));

    private static readonly ComponentDescription WidgetWithATarget = Describe(
        new SlotDescription("OriginTarget", "OriginTarget", ConceptualAbstraction.Targets, 0, 1));

    /// <summary>A widget with one Part at most beside any number of Gears, so a count has other slots' children to pass over.</summary>
    private static readonly ComponentDescription WidgetWithAPartAndGears = new("A widget.",
        Array.Empty<AttributeDescription>(), new[]
        {
            new SlotDescription("Part", "Part", BareDescription, 0, 1),
            new SlotDescription("Gear", "Gear", BareDescription, 0, SlotDescription.Unbounded),
        });

    /// <summary>A widget reading Weight and Size, so an undescribed attribute can stand after described ones.</summary>
    private static readonly ComponentDescription WidgetWithWeightAndSize = new("A widget.", new[]
    {
        AttributeDescription.Optional("Weight", "Weight", AttributeKind.Float, "1", "How heavy the widget is."),
        AttributeDescription.Optional("Size", "Size", AttributeKind.Float, "1", "How big the widget is."),
    }, Array.Empty<SlotDescription>());

    private RecordingLogSink _logSink;
    private FactorySet _factories;
    private ComponentFactory<IAiComponent> _factory;

    [Test]
    public void CreatesTheComponentTheTypeNames()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetDescription);
        AiCreationData creationData = CreationDataFor("""<Widget Type="Loadable"/>""");

        bool wasCreated = _factory.TryCreate(creationData, out IAiComponent? component);

        LoadableComponent loadable = (LoadableComponent)component!;

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(loadable.ReceivedCreationData, Is.SameAs(creationData), "Init pulls from this data");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void ReportsAnElementWithNoType()
    {
        AiCreationData creationData = CreationDataFor("<Widget/>");

        bool wasCreated = _factory.TryCreate(creationData, out IAiComponent? component);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(component, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 1, the Widget element has no Type attribute",
            }));
        });
    }

    [Test]
    public void ReportsATypeNobodyRegistered()
    {
        AiCreationData creationData = CreationDataFor("""<Widget Type="Missing"/>""");

        bool wasCreated = _factory.TryCreate(creationData, out IAiComponent? component);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(component, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 1, the Widget element's type Missing is not registered",
            }));
        });
    }

    [Test]
    public void ReportsAComponentThatFailsToInitialize()
    {
        _factory.Register("Unloadable", () => new UnloadableComponent(), WidgetDescription);
        AiCreationData creationData = CreationDataFor("""<Widget Type="Unloadable"/>""");

        bool wasCreated = _factory.TryCreate(creationData, out IAiComponent? component);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(component, Is.Null, "a component that could not load is not handed out");
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 1, the Widget element's type Unloadable failed to initialize",
            }));
        });
    }

    [Test]
    public void ReplacesARegistrationWithTheSameName()
    {
        // A game overriding a built-in registers its name again, so the later creator and description must be the
        // ones kept.
        ComponentDescription replacement = new("The widget a game registered.", Array.Empty<AttributeDescription>(),
            Array.Empty<SlotDescription>());

        _factory.Register("Widget", () => new UnloadableComponent(), WidgetDescription);
        _factory.Register("Widget", () => new LoadableComponent(), replacement);
        AiCreationData creationData = CreationDataFor("""<Widget Type="Widget"/>""");

        bool wasCreated = _factory.TryCreate(creationData, out IAiComponent? component);
        _factory.TryGetDescription("Widget", out ComponentDescription? description);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(component, Is.InstanceOf<LoadableComponent>());
            Assert.That(description, Is.SameAs(replacement));
        });
    }

    [Test]
    public void ReportsTheDescriptionATypeWasRegisteredWith()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetDescription);

        bool isRegistered = _factory.TryGetDescription("Loadable", out ComponentDescription? description);

        Assert.Multiple(() =>
        {
            Assert.That(isRegistered, Is.True);
            Assert.That(description, Is.SameAs(WidgetDescription));
        });
    }

    [Test]
    public void ReportsNoDescriptionForATypeNobodyRegistered()
    {
        bool isRegistered = _factory.TryGetDescription("Missing", out ComponentDescription? description);

        Assert.Multiple(() =>
        {
            Assert.That(isRegistered, Is.False);
            Assert.That(description, Is.Null);
        });
    }

    [Test]
    public void KeepsAReplacedTypeInItsFirstPlace()
    {
        _factory.Register("Widget", () => new UnloadableComponent(), WidgetDescription);
        _factory.Register("Other", () => new LoadableComponent(), WidgetDescription);
        _factory.Register("Widget", () => new LoadableComponent(), WidgetDescription);

        Assert.That(_factory.Types, Is.EqualTo(new AiString[] { "Widget", "Other" }));
    }

    [Test]
    public void KeepsTheSpellingATypeWasRegisteredUnder()
    {
        // The table keeps the casing it met first, so the name is planted in another casing before the registration
        // and the two spellings can be told apart.
        AiString planted = new("gADGET");

        _factory.Register("Gadget", () => new LoadableComponent(), WidgetDescription);

        Assert.Multiple(() =>
        {
            Assert.That(planted.Text, Is.EqualTo("gADGET"), "the table met the name in the planted casing first");
            Assert.That(_factory.TypeSpellings[^1], Is.EqualTo("Gadget"));
            Assert.That(_factory.Types[^1].Text, Is.EqualTo("gADGET"), "the identity's own text is the table's");
        });
    }

    [Test]
    public void TakesTheSpellingOfTheLatestRegistrationOfAName()
    {
        _factory.Register("Widget", () => new UnloadableComponent(), WidgetDescription);
        _factory.Register("WIDGET", () => new LoadableComponent(), WidgetDescription);

        Assert.Multiple(() =>
        {
            Assert.That(_factory.Types, Has.Count.EqualTo(1));
            Assert.That(_factory.TypeSpellings, Is.EqualTo(new[] { "WIDGET" }));
        });
    }

    [Test]
    public void RefusesAnAttributeItsDescriptionDoesNotName()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetDescription);
        AiCreationData creationData = CreationDataFor("""<Widget Type="Loadable" Ranck="10"/>""");

        bool wasCreated = _factory.TryCreate(creationData, out IAiComponent? component);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(component, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 1, the Widget element does not take the Ranck attribute",
            }));
        });
    }

    [Test]
    public void BuildsNothingForAnElementItRefuses()
    {
        int constructed = 0;

        _factory.Register("Loadable", () =>
        {
            constructed++;
            return new LoadableComponent();
        }, WidgetDescription);
        AiCreationData creationData = CreationDataFor("""<Widget Type="Loadable" Ranck="10"/>""");

        _factory.TryCreate(creationData, out _);

        Assert.That(constructed, Is.EqualTo(0), "the creator never runs for an element the check refuses");
    }

    [Test]
    public void RefusesAChildThatFillsNoSlot()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetDescription);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Junk/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out IAiComponent? component);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(component, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 2, the Widget element does not take Junk elements",
            }), "one line at the child's line, and no failed to initialize, since Init never ran");
        });
    }

    [Test]
    public void RefusesASecondChildInASlotThatTakesOne()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithOnePart);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Part/>
                                                          <Part/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 3, the Widget element takes only one Part element",
            }));
        });
    }

    [Test]
    public void RefusesAChildPastABoundAboveOne()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithTwoGears);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Gear/>
                                                          <Gear/>
                                                          <Gear/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 4, the Widget element takes at most 2 Gear elements",
            }));
        });
    }

    [Test]
    public void RefusesAnAttributeOnAnInlineChild()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithOnePart);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Part Ranck="1"/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 2, the Part element does not take the Ranck attribute",
            }));
        });
    }

    [Test]
    public void RefusesAChildOfAnInlineChild()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithOnePart);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Part>
                                                              <Bolt/>
                                                          </Part>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 3, the Part element does not take Bolt elements",
            }));
        });
    }

    [Test]
    public void RefusesATypeOnAnInlineChild()
    {
        // An inline element is read by its owner under its name, so a Type there picks nothing and is a stray.
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithOnePart);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Part Type="Loadable"/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 2, the Part element does not take the Type attribute",
            }));
        });
    }

    [Test]
    public void TakesTheTypeOfAComponentDescribedWithoutAnAbstraction()
    {
        // A game may describe a component with the one-argument builder, which records no abstraction. The factory
        // still picked the component by its Type, so the Type is not refused.
        ComponentDescription withoutAbstraction = new DescriptionBuilder("A widget described with no abstraction.")
            .Build();

        _factory.Register("Loadable", () => new LoadableComponent(), withoutAbstraction);
        AiCreationData creationData = CreationDataFor("""<Widget Type="Loadable"/>""");

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(withoutAbstraction.Abstraction, Is.Null);
            Assert.That(wasCreated, Is.True);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void LeavesATypePickedChildToItsOwnFactory()
    {
        // The owner's Init builds a child of an abstraction's slot through that child's factory, which checks it
        // then. This owner builds nothing, so the stray attribute on the child is never checked.
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithATarget);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <OriginTarget Type="Position" Ranck="1"/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void ReportsOnlyTheFirstThingItRefuses()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetDescription);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable" Ranck="1">
                                                          <Junk/>
                                                      </Widget>
                                                      """);

        _factory.TryCreate(creationData, out _);

        Assert.That(_logSink.Entries, Is.EqualTo(new[]
        {
            "Error factory.xml Line 1, the Widget element does not take the Ranck attribute",
        }), "the attribute comes first in document order, and loading stops at the first error");
    }

    [Test]
    public void SpellsTheElementAsTheDocumentDoes()
    {
        // The table keeps the casing it met first, so the name is planted in the description's casing before the
        // document is parsed, and the report shows which of the two spellings it prints.
        AiString planted = new("Sprocket");

        _factory.Register("Loadable", () => new LoadableComponent(), WidgetDescription);
        AiCreationData creationData = CreationDataFor("""<sPROCKET Type="Loadable" Ranck="1"/>""");

        _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(planted.Text, Is.EqualTo("Sprocket"), "the table met the name in the planted casing first");
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 1, the sPROCKET element does not take the Ranck attribute",
            }));
        });
    }

    [Test]
    public void SpellsTheElementAsTheDocumentDoesInItsOwnThreeLines()
    {
        // The factory refuses an element itself for no Type, for a Type nothing registered and for an Init that failed.
        // The name is planted in another casing first, so each of the three lines shows the element as the document
        // spells it.
        AiString planted = new("Cog");

        _factory.Register("Unloadable", () => new UnloadableComponent(), WidgetDescription);

        _factory.TryCreate(CreationDataFor("<cOG/>"), out _);
        _factory.TryCreate(CreationDataFor("""<cOG Type="Missing"/>"""), out _);
        _factory.TryCreate(CreationDataFor("""<cOG Type="Unloadable"/>"""), out _);

        Assert.Multiple(() =>
        {
            Assert.That(planted.Text, Is.EqualTo("Cog"), "the table met the name in the planted casing first");
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 1, the cOG element has no Type attribute",
                "Error factory.xml Line 1, the cOG element's type Missing is not registered",
                "Error factory.xml Line 1, the cOG element's type Unloadable failed to initialize",
            }));
        });
    }

    [Test]
    public void RefusesAnEmptySlotItsComponentDoesNotRefuse()
    {
        LoadableComponent loadable = new();

        _factory.Register("Loadable", () => loadable, WidgetWithARequiredPart);
        AiCreationData creationData = CreationDataFor("""<Widget Type="Loadable"/>""");

        bool wasCreated = _factory.TryCreate(creationData, out IAiComponent? component);

        Assert.Multiple(() =>
        {
            Assert.That(loadable.InitCount, Is.EqualTo(1), "the minimum is checked after Init returns true");
            Assert.That(wasCreated, Is.False);
            Assert.That(component, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 1, the Widget element has no Part element",
            }), "one line, with no failed to initialize, since Init succeeded and the refusal is the factory's");
        });
    }

    [Test]
    public void RefusesASlotShortOfAMinimumAboveOne()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithThreeGears);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Gear/>
                                                          <Gear/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 1, the Widget element has fewer than 3 Gear elements",
            }));
        });
    }

    [Test]
    public void ReportsAComponentsOwnRefusalOfAShortSlotAlone()
    {
        _factory.Register("Wanting", () => new PartWantingComponent(), WidgetWithARequiredPart);
        AiCreationData creationData = CreationDataFor("""<Widget Type="Wanting"/>""");

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 1, the Widget element wants a Part element that says what it is made of",
                "Error factory.xml Line 1, the Widget element's type Wanting failed to initialize",
            }), "the component's own sentence is reported first, and the factory never counts the slot");
            Assert.That(_logSink.Entries, Has.None.Contains("has no Part element"));
        });
    }

    [Test]
    public void TakesNoChildrenInASlotWhoseMinimumIsZero()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithOnePart);
        AiCreationData creationData = CreationDataFor("""<Widget Type="Loadable"/>""");

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesAShortSlotInsideAnInlineChild()
    {
        LoadableComponent loadable = new();

        _factory.Register("Loadable", () => loadable, WidgetWithABoltedPart);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Part/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(loadable.InitCount, Is.EqualTo(1), "the inline minimum is checked after Init as well");
            Assert.That(wasCreated, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 2, the Part element has no Bolt element",
            }));
        });
    }

    [Test]
    public void RefusesAnUndescribedAttributeAfterDescribedOnes()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithWeightAndSize);
        AiCreationData creationData = CreationDataFor("""<Widget Type="Loadable" Weight="2" Size="3" Ranck="1"/>""");

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 1, the Widget element does not take the Ranck attribute",
            }), "the described attributes before it pass, and the last one is the one named");
        });
    }

    [Test]
    public void SpellsTheAttributeAsTheDocumentDoes()
    {
        // The table keeps the casing it met first, so the attribute name is planted in one casing before the document
        // writes it in another, and the report shows which spelling it prints.
        AiString planted = new("Wobble");

        _factory.Register("Loadable", () => new LoadableComponent(), WidgetDescription);
        AiCreationData creationData = CreationDataFor("""<Widget Type="Loadable" wOBBLE="1"/>""");

        _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(planted.Text, Is.EqualTo("Wobble"), "the table met the name in the planted casing first");
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 1, the Widget element does not take the wOBBLE attribute",
            }));
        });
    }

    [Test]
    public void RefusesAJunkChildAfterChildrenItTakes()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithAnyGears);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Gear/>
                                                          <Gear/>
                                                          <Junk/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error factory.xml Line 4, the Widget element does not take Junk elements",
            }));
        });
    }

    [Test]
    public void SpellsTheChildAsTheDocumentDoesInACountRefusal()
    {
        // The second child is the one refused, so the sentence spells it as that child's tag does.
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithOnePart);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Part/>
                                                          <pART/>
                                                      </Widget>
                                                      """);

        _factory.TryCreate(creationData, out _);

        Assert.That(_logSink.Entries, Is.EqualTo(new[]
        {
            "Error factory.xml Line 3, the Widget element takes only one pART element",
        }));
    }

    [Test]
    public void TakesAChildAtItsSlotsBound()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithTwoGears);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Gear/>
                                                          <Gear/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True, "the bound is the most a slot takes, and the second gear is at it");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void CountsOnlyTheChildrenOfTheSlotItBounds()
    {
        // Two gears stand before the one part, and neither counts against the part's bound of one.
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithAPartAndGears);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Gear/>
                                                          <Gear/>
                                                          <Part/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void TakesASlotFilledToItsMinimum()
    {
        _factory.Register("Loadable", () => new LoadableComponent(), WidgetWithThreeGears);
        AiCreationData creationData = CreationDataFor("""
                                                      <Widget Type="Loadable">
                                                          <Gear/>
                                                          <Gear/>
                                                          <Gear/>
                                                      </Widget>
                                                      """);

        bool wasCreated = _factory.TryCreate(creationData, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True, "the minimum is the fewest a slot takes, and three gears meet it");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    /// <summary>A widget description holding the one slot given and no attributes.</summary>
    private static ComponentDescription Describe(SlotDescription slot)
    {
        return new ComponentDescription("A widget.", Array.Empty<AttributeDescription>(), new[] { slot });
    }

    /// <summary>Creation data whose node is the given document's root and whose reports reach the fixture's sink.</summary>
    private AiCreationData CreationDataFor(string xml)
    {
        XmlSpecificationReader reader = new(_logSink);
        reader.TryRead(xml, SourceName, out SpecificationNode? root);

        return new AiCreationData(root!, _factories) { LogSink = _logSink };
    }

    /// <summary>A component whose Init refuses an element with no Part child in a sentence of its own.</summary>
    private sealed class PartWantingComponent : IAiComponent
    {
        /// <summary>Refuses a node with no children, reporting why in its own words, and loads one that has any.</summary>
        public bool Init(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (node.Children.Count == 0)
            {
                creationData.ReportError(
                    $"Line {node.LineNumber}, the {node.NameSpelling} element wants a Part element that says what it is made of");
                return false;
            }

            return true;
        }
    }
}
