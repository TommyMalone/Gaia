using Gaia.Core.Factories;
using Gaia.Core.Services.Log;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Factories;

/// <summary>Covers <see cref="AiCreationData" />, its defaults and how a child derivation shares its context.</summary>
[TestFixture]
public class AiCreationDataSpec
{
    [SetUp]
    public void ParseTheTree()
    {
        _logSink = new RecordingLogSink();
        _factories = new FactorySet();

        XmlSpecificationReader reader = new() { LogSink = _logSink };
        reader.TryRead(Snippet, "guard.xml", out SpecificationNode? root);

        _root = root!;
    }

    private const string Snippet = """
                                   <Option Type="ConsiderationAndAction">
                                       <Action Type="Pause" Duration="2"/>
                                   </Option>
                                   """;

    private RecordingLogSink _logSink;
    private FactorySet _factories;
    private SpecificationNode _root;

    [Test]
    public void StartsWithTheProperDefaults()
    {
        AiCreationData creationData = new(_root, _factories);

        Assert.Multiple(() =>
        {
            Assert.That(creationData.LogSink, Is.InstanceOf<TraceLogSink>());
            Assert.That(creationData.LogChannel, Is.EqualTo(AiString.Empty));
            Assert.That(creationData.TimeSource, Is.InstanceOf<SystemTimeSource>());
            Assert.That(creationData.RandomSource, Is.InstanceOf<SystemRandomSource>());
        });
    }

    [Test]
    public void SharesEverythingButTheNodeWithAChild()
    {
        ManualTimeSource timeSource = new();
        FixedRandomSource randomSource = new(0.5f);
        AiString guardChannel = "Guard";

        AiCreationData creationData = new(_root, _factories)
        {
            LogSink = _logSink,
            LogChannel = guardChannel,
            TimeSource = timeSource,
            RandomSource = randomSource,
        };

        AiCreationData childData = creationData.ForChild(_root.Children[0]);

        Assert.Multiple(() =>
        {
            Assert.That(childData.Node, Is.SameAs(_root.Children[0]));
            Assert.That(childData.Factories, Is.SameAs(_factories));
            Assert.That(childData.LogSink, Is.SameAs(_logSink));
            Assert.That(childData.LogChannel, Is.EqualTo(guardChannel));
            Assert.That(childData.TimeSource, Is.SameAs(timeSource));
            Assert.That(childData.RandomSource, Is.SameAs(randomSource));
        });
    }
}
