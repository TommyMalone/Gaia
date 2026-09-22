using Gaia.Core.Actions;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.HostSpecs;

/// <summary>A game-defined action built against the public surface alone, recording each selection it receives.</summary>
/// <remarks>
///     This project has no InternalsVisibleTo grant, so the ActionBase hooks, the creation data, the node accessors
///     and the description builders this type reaches are the surface a host gets. The description names Name itself,
///     since the base's own description of it is internal.
/// </remarks>
internal sealed class HostRecordAction : ActionBase
{
    private static readonly AiString DoneAttribute = "Done";

    /// <summary>Describes the action for the host specs that register it.</summary>
    internal static readonly ComponentDescription Description = new(ConceptualAbstraction.Actions,
        "Records each selection it receives.", new[]
        {
            AttributeDescription.Required("Name", "Name", AttributeKind.String,
                "What the action is called in the log."),
            AttributeDescription.Optional(DoneAttribute, "Done", AttributeKind.Bool, "true",
                "Whether the action is done as soon as it is selected."),
        }, Array.Empty<SlotDescription>());

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
