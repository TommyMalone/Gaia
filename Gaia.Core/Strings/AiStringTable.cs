using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Gaia.Core.Strings
{
    /// <summary>Holds the text behind every <see cref="AiString" /> that has been created.</summary>
    /// <remarks>
    ///     <para>
    ///         An <see cref="AiString" /> carries only a hash, so the original text has to live somewhere for debug output
    ///         to read it back.
    ///     </para>
    ///     <para>The table is process wide because two strings with the same text are the same string wherever they were made.</para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.1</seealso>
    public static class AiStringTable
    {
        /// <summary>Guards Texts against a read on one thread racing a write on another.</summary>
        /// <remarks>
        ///     Register usually happens while a configuration is loading, and GetText usually happens while debug output is
        ///     being written. This makes it so that the two can easily land on different threads. A Dictionary tolerates
        ///     concurrent reads but not a read alongside a write, and a read that catches a resize part way through returns a
        ///     wrong answer or loops rather than throwing. The lock is a private object nothing else can reach, so no caller
        ///     outside this class can take it and deadlock against this one.
        /// </remarks>
        private static readonly object Lock = new();

        /// <summary>The text behind every registered hash.</summary>
        private static readonly Dictionary<ulong, string> Texts = new();

        /// <summary>How many distinct strings have been registered.</summary>
        public static int Count
        {
            get
            {
                lock (Lock)
                {
                    return Texts.Count;
                }
            }
        }

        /// <summary>Reports the text a hash was made from.</summary>
        /// <returns>The registered text, or an empty string if the hash is not in the table.</returns>
        public static string GetText(ulong hash)
        {
            lock (Lock)
            {
                return Texts.TryGetValue(hash, out string? text) ? text : string.Empty;
            }
        }

        /// <summary>Hashes the given text and records it, so that the text can be read back later.</summary>
        /// <remarks>
        ///     Two different texts can hash to the same value, so a hash that already belongs to other text is reported
        ///     through ReportAnyCollision. Nothing here sits on a hot path. Register is called when an <see cref="AiString" /> is
        ///     made, and comparing two of them afterwards comes down to an integer test that never touches the table, so the lock
        ///     is only ever contended during loading.
        /// </remarks>
        internal static ulong Register(string text)
        {
            ulong hash = ComputeHash(text);

            lock (Lock)
            {
                if (Texts.TryGetValue(hash, out string? registeredText))
                {
                    ReportAnyCollision(text, registeredText);
                }
                else
                {
                    Texts[hash] = text;
                }
            }

            return hash;
        }

        /// <summary>The djb2 hash, over the lower cased text so that comparisons ignore case.</summary>
        /// <seealso>djb2, described at https://www.cse.yorku.ca/~oz/hash.html</seealso>
        private static ulong ComputeHash(string text)
        {
            ulong hash = 5381;

            foreach (char character in text)
            {
                hash = (hash << 5) + hash + char.ToLowerInvariant(character);
            }

            return hash;
        }

        /// <summary>Reports two different strings hashing to the same value.</summary>
        /// <remarks>Conditional, so that a release build spends nothing on the comparison.</remarks>
        [Conditional("DEBUG")]
        private static void ReportAnyCollision(string text, string registeredText)
        {
            Debug.Assert(string.Equals(text, registeredText, StringComparison.OrdinalIgnoreCase),
                $"The strings {text} and {registeredText} hash to the same value.");
        }
    }
}
