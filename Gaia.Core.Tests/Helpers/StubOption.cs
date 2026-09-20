using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Options;

namespace Gaia.Core.Tests.Helpers;

/// <summary>
///     A concrete option for specs to build by hand, finished with the default <see cref="TuningConsideration" />
///     that <see cref="OptionBase.Init" /> would otherwise have supplied.
/// </summary>
/// <remarks>
///     The option is assembled rather than loaded, so it never runs Init. It calls EnsureTuningConsideration itself
///     so that a spec can read the option's weight before any reasoner has enabled it. The OnInit override exists because
///     <see cref="OptionBase" /> requires one of every concrete option, and it refuses, the way the other hand-built
///     helpers do.
/// </remarks>
internal sealed class StubOption : OptionBase
{
    /// <summary>Creates an option holding the given considerations and actions, ready to be scored.</summary>
    internal StubOption(ConsiderationBase[] considerations, ActionBase[] actions)
    {
        foreach (ConsiderationBase consideration in considerations)
        {
            AddConsideration(consideration);
        }

        foreach (ActionBase action in actions)
        {
            AddAction(action);
        }

        // Assembled rather than loaded, so Init is not called. The default tuning is ensured here rather than at
        // enable, so a spec can read the option's weight without a reasoner.
        EnsureTuningConsideration();
    }

    /// <inheritdoc />
    protected override bool OnInit(AiCreationData creationData)
    {
        // Built in code with its values set by hand, so a load is a spec mistake and is reported as one.
        return false;
    }
}
