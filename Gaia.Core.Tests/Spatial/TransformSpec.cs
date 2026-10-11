using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Spatial;

/// <summary>
///     Covers <see cref="Transform" />, carrying points in and out of its local coordinates, placing one transform
///     inside another, moving one to another origin, and its value equality.
/// </summary>
/// <remarks>
///     Each turn is built with <see cref="Rotation.FromEulerAngles" />, and what each conversion is expected to give
///     was worked by hand from the turned directions, so none of it comes from the conversions under test. Positions are
///     compared within a tolerance because the directions come out of trigonometry in floats.
/// </remarks>
[TestFixture]
public class TransformSpec
{
    private const float Tolerance = 0.001f;

    [Test]
    public void PutsALocalPointAlongItsDirections()
    {
        // A quarter turn, since with no turn or a half turn a conversion that turns the wrong way gives the same
        // answer. Turned a quarter right, Forward lies along the world's X and Right along the world's -Z.
        Vector3 origin = new(10f, 0f, 0f);
        Rotation quarterTurnRight = Rotation.FromEulerAngles(0f, 90f, 0f);
        Transform turned = new(origin, quarterTurnRight);

        Vector3 oneAhead = new(0f, 0f, 1f);
        Vector3 oneAheadInTheWorld = new(11f, 0f, 0f);
        Vector3 oneAlongTheWorldsZ = new(10f, 0f, 1f);
        Vector3 oneToTheLeft = new(-1f, 0f, 0f);

        Assert.Multiple(() =>
        {
            AssertNearly(turned.ToWorld(oneAhead), oneAheadInTheWorld);
            AssertNearly(turned.ToLocal(oneAlongTheWorldsZ), oneToTheLeft);
        });
    }

    [Test]
    public void BringsAPositionInAndBackOut()
    {
        // A turn about all three axes leaves no direction along a world axis, so a direction or a component swapped in
        // either conversion moves the position.
        Vector3 origin = new(3f, -2f, 5f);
        Rotation turnedAboutEveryAxis = Rotation.FromEulerAngles(40f, 30f, 25f);
        Transform turned = new(origin, turnedAboutEveryAxis);
        Vector3 position = new(7f, -3f, 12f);

        Vector3 local = turned.ToLocal(position);

        AssertNearly(turned.ToWorld(local), position);
    }

    [Test]
    public void PlacesALocalFrameInsideItsParent()
    {
        // The parent turns a quarter right and the local transform pitches a quarter down inside it. The two
        // turns share no axis, so their order shows. Turned by the parent first and then pitched about the
        // parent's axes, the placed transform faces down, and the other order ends facing along the world's
        // X.
        Vector3 parentOrigin = new(100f, 0f, 0f);
        Rotation quarterTurnRight = Rotation.FromEulerAngles(0f, 90f, 0f);
        Transform parent = new(parentOrigin, quarterTurnRight);

        Vector3 localOrigin = new(2f, 0f, 5f);
        Rotation quarterTurnDown = Rotation.FromEulerAngles(90f, 0f, 0f);
        Transform local = new(localOrigin, quarterTurnDown);

        Transform placed = parent.ToWorld(local);

        // Two along the parent's Right is two along the world's -Z, and five along its Forward is five along the
        // world's X.
        Vector3 placedOrigin = new(105f, 0f, -2f);
        Vector3 worldDown = new(0f, -1f, 0f);

        Assert.Multiple(() =>
        {
            AssertNearly(placed.Origin, placedOrigin);
            AssertNearly(placed.Forward, worldDown);
        });
    }

    [Test]
    public void ScalesBeforeItTurns()
    {
        // Stretched to twice its length along its own Right, which the quarter turn lays along the world's -Z. A local
        // step along X therefore lands two along -Z rather than being stretched along the world's X.
        Rotation quarterTurnRight = Rotation.FromEulerAngles(0f, 90f, 0f);
        Vector3 twiceAlongRight = new(2f, 1f, 1f);
        Transform stretched = new(Vector3.Zero, quarterTurnRight, twiceAlongRight);
        Vector3 oneAlongX = new(1f, 0f, 0f);

        Vector3 placed = stretched.ToWorld(oneAlongX);

        Vector3 twoAlongTheWorldsBack = new(0f, 0f, -2f);

        Assert.Multiple(() =>
        {
            AssertNearly(placed, twoAlongTheWorldsBack);
            AssertNearly(stretched.ToLocal(placed), oneAlongX);
        });
    }

