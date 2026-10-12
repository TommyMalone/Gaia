using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Helpers;

/// <summary>A group whose answer is set by hand and which records what it saw when the base asked it.</summary>
internal sealed class RecordingGroupConsideration : GroupConsiderationBase
{
    /// <summary>Creates a group that holds nothing until a configuration loads.</summary>
    internal RecordingGroupConsideration()
    {
    }

    /// <summary>Creates a group that holds the given considerations.</summary>
    internal RecordingGroupConsideration(IEnumerable<ConsiderationBase> considerations) : base(considerations)
    {
    }

    /// <summary>What CombineWeights reports, or null to report Totals.</summary>
    internal WeightValues? Answer { get; set; }

    /// <summary>What OnGroupInit reports, true unless a spec sets it.</summary>
    internal bool GroupInitAnswer { get; set; } = true;

    /// <summary>How many considerations the group held when OnGroupInit ran, or -1 until it runs.</summary>
    internal int CountAtGroupInit { get; private set; } = -1;

    /// <summary>The first held consideration's addend when CombineWeights last ran.</summary>
    internal float FirstAddendAtCombine { get; private set; }

    /// <summary>Totals as CombineWeights last read it.</summary>
    internal WeightValues TotalsAtCombine { get; private set; }

    /// <summary>The sum of every held addend CombineWeights has read by index, across every time it was asked.</summary>
    internal float AddendSum { get; private set; }

    /// <summary>Records how many considerations are held, then reports GroupInitAnswer.</summary>
    protected override bool OnGroupInit(AiCreationData creationData)
    {
        CountAtGroupInit = Considerations.Count;
        return GroupInitAnswer;
    }

    /// <summary>Records what it reads, then reports Answer, or Totals when Answer is null.</summary>
    protected override WeightValues CombineWeights()
    {
        if (Considerations.Count > 0)
        {
            FirstAddendAtCombine = Considerations[0].Weights.Addend;
        }

        TotalsAtCombine = Totals;

        for (int index = 0; index < Considerations.Count; index++)
        {
            AddendSum += Considerations[index].Weights.Addend;
        }

        return Answer ?? Totals;
    }
}
