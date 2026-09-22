using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Factories;

/// <summary>
///     Covers <see cref="ComponentFactory{TBase}" />, what it builds, the three failures it reports, and what it says
///     it holds.
/// </summary>
/// <remarks>
///     The registered components are <see cref="LoadableComponent" /> and <see cref="UnloadableComponent" />, whose
///     Inits succeed and fail on purpose, so the factory is exercised without depending on how any Gaia component loads.
///     Both register under <see cref="WidgetDescription" />, a description naming nothing, since what is asserted about a
///     description here is only that the factory hands back the one a name was registered with. Nodes are parsed through
///     <see cref="XmlSpecificationReader" />, and reports are observed through a <see cref="RecordingLogSink" /> filed
///     under the source name. The element name Widget is deliberately arbitrary, the factory reads only the Type
///     discriminator and uses the element name in its reports.
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

    /// <summary>Creation data whose node is the given document's root and whose reports reach the fixture's sink.</summary>
    private AiCreationData CreationDataFor(string xml)
    {
        XmlSpecificationReader reader = new(_logSink);
        reader.TryRead(xml, SourceName, out SpecificationNode? root);

        return new AiCreationData(root!, _factories) { LogSink = _logSink };
    }
}
