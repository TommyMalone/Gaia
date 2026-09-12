using Gaia.Core.Actions;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.HostSpecs;

/// <summary>
///     A game-defined action built against the public surface alone, recording each selection it receives. This is
///     the extension path a real game takes for actions it defines.
/// </summary>
/// <remarks>
///     This project has no InternalsVisibleTo grant, so the ActionBase hooks, the creation data and the node
///     accessors this type reaches are the surface a host gets.
/// </remarks>
internal sealed class HostRecordAction : ActionBase
{
    private static readonly AiString DoneAttribute = "Done";

    private readonly List<string> _selections;

    private bool _isDone = true;

    /// <summary>Creates an action for the factories to build, appending each selection to the given list.</summary>
    internal HostRecordAction(List<string> selections)
    {
        _selections = selections;
    }

    /// <summary>Creates a named action for hand assembly, appending each selection to the given list.</summary>
    internal HostRecordAction(AiString name, List<string> selections)
    {
        Name = name;
        _selections = selections;
    }

    /// <inheritdoc />
    public override bool IsDone => _isDone;

    /// <summary>Takes an optional Done attribute, so a configuration can mark an action that keeps running.</summary>
    protected override bool OnInit(AiCreationData creationData)
    {
        SpecificationNode node = creationData.Node;

        if (node.HasAttribute(DoneAttribute))
        {
            if (!node.ReadBool(DoneAttribute, out bool isDone))
            {
                return false;
            }

            _isDone = isDone;
        }

        return true;
    }

    /// <summary>Records the action's name.</summary>
    protected override void OnSelect()
    {
        _selections.Add(Name.Text);
    }
}