    [Test]
    public void UndoesItsStretchWhenBringingAPositionIn()
    {
        // No scale is 1, so multiplying a distance by its scale gives another answer than dividing it. No two scales
        // match, so dividing by the wrong axis's scale does too. With no turn each local axis lies along the world's.
        Vector3 differentStretchAlongEachAxis = new(2f, 4f, 8f);
        Transform stretched = new(Vector3.Zero, Rotation.Identity, differentStretchAlongEachAxis);
        Vector3 position = new(2f, 4f, 8f);

        Vector3 oneAlongEachAxis = new(1f, 1f, 1f);

        AssertNearly(stretched.ToLocal(position), oneAlongEachAxis);
    }

    [Test]
    public void ComposesScalesAxisByAxis()
    {
        // Neither transform is turned, which is a case the axis-by-axis product composes exactly. The parent stretches
        // only Y, so the local origin moves along Y alone.
        Vector3 parentScale = new(1f, 2f, 1f);
        Transform parent = new(Vector3.Zero, Rotation.Identity, parentScale);

        Vector3 localOrigin = new(1f, 4f, 2f);
        Vector3 localScale = new(2f, 3f, 1f);
        Transform local = new(localOrigin, Rotation.Identity, localScale);

        Transform placed = parent.ToWorld(local);

        Vector3 multipliedScale = new(2f, 6f, 1f);
        Vector3 originStretchedAlongY = new(1f, 8f, 2f);

        Assert.Multiple(() =>
        {
            AssertNearly(placed.Scale, multipliedScale);
            AssertNearly(placed.Origin, originStretchedAlongY);
        });
    }

    [Test]
    public void MultipliesTheScalesOfAllThreeAxes()
    {
        // Neither transform has a scale of 1 on any axis, so dividing the two scales on an axis gives another answer
        // than multiplying them. Neither transform is turned, a case the axis-by-axis product composes exactly.
        Vector3 parentScale = new(2f, 3f, 4f);
        Transform parent = new(Vector3.Zero, Rotation.Identity, parentScale);

        Vector3 localScale = new(5f, 6f, 7f);
        Transform local = new(Vector3.Zero, Rotation.Identity, localScale);

        Transform placed = parent.ToWorld(local);

        Vector3 multipliedScale = new(10f, 18f, 28f);

        AssertNearly(placed.Scale, multipliedScale);
    }

    [Test]
    public void EqualsAnotherTransformWithTheSameParts()
    {
        Vector3 origin = new(3f, -2f, 5f);
        Rotation quarterTurnRight = Rotation.FromEulerAngles(0f, 90f, 0f);
        Vector3 twiceAlongUp = new(1f, 2f, 1f);
        Transform stretched = new(origin, quarterTurnRight, twiceAlongUp);
        Transform sameStretched = new(origin, quarterTurnRight, twiceAlongUp);

        Assert.Multiple(() =>
        {
            Assert.That(stretched == sameStretched, Is.True);
            Assert.That(stretched != sameStretched, Is.False);
        });

        // The same origin and turn with no stretch, so only Scale tells this transform from the stretched one.
        Vector3 noStretch = new(1f, 1f, 1f);
        Transform unstretched = new(origin, quarterTurnRight, noStretch);

        Assert.Multiple(() =>
        {
            Assert.That(stretched == unstretched, Is.False);
            Assert.That(stretched != unstretched, Is.True);
            Assert.That(stretched.Scale, Is.EqualTo(twiceAlongUp));
            Assert.That(unstretched.Scale, Is.EqualTo(noStretch));
        });
    }

