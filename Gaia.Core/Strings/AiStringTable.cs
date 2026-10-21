using System;
using System.Collections.Generic;
using Gaia.Core.Services.Log;

namespace Gaia.Core.Strings
{
    /// <summary>Holds the text behind every <see cref="AiString" /> that has been created.</summary>
    /// <remarks>
    ///     <para>
    ///         An <see cref="AiString" /> carries only a hash, so the original text has to live somewhere for debug output
    ///         to read it back.
    ///     </para>
    ///     <para>The table is process wide because two strings with the same text are the same string wherever they were made.</para>
    ///     <para>
    ///         Nothing is ever evicted, so the table lives as long as the process. Names built from dynamic strings, such as
    ///         an id appended to a prefix per entity, grow it without bound and do not belong in AiStrings.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.4.1</seealso>
    public static class AiStringTable
    {
        /// <summary>The hash of empty text, which is the value djb2 starts from.</summary>
        internal const ulong EmptyTextHash = 5381;

        /// <summary>Guards Texts against a read on one thread racing a write on another.</summary>
        /// <remarks>
        ///     Register usually happens while a configuration is loading, and GetText usually happens while debug output is
        ///     being written. The two can run on different threads. A Dictionary tolerates concurrent reads but not a read
        ///     alongside a write. A read that overlaps a write can miss a key that is present. A read caught in a chain the write
        ///     is rebuilding throws on the current .NET runtimes, .NET 6 and later, which Godot 4 runs on, and Unity's current
        ///     Mono and IL2CPP, and could loop forever on the .NET Framework's old Dictionary.
        /// </remarks>
        private static readonly object Lock = new();

        /// <summary>The text behind every registered hash.</summary>
        private static readonly Dictionary<ulong, string> Texts = new();

        /// <summary>The channel a collision is reported on, held so that reporting one registers nothing itself.</summary>
        private static readonly AiString GaiaChannel;

        /// <summary>Makes the channel once Lock and Texts exist, since making an AiString registers its text.</summary>
        static AiStringTable()
        {
            GaiaChannel = "Gaia";
        }

        /// <summary>Where a hash collision is reported. Replaceable so a host routes it to its own console.</summary>
        /// <remarks>
        ///     <para>
        ///         A collision gives two unrelated names the same hash, so every lookup keyed on them treats them as one name,
        ///         and nothing afterwards can tell them apart. Without the report, it would show only in what the lookups answer,
        ///         a data store handing back the wrong variable's value or a factory building the wrong component for an element.
        ///         It is reported through this sink in every build rather than by an assert, because a collision depends on which
        ///         names exist, so two names can first collide after the game ships, when a patch or downloadable content adds
        ///         one, and a release build runs no asserts.
        ///     </para>
        ///     <para>
        ///         This is process wide, as the table is, so the sink in force is the one the most recently constructed
        ///         <see cref="Factories.GaiaLoader" /> set. A host that wants the report somewhere else sets this itself, once its
        ///         loaders are built.
        ///     </para>
        /// </remarks>
        public static ILogSink LogSink { get; set; } = new TraceLogSink();

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
        ///     through <see cref="LogSink" /> as an error. The same text in another casing is the same string, so the table keeps
        ///     the casing registered first and GetText reports that one. A name registered once and kept as a static
        ///     <see cref="AiString" /> never comes back here, and comparing two of them never touches the table, so a cycle takes
        ///     the lock only in <see cref="GetText" />, when logging reads text back.
        /// </remarks>
        internal static ulong Register(string text)
        {
            ulong hash = ComputeHash(text);
            string? collidingText = null;

            lock (Lock)
            {
                if (Texts.TryGetValue(hash, out string? registeredText))
                {
                    if (!string.Equals(text, registeredText, StringComparison.OrdinalIgnoreCase))
                    {
                        collidingText = registeredText;
                    }
                }
                else
                {
                    Texts[hash] = text;
                }
            }

            // Reported outside the lock, since a sink is host code that must not run under it.
            if (collidingText != null)
            {
                LogSink.LogError(GaiaChannel, $"the strings \"{text}\" and \"{collidingText}\" hash to the same value");
            }

            return hash;
        }

        /// <summary>The djb2 hash, over the lower cased text so that comparisons ignore case.</summary>
        /// <seealso>djb2, described at https://www.cse.yorku.ca/~oz/hash.html</seealso>
        private static ulong ComputeHash(string text)
        {
            ulong hash = EmptyTextHash;

            foreach (char character in text)
            {
                hash = (hash << 5) + hash + char.ToLowerInvariant(character);
            }

            return hash;
        }
    }
}
