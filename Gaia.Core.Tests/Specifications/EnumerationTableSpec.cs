using Gaia.Core.Specifications;

namespace Gaia.Core.Tests.Specifications;

/// <summary>
///     Covers the table the enumeration readers and descriptions share, its names, values, report list and the types
///     it refuses.
/// </summary>
/// <remarks>
///     Every test builds a fresh table through Build, so what building does is reached on each call and not only by
///     the table the readers share, which is built once for each type.
/// </remarks>
[TestFixture]
public class EnumerationTableSpec
{
    [TestCase(new[] { "Walk", "Run", "Sprint" }, new[] { 0, 1, 2 }, "Walk, Run or Sprint",
        TypeArgs = new[] { typeof(Pace) })]
    [TestCase(new[] { "Only" }, new[] { 0 }, "Only", TypeArgs = new[] { typeof(Lone) })]
    [TestCase(new[] { "Left", "Right" }, new[] { 0, 1 }, "Left or Right", TypeArgs = new[] { typeof(Side) })]
    [TestCase(new[] { "First", "Second" }, new[] { 1, 2 }, "First or Second", TypeArgs = new[] { typeof(Order) })]
    public void ListsTheNamesValuesAndReportOfAType<TEnum>(string[] names, int[] values, string expected)
        where TEnum : struct, Enum
    {
        EnumerationTable<TEnum> table = EnumerationTable<TEnum>.Build();

        Assert.Multiple(() =>
        {
            Assert.That(table.Names, Is.EqualTo(names));
            Assert.That(ValuesOf(table), Is.EqualTo(values));
            Assert.That(table.Expected, Is.EqualTo(expected));
        });
    }

    [TestCase(null, TypeArgs = new[] { typeof(Pace) })]
    [TestCase(
        "The enumeration Mask carries the Flags attribute, whose values are meant to be combined, and an enumeration attribute holds one member's name, so a file could not combine them.",
        TypeArgs = new[] { typeof(Mask) })]
    [TestCase("The enumeration Empty has no members, so an enumeration attribute has no name to hold.",
        TypeArgs = new[] { typeof(Empty) })]
    [TestCase(
        "The enumeration Twins has the members Fast and FAST, whose names differ only in casing, so a file could not tell them apart.",
        TypeArgs = new[] { typeof(Twins) })]
    public void FindsWhatAFileCannotReadInAType<TEnum>(string? problem) where TEnum : struct, Enum
    {
        EnumerationTable<TEnum> table = EnumerationTable<TEnum>.Build();

        Assert.That(table.Problem, Is.EqualTo(problem));
    }

    /// <summary>The table's values as whole numbers, in the table's order.</summary>
    private static int[] ValuesOf<TEnum>(EnumerationTable<TEnum> table) where TEnum : struct, Enum
    {
        int[] values = new int[table.Values.Length];

        for (int index = 0; index < values.Length; index++)
        {
            values[index] = Convert.ToInt32(table.Values[index]);
        }

        return values;
    }

    /// <summary>Three members valued in declared order, as the library's own enumerations are.</summary>
    private enum Pace
    {
        Walk,
        Run,
        Sprint,
    }

    /// <summary>One member, so the report's list is one name with no "or".</summary>
    private enum Lone
    {
        Only,
    }

    /// <summary>Two members, so the report's list is two names joined by "or" alone.</summary>
    private enum Side
    {
        Left,
        Right,
    }

    /// <summary>Declared out of order, so a name read by declared position would take the wrong value.</summary>
    private enum Order
    {
        Second = 2,
        First = 1,
    }

    /// <summary>Values meant to be combined, which one whole name cannot express.</summary>
    [Flags]
    private enum Mask
    {
        Near = 1,
        Far = 2,
    }

    /// <summary>No members, so there is no name for a file to give.</summary>
    private enum Empty
    {
    }

    /// <summary>Two names that differ only in casing, which a reader taking a name in any casing cannot tell apart.</summary>
    private enum Twins
    {
        Fast,
        FAST,
    }
}
