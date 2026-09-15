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
        /// <summary>Guards Texts against a read on one thread racing a write on another.</summary>
        /// <remarks>
        ///     Register usually happens while a configuration is loading, and GetText usually happens while debug output is
        ///     being written. The two can easily be on different threads. A Dictionary tolerates concurrent reads but not a read
        ///     alongside a write, and a read that catches a resize part way through returns a wrong answer or loops rather than
        ///     throwing. The lock is a private object nothing else can reach, so no caller outside this class can take it and
        ///     deadlock against this one.
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
        ///         A collision aliases two unrelated names into one, unrecoverable corruption that would otherwise show up as a
        ///         data store answering with the wrong variable's value. It is reported in every build, because a live patch, or
        ///         any content added after development, never runs under an assert.
        ///     </para>
        ///     <para>
        ///         This is process wide, as the table is, so the sink in force is the one the most recently constructed
        ///         <see cref="Factories.GaiaLoader" /> stamped. A host that wants the report somewhere else sets this itself, once
        ///         its loaders are built.
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
        ///     through <see cref="LogSink" /> as an error. Nothing here sits on a hot path. Register is called when an
        ///     <see cref="AiString" /> is made, and comparing two of them afterwards comes down to an integer test that never
        ///     touches the table, so the lock is only ever contended during loading.
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
            ulong hash = 5381;

            foreach (char character in text)
            {
                hash = (hash << 5) + hash + char.ToLowerInvariant(character);
            }

            return hash;
        }
    }
}
