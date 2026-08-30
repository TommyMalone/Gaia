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
///     The option is assembled rather than loaded, so it calls EnsureTuningConsideration itself instead of running an
///     Init that has no configuration to read. The OnInit override exists because <see cref="OptionBase" /> requires one
///     of every concrete option, and reports the only thing an option with nothing to load can report.
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

        // Assembled rather than loaded. Init is not called, there is no configuration to read, and the default
        // tuning Init would have ensured is ensured directly instead.
        EnsureTuningConsideration();
    }

    /// <summary>Reports success, since an option assembled in code has nothing to load.</summary>
    protected override bool OnInit(AiCreationData creationData)
    {
        return true;
    }
}
