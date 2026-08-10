using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Spatial;

/// <summary>Covers <see cref="Rotation" />, its direction vectors, its Euler report and its value equality.</summary>
/// <remarks>
///     The rotations under test are built from single-axis half-angle quaternions and combined with a hand-rolled
///     Hamilton product at the bottom of the fixture, so the Euler report is checked against an independent construction
///     rather than against itself. Angle and direction asserts are tolerant because the trigonometry does not come out
///     exact in floats.
/// </remarks>
[TestFixture]
public class RotationSpec
{
    [SetUp]
    public void BuildWorldAxes()
    {
        _worldRight = new Vector3(1f, 0f, 0f);
        _worldUp = new Vector3(0f, 1f, 0f);
        _worldForward = new Vector3(0f, 0f, 1f);
    }

    private const float Tolerance = 0.001f;

    private Vector3 _worldRight;
    private Vector3 _worldUp;
    private Vector3 _worldForward;

    [Test]
    public void PointsAlongTheWorldAxesWithNoRotation()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Rotation.Identity.Forward, Is.EqualTo(_worldForward));
            Assert.That(Rotation.Identity.Right, Is.EqualTo(_worldRight));
            Assert.That(Rotation.Identity.Up, Is.EqualTo(_worldUp));
        });
    }

    [Test]
    public void TurnsItsDirectionsWithAQuarterTurnToTheRight()
    {
        // Facing what used to be right, the right hand points back the way the world calls backward.
        Vector3 worldBack = new(0f, 0f, -1f);
        Rotation quarterTurnRight = YawOf(90f);

        Assert.Multiple(() =>
        {
            AssertNearly(quarterTurnRight.Forward, _worldRight);
            AssertNearly(quarterTurnRight.Right, worldBack);
            AssertNearly(quarterTurnRight.Up, _worldUp);
        });
    }

    [Test]
    public void TiltsItsDirectionsWithAQuarterTurnDown()
    {
        // A positive pitch tilts the view toward the ground, so a quarter turn leaves forward straight down and up
        // pointing where forward used to.
        Vector3 worldDown = new(0f, -1f, 0f);
        Rotation quarterTurnDown = PitchOf(90f);

        Assert.Multiple(() =>
        {
            AssertNearly(quarterTurnDown.Forward, worldDown);
            AssertNearly(quarterTurnDown.Up, _worldForward);
            AssertNearly(quarterTurnDown.Right, _worldRight);
        });
    }

    [Test]
    public void ReportsEachSingleAxisTurnInItsOwnSlot()
    {
        Vector3 pitchAlone = new(40f, 0f, 0f);
        Vector3 yawAlone = new(0f, 30f, 0f);
        Vector3 rollAlone = new(0f, 0f, 25f);

        Assert.Multiple(() =>
        {
            AssertNearly(PitchOf(40f).ToEulerAngles(), pitchAlone);
            AssertNearly(YawOf(30f).ToEulerAngles(), yawAlone);
            AssertNearly(RollOf(25f).ToEulerAngles(), rollAlone);
        });
    }

    [Test]
    public void RecoversTheAnglesACombinedTurnWasBuiltFrom()
    {
        // Yaw, then pitch, then roll, which is the order the report promises to describe.
        Rotation combined = Multiply(Multiply(YawOf(30f), PitchOf(40f)), RollOf(25f));
        Vector3 anglesBuiltFrom = new(40f, 30f, 25f);

        AssertNearly(combined.ToEulerAngles(), anglesBuiltFrom);
    }

    [Test]
    public void ReportsTheWholeTurnAsYawNearStraightUpAndDown()
    {
        // With forward on the vertical axis there is no single split between yaw and roll, and the report resolves
        // the ambiguity by calling all of it yaw.
        Rotation straightDown = Multiply(YawOf(30f), PitchOf(90f));
        Rotation straightUp = Multiply(YawOf(30f), PitchOf(-90f));
        Vector3 downWithYawOnly = new(90f, 30f, 0f);
        Vector3 upWithYawOnly = new(-90f, 30f, 0f);

        Assert.Multiple(() =>
        {
            AssertNearly(straightDown.ToEulerAngles(), downWithYawOnly);
            AssertNearly(straightUp.ToEulerAngles(), upWithYawOnly);
        });
    }

    [Test]
    public void FoldsRollIntoYawWhenPointingStraightDown()
    {
        // Pitched a quarter turn down, the roll axis lies along the world vertical, where rolling turns against the
        // yaw. A yaw of 30 and a roll of 20 amount to one turn of 10, reported entirely as yaw.
        Rotation rolledStraightDown = Multiply(Multiply(YawOf(30f), PitchOf(90f)), RollOf(20f));
        Vector3 oneFoldedTurn = new(90f, 10f, 0f);

        AssertNearly(rolledStraightDown.ToEulerAngles(), oneFoldedTurn);
    }

    [Test]
    public void EqualsAnotherRotationWithTheSameComponents()
    {
        Rotation facing = new(0f, 0.6f, 0f, 0.8f);
        Rotation sameFacing = new(0f, 0.6f, 0f, 0.8f);

        Assert.Multiple(() =>
        {
            Assert.That(facing == sameFacing, Is.True);
            Assert.That(facing != sameFacing, Is.False);
        });

        Rotation oppositeFacing = new(facing.X, -facing.Y, facing.Z, facing.W);

        Assert.Multiple(() =>
        {
            Assert.That(facing == oppositeFacing, Is.False);
            Assert.That(facing != oppositeFacing, Is.True);
            Assert.That(facing.Y, Is.EqualTo(0.6f));
            Assert.That(oppositeFacing.Y, Is.EqualTo(-0.6f));
        });
    }

    [Test]
    public void EqualsTheSameRotationArrivingBoxed()
    {
        object boxedSameFacing = new Rotation(0f, 0.6f, 0f, 0.8f);
        Rotation facing = new(0f, 0.6f, 0f, 0.8f);

        Assert.Multiple(() =>
        {
            Assert.That(facing.Equals(boxedSameFacing), Is.True);

            // A value of another type is never equal, whatever numbers it holds.
            Assert.That(facing.Equals(_worldUp), Is.False);
        });
    }

    [Test]
    public void HashesEqualRotationsAlike()
    {
        Rotation facing = new(0f, 0.6f, 0f, 0.8f);
        Rotation sameFacing = new(0f, 0.6f, 0f, 0.8f);

        Assert.That(facing.GetHashCode(), Is.EqualTo(sameFacing.GetHashCode()));
    }

    [Test]
    public void PrintsItsFourComponents()
    {
        Assert.That(Rotation.Identity.ToString(), Is.EqualTo("(0, 0, 0, 1)"));
    }

    private static Rotation YawOf(float degrees)
    {
        // A quaternion stores half its rotation angle, here around the Y axis.
        float halfAngle = degrees * MathF.PI / 360f;

        return new Rotation(0f, MathF.Sin(halfAngle), 0f, MathF.Cos(halfAngle));
    }

    private static Rotation PitchOf(float degrees)
    {
        float halfAngle = degrees * MathF.PI / 360f;

        return new Rotation(MathF.Sin(halfAngle), 0f, 0f, MathF.Cos(halfAngle));
    }

    private static Rotation RollOf(float degrees)
    {
        float halfAngle = degrees * MathF.PI / 360f;

        return new Rotation(0f, 0f, MathF.Sin(halfAngle), MathF.Cos(halfAngle));
    }

    /// <summary>
    ///     Composes two rotations with the Hamilton product, under which yaw times pitch times roll builds the turns in
    ///     the order the Euler report describes.
    /// </summary>
    private static Rotation Multiply(Rotation left, Rotation right)
    {
        return new Rotation(left.W * right.X + left.X * right.W + left.Y * right.Z - left.Z * right.Y,
            left.W * right.Y - left.X * right.Z + left.Y * right.W + left.Z * right.X,
            left.W * right.Z + left.X * right.Y - left.Y * right.X + left.Z * right.W,
            left.W * right.W - left.X * right.X - left.Y * right.Y - left.Z * right.Z);
    }

    private static void AssertNearly(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance));
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance));
        Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(Tolerance));
    }
}
