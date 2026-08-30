using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Factories;

/// <summary>Covers <see cref="ComponentFactory{TBase}" />, what it builds and the three failures it reports.</summary>
/// <remarks>
///     The registered components are <see cref="LoadableComponent" /> and <see cref="UnloadableComponent" />, whose
///     Inits succeed and fail on purpose, so the factory is exercised without depending on how any Gaia component loads.
///     Nodes are parsed through <see cref="XmlSpecificationReader" />, and reports are observed through a
///     <see cref="RecordingLogSink" /> filed under the source name. The element name Widget is deliberately arbitrary, the
///     factory reads only the Type discriminator and uses the element name in its reports.
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

    private RecordingLogSink _logSink;
    private FactorySet _factories;
    private ComponentFactory<IAiComponent> _factory;

    [Test]
    public void CreatesTheComponentTheTypeNames()
    {
        _factory.Register("Loadable", () => new LoadableComponent());
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
        _factory.Register("Unloadable", () => new UnloadableComponent());
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
        // A game overriding a built-in is just registering its name again, so the later creator must win.
        _factory.Register("Widget", () => new UnloadableComponent());
        _factory.Register("Widget", () => new LoadableComponent());
        AiCreationData creationData = CreationDataFor("""<Widget Type="Widget"/>""");

        bool wasCreated = _factory.TryCreate(creationData, out IAiComponent? component);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(component, Is.InstanceOf<LoadableComponent>());
        });
    }

    /// <summary>Creation data whose node is the given document's root and whose reports reach the fixture's sink.</summary>
    private AiCreationData CreationDataFor(string xml)
    {
        XmlSpecificationReader reader = new() { LogSink = _logSink };
        reader.TryRead(xml, SourceName, out SpecificationNode? root);

        return new AiCreationData(root!, _factories) { LogSink = _logSink };
    }
}
