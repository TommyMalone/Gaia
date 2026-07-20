using Gaia.Core.Considerations;
using Gaia.Core.Options;
using Gaia.Core.Temp;

namespace Gaia.Core.Tests.Helpers;

internal static class TestHelpers
{
    /// <summary>
    ///     Builds an option carrying the given considerations plus a single action that records its lifecycle calls under
    ///     the given name. Init runs last, so an option with no Tuning of its own gets the default one. The caller registers
    ///     the result, keeping option order visible in the spec.
    /// </summary>
    /// <param name="name">The name the option's action records under.</param>
    /// <param name="log">The list the option's action appends to.</param>
    /// <param name="considerations">The considerations the option should carry, if any.</param>
    /// <returns>A StubOption ready to add to a reasoner.</returns>
    internal static OptionBase BuildOption(string name, List<string> log, params ConsiderationBase[] considerations)
    {
        StubOption option = new();

        foreach (ConsiderationBase consideration in considerations)
        {
            option.AddConsideration(consideration);
        }

        option.AddAction(new RecordAction(name, log));

        option.Init(new AiCreationData());

        return option;
    }
}
