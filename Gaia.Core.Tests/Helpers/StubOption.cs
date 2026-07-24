using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Options;
using Gaia.Core.Temp;

namespace Gaia.Core.Tests.Helpers;

/// <summary>
///     A concrete option for specs to build by hand. OnInit returns true because there is no configuration to load.
///     Returning false would short-circuit Init before EnsureTuningConsideration supplies the default
///     <see cref="TuningConsideration" />.
/// </summary>
internal sealed class StubOption : OptionBase
{
    /// <summary>Creates an option holding the given considerations and actions, with Init already run.</summary>
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

        AiCreationData creationData = new();
        Init(creationData);
    }

    /// <summary>Reports success despite loading nothing, which lets Init finish and add the default tuning.</summary>
    protected override bool OnInit(AiCreationData creationData)
    {
        return true;
    }
}