    [Test]
    public void EqualsOnlyATransformWithTheSameOriginTurnAndScale()
    {
        // The moved transform differs from the stretched one in its origin alone, and the unturned one in its turn
        // alone. A comparison that took a match in either part as enough would call one of them equal.
        // EqualsAnotherTransformWithTheSameParts tells two transforms apart by Scale alone.
        Vector3 origin = new(3f, -2f, 5f);
        Rotation quarterTurnRight = Rotation.FromEulerAngles(0f, 90f, 0f);
        Vector3 twiceAlongUp = new(1f, 2f, 1f);
        Transform stretched = new(origin, quarterTurnRight, twiceAlongUp);

        Vector3 oneFurtherAlongX = new(4f, -2f, 5f);
        Transform moved = new(oneFurtherAlongX, quarterTurnRight, twiceAlongUp);
        Transform unturned = new(origin, Rotation.Identity, twiceAlongUp);

        Assert.Multiple(() =>
        {
            Assert.That(stretched == moved, Is.False);
            Assert.That(stretched != moved, Is.True);
            Assert.That(stretched == unturned, Is.False);
            Assert.That(stretched != unturned, Is.True);
        });
    }

    [Test]
    public void EqualsTheSameTransformArrivingBoxed()
    {
        Vector3 origin = new(3f, -2f, 5f);
        Rotation quarterTurnRight = Rotation.FromEulerAngles(0f, 90f, 0f);
        Vector3 twiceAlongUp = new(1f, 2f, 1f);
        object boxedSameStretched = new Transform(origin, quarterTurnRight, twiceAlongUp);
        Transform stretched = new(origin, quarterTurnRight, twiceAlongUp);

        Assert.Multiple(() =>
        {
            Assert.That(stretched.Equals(boxedSameStretched), Is.True);

            // A value of another type is never equal, not even the transform's own origin.
            Assert.That(stretched.Equals(origin), Is.False);
        });
    }

    [Test]
    public void HashesEqualTransformsAlike()
    {
        // Equal transforms hash alike, the one promise a hash makes. No test asks two different transforms to hash
        // differently, since a hash that ignored a part would keep the promise and only collide more often.
        // HashCode.Combine takes a new seed each process, so two different transforms can share a hash in one run and
        // not the next. Such a test would fail in the rare run where the two happen to collide.
        Vector3 origin = new(3f, -2f, 5f);
        Rotation quarterTurnRight = Rotation.FromEulerAngles(0f, 90f, 0f);
        Vector3 twiceAlongUp = new(1f, 2f, 1f);
        Transform stretched = new(origin, quarterTurnRight, twiceAlongUp);
        Transform sameStretched = new(origin, quarterTurnRight, twiceAlongUp);

        Assert.That(stretched.GetHashCode(), Is.EqualTo(sameStretched.GetHashCode()));
    }

    [Test]
    public void ChangesOnlyItsOriginWhenMoved()
    {
        // Turned and stretched, so a move that dropped the turn or the stretch would change Right or Scale.
        Rotation quarterTurnRight = Rotation.FromEulerAngles(0f, 90f, 0f);
        Vector3 twiceAlongUp = new(1f, 2f, 1f);
        Transform stretched = new(Vector3.Zero, quarterTurnRight, twiceAlongUp);
        Vector3 elsewhere = new(3f, -2f, 5f);

        Transform moved = stretched.WithOrigin(elsewhere);

        Assert.Multiple(() =>
        {
            Assert.That(moved.Origin, Is.EqualTo(elsewhere));
            Assert.That(moved.Scale, Is.EqualTo(twiceAlongUp));
            Assert.That(moved.Right, Is.EqualTo(stretched.Right));
        });
    }

    [Test]
    public void PrintsItsThreeParts()
    {
        Assert.That(Transform.Identity.ToString(), Is.EqualTo("(0, 0, 0) turned (0, 0, 0, 1) scaled (1, 1, 1)"));
    }

    private static void AssertNearly(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance));
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance));
        Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(Tolerance));
    }
}
