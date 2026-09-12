using System;
using Gaia.Core.Blackboards;
using Gaia.Core.Brains;
using Gaia.Core.Entities;
using Gaia.Core.Services.Log;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Factories
{
    /// <summary>
    ///     Builds a brain and the whole tree beneath it from configuration, carrying the factory set and the services
    ///     every load shares. A load starts at a Brain element, which in a single-brain document is the document's own root.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The loader is constructed once with the game's factory set and services and then loads any number of brains.
    ///         They may come from as many documents as the game has, and one document may be loaded over and over, so five
    ///         hundred wolves from one wolf.xml are five hundred independent brains, each with its own blackboard and its own
    ///         random source.
    ///     </para>
    ///     <para>
    ///         Randomness is per brain. The loader's own source is a master stream, and a brain whose load is handed no
    ///         source of its own gets a <see cref="SystemRandomSource" /> seeded from the master's next value, so one
    ///         character's draws never shift another's and brains can tick on parallel threads. Each such load takes one value
    ///         from the master, so the master seed reproduces a brain's stream only when the loads before it happen in the
    ///         same order. A loader given no random source rolls a master seed and reports it through the sink. A fleet is
    ///         every brain one loader built, and any fleet can be reproduced by constructing a
    ///         <see cref="SystemRandomSource" /> with the reported seed and loading in the same order.
    ///     </para>
    /// </remarks>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public sealed class GaiaLoader
    {
        private static readonly AiString GaiaChannel = "Gaia";

        private readonly FactorySet _factories;
        private readonly ILogSink _logSink;
        private readonly IRandomSource _randomSource;
        private readonly XmlSpecificationReader _reader;
        private readonly ITimeSource _timeSource;

        /// <summary>Creates a loader over the given factories and services.</summary>
        /// <param name="factories">The factory set every load builds through, the game's own registered types included.</param>
        /// <param name="timeSource">
        ///     The clock the host advances once per tick of its own loop, read by every loaded component that
        ///     measures time. It is required, because a brain whose clock is never advanced holds its cooldowns and pauses at zero
        ///     forever, with no symptom beyond behavior that looks mistuned.
        /// </param>
        /// <param name="randomSource">The master stream brain sources are seeded from, or null to roll and report a master seed.</param>
        /// <param name="logSink">Where loading and the loaded brains report, or null for <see cref="TraceLogSink" />.</param>
        public GaiaLoader(
            FactorySet factories,
            ITimeSource timeSource,
            IRandomSource? randomSource = null,
            ILogSink? logSink = null)
        {
            _factories = factories;
            _timeSource = timeSource;
            _logSink = logSink ?? new TraceLogSink();
            _reader = new XmlSpecificationReader(_logSink);

            if (randomSource == null)
            {
                int seed = Environment.TickCount;
                _randomSource = new SystemRandomSource(seed);
                _logSink.LogMessage(GaiaChannel, $"random seed {seed}");
            }
            else
            {
                _randomSource = randomSource;
            }
        }

        /// <summary>Loads one brain from configuration text, parsing and loading in one call.</summary>
        /// <param name="configurationText">The configuration document's text.</param>
        /// <param name="sourceDocumentName">The name loading errors are filed under, usually the file's name.</param>
        /// <param name="brain">The loaded brain, or null when loading failed.</param>
        /// <param name="worldBlackboard">The blackboard every character shares, or null when the game keeps none.</param>
        /// <param name="self">
        ///     The actor the brain belongs to, or null. A load without one leaves every
        ///     <see cref="Targets.SelfTarget" /> in the brain empty and its log channel unnamed, and nothing sets them later.
        /// </param>
        /// <param name="brainBlackboard">
        ///     The blackboard the brain keeps, or null to make its own. A host swapping a character's
        ///     brain passes the old brain's, so the character keeps what it knew.
        /// </param>
        /// <param name="randomSource">
        ///     The brain's own source, or null to derive one from the loader's master stream. Handing one
        ///     in is for scripting a single brain's rolls.
        /// </param>
        /// <returns>True when the brain loaded. False after reporting what went wrong.</returns>
        public bool TryLoadBrain(
            string configurationText,
            string sourceDocumentName,
            out Brain? brain,
            WorldBlackboard? worldBlackboard = null,
            Actor? self = null,
            BrainBlackboard? brainBlackboard = null,
            IRandomSource? randomSource = null)
        {
            brain = null;

            if (!TryParse(configurationText, sourceDocumentName, out SpecificationNode? root))
            {
                return false;
            }

            return TryLoadBrain(root!, out brain, worldBlackboard, self, brainBlackboard, randomSource);
        }

        /// <summary>Loads one brain from a parsed tree.</summary>
        /// <param name="root">
        ///     The Brain element to load, from the tree <see cref="TryParse" /> builds. The loader does not check
        ///     the element's name.
        /// </param>
        /// <param name="brain">The loaded brain, or null when loading failed.</param>
        /// <param name="worldBlackboard">The blackboard every character shares, or null when the game keeps none.</param>
        /// <param name="self">
        ///     The actor the brain belongs to, or null. A load without one leaves every
        ///     <see cref="Targets.SelfTarget" /> in the brain empty and its log channel unnamed, and nothing sets them later.
        /// </param>
        /// <param name="brainBlackboard">
        ///     The blackboard the brain keeps, or null to make its own. A host swapping a character's
        ///     brain passes the old brain's, so the character keeps what it knew.
        /// </param>
        /// <param name="randomSource">
        ///     The brain's own source, or null to derive one from the loader's master stream. Handing one
        ///     in is for scripting a single brain's rolls.
        /// </param>
        /// <returns>True when the brain loaded. False after reporting what went wrong.</returns>
        public bool TryLoadBrain(
            SpecificationNode root,
            out Brain? brain,
            WorldBlackboard? worldBlackboard = null,
            Actor? self = null,
            BrainBlackboard? brainBlackboard = null,
            IRandomSource? randomSource = null)
        {
            brain = null;

            AiCreationData creationData = new(root, _factories)
            {
                LogSink = _logSink,
                TimeSource = _timeSource,
                RandomSource = randomSource ?? new SystemRandomSource(_randomSource.NextInt()),
                WorldBlackboard = worldBlackboard,
                Self = self,
            };

            Brain loaded = new();

            if (brainBlackboard != null)
            {
                loaded.Blackboard = brainBlackboard;
            }

            if (!loaded.Init(creationData))
            {
                return false;
            }

            brain = loaded;
            return true;
        }

        /// <summary>Parses configuration text into the tree the root overload loads from.</summary>
        /// <remarks>
        ///     Loading reads the tree and leaves it as it was, so a document loaded many times is parsed once through here
        ///     and every brain is loaded from the same root.
        /// </remarks>
        /// <param name="configurationText">The configuration document's text.</param>
        /// <param name="sourceDocumentName">The name loading errors are filed under, usually the file's name.</param>
        /// <param name="root">The parsed root, or null when the document did not parse.</param>
        /// <returns>True when the document parsed. False after reporting what went wrong.</returns>
        public bool TryParse(string configurationText, string sourceDocumentName, out SpecificationNode? root)
        {
            return _reader.TryRead(configurationText, sourceDocumentName, out root);
        }
    }
}
