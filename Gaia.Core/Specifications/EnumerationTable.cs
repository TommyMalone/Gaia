using System;

namespace Gaia.Core.Specifications
{
    /// <summary>The member names and values of one enumeration, and the list of names a malformed report shows.</summary>
    /// <remarks>
    ///     The names stand in the order Enum.GetNames gives them, by value read as unsigned, and each value at its name's
    ///     index. The shared table is built the first time the type is read or described, so a read afterwards allocates
    ///     nothing, and it is where both the readers and AttributeDescription.Enumeration take the names from, so the choices
    ///     a tool offers are the names a file may hold.
    /// </remarks>
    internal sealed class EnumerationTable<TEnum> where TEnum : struct, Enum
    {
        /// <summary>The table the readers and the description builders share, built once for the type.</summary>
        private static readonly EnumerationTable<TEnum> Shared = Build();

        /// <summary>Keeps what Build worked out from the type.</summary>
        private EnumerationTable(string[] names, TEnum[] values, string expected, string? problem)
        {
            Names = names;
            Values = values;
            Expected = expected;
            Problem = problem;
        }

        /// <summary>The type's member names, in the order Enum.GetNames gives them.</summary>
        internal string[] Names { get; }

        /// <summary>Each member's value, at the index of its name in Names.</summary>
        internal TEnum[] Values { get; }

        /// <summary>The names as a malformed report lists them, "A, B or C", "A or B" or "A".</summary>
        internal string Expected { get; }

        /// <summary>
        ///     What keeps a file from naming the type's members one by one, or null for a type a file can name member by
        ///     member.
        /// </summary>
        internal string? Problem { get; }

        /// <summary>The shared table, for a type a file can name member by member.</summary>
        /// <exception cref="ArgumentException">
        ///     The enumeration carries the Flags attribute, has no members, or has two member
        ///     names that differ only in casing.
        /// </exception>
        internal static EnumerationTable<TEnum> Readable()
        {
            if (Shared.Problem != null)
            {
                throw new ArgumentException(Shared.Problem);
            }

            return Shared;
        }

        /// <summary>Builds a fresh table for the type.</summary>
        /// <remarks>The shared table is built through here once, and the specs call it to build their own.</remarks>
        internal static EnumerationTable<TEnum> Build()
        {
            Type type = typeof(TEnum);
            string[] names = Enum.GetNames(type);
            Array boxedValues = Enum.GetValues(type);
            TEnum[] values = new TEnum[names.Length];

            for (int index = 0; index < names.Length; index++)
            {
                values[index] = (TEnum)boxedValues.GetValue(index)!;
            }

            return new EnumerationTable<TEnum>(names, values, JoinNames(names), FindProblem(type, names));
        }

        /// <summary>Joins the names with commas and an "or" before the last, with no comma before the "or".</summary>
        private static string JoinNames(string[] names)
        {
            if (names.Length == 0)
            {
                return string.Empty;
            }

            string joined = names[0];

            for (int index = 1; index < names.Length; index++)
            {
                string separator = index == names.Length - 1 ? " or " : ", ";
                // Stryker disable once Arithmetic : a mutant of text joined with + does not compile
                joined = joined + separator + names[index];
            }

            return joined;
        }

        /// <summary>Says what keeps a file from naming the type's members one by one, or returns null.</summary>
        private static string? FindProblem(Type type, string[] names)
        {
            if (type.IsDefined(typeof(FlagsAttribute), false))
            {
                return
                    $"The enumeration {type.Name} carries the Flags attribute, whose values are meant to be combined, and an enumeration attribute holds one member's name, so a file could not combine them.";
            }

            if (names.Length == 0)
            {
                return $"The enumeration {type.Name} has no members, so an enumeration attribute has no name to hold.";
            }

            for (int first = 0; first < names.Length; first++)
            {
                for (int second = first + 1; second < names.Length; second++)
                {
                    if (string.Equals(names[first], names[second], StringComparison.OrdinalIgnoreCase))
                    {
                        return
                            $"The enumeration {type.Name} has the members {names[first]} and {names[second]}, whose names differ only in casing, so a file could not tell them apart.";
                    }
                }
            }

            return null;
        }
    }
}
