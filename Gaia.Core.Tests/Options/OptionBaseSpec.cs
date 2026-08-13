using Gaia.Core.Considerations;
using Gaia.Core.Options;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Options;

/// <summary>Covers how the considerations an option takes learn which option they belong to.</summary>
/// <remarks>
///     The rest of <see cref="OptionBase" />, combining, screening and the lifecycle, is covered through the reasoner
///     specs. What is asserted here is the Option back-reference a consideration needs when it evaluates its own option
///     rather than the world.
/// </remarks>
[TestFixture]
public class OptionBaseSpec
{
    [SetUp]
    public void BuildOption()
    {
        _consideration = new StubConsideration(WeightValues.Neutral);
        _option = new StubOption([_consideration], []);
    }

    private StubConsideration _consideration;
    private StubOption _option;

    [Test]
    public void TellsAConsiderationWhichOptionItBelongsTo()
    {
        Assert.That(_consideration.Option, Is.SameAs(_option));
    }

    [Test]
    public void TellsAConsiderationAddedLaterAsWell()
    {
        StubConsideration lateConsideration = new(WeightValues.Neutral);

        _option.AddConsideration(lateConsideration);

        Assert.That(lateConsideration.Option, Is.SameAs(_option));
    }

    [Test]
    public void TellsTheConsiderationsInsideANestedSet()
    {
        StubConsideration innerConsideration = new(WeightValues.Neutral);
        ConsiderationSetConsideration nestedSet = new([innerConsideration]);

        _option.AddConsideration(nestedSet);

        Assert.Multiple(() =>
        {
            Assert.That(nestedSet.Option, Is.SameAs(_option));
            Assert.That(innerConsideration.Option, Is.SameAs(_option));
        });
    }

    [Test]
    public void LeavesAConsiderationNoOptionUntilOneTakesIt()
    {
        StubConsideration looseConsideration = new(WeightValues.Neutral);

        Assert.That(looseConsideration.Option, Is.Null);
    }
}
