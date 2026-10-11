using Gaia.Core.Strings;

namespace Gaia.Core.Tests.Strings;

/// <summary>Covers <see cref="AiString" />, what a default one is and the hash a text reduces to.</summary>
[TestFixture]
public class AiStringSpec
{
    [Test]
    public void TreatsADefaultAsTheEmptyString()
    {
        AiString neverAssigned = default;
        Dictionary<AiString, int> keyedByEmpty = new() { [AiString.Empty] = 1 };

        Assert.Multiple(() =>
        {
            Assert.That(neverAssigned.IsEmpty, Is.True);
            Assert.That(neverAssigned == AiString.Empty, Is.True);
            Assert.That(neverAssigned.Equals(AiString.Empty), Is.True);
            Assert.That(neverAssigned.Hash, Is.EqualTo(5381UL));
            Assert.That(neverAssigned.Text, Is.EqualTo(string.Empty));
            Assert.That(keyedByEmpty.ContainsKey(neverAssigned), Is.True);
        });
    }

    [Test]
    public void KeepsTheHashItsTextReducesTo()
    {
        // djb2 starts at 5381 and multiplies by 33 and adds each lower-cased character, so "Ab" reduces to
        // (5381 * 33 + 97) * 33 + 98.
        AiString mixedCase = new("Ab");

        Assert.Multiple(() =>
        {
            Assert.That(mixedCase.Hash, Is.EqualTo(5863208UL));
            Assert.That(AiString.Empty.Hash, Is.EqualTo(5381UL));
        });
    }
}
