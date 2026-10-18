using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Spatial;

/// <summary>Covers <see cref="Vector3" />, its cross product and its value equality.</summary>
/// <remarks>
///     The equality asserts are exact because every component here is a whole number, which products and differences
///     of whole numbers keep. The vector's other arithmetic is reached by the specs of the shapes and transforms that use
///     it.
/// </remarks>
[TestFixture]
public class Vector3Spec
{
    [Test]
    public void CrossesTwoVectorsOffTheAxes()
    {
        // No component of either vector is zero, so every term of each component counts, and a sign swapped in any of
        // them gives another answer. Two vectors along the axes would leave most terms multiplying a zero.
        Vector3 left = new(1f, 2f, 3f);
        Vector3 right = new(4f, 5f, 6f);

        Assert.That(Vector3.Cross(left, right), Is.EqualTo(new Vector3(-3f, 6f, -3f)));
    }

    [Test]
    public void EqualsOnlyAVectorWithTheSameThreeComponents()
    {
        Vector3 value = new(1f, 2f, 3f);
        Vector3 same = new(1f, 2f, 3f);
        Vector3[] differingInOneComponent = { new(9f, 2f, 3f), new(1f, 9f, 3f), new(1f, 2f, 9f) };

        Assert.Multiple(() =>
        {
            Assert.That(value.Equals(same), Is.True);
            Assert.That(value.Equals((object)same), Is.True, "equality survives boxing");
            Assert.That(value == same, Is.True);
            Assert.That(value != same, Is.False);

            foreach (Vector3 other in differingInOneComponent)
            {
                Assert.That(value.Equals(other), Is.False, $"{other} through Equals");
                Assert.That(value.Equals((object)other), Is.False, $"{other} through Equals(object)");
                Assert.That(value == other, Is.False, $"{other} through ==");
                Assert.That(value != other, Is.True, $"{other} through !=");
            }
        });
    }
}
