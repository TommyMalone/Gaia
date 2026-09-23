using Gaia.Core.Actions;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.HostSpecs;

/// <summary>A game-defined action built against the public surface alone, recording each selection it receives.</summary>
/// <remarks>
///     This project has no InternalsVisibleTo grant, so the ActionBase hooks, the creation data, the node accessors,
///     the <see cref="DescriptionBuilder" /> and <see cref="ActionBase.NameAttribute" /> this type reaches are the surface
///     a host gets. The description adds the base's Name attribute rather than restating its spelling.
/// </remarks>
internal sealed class HostRecordAction : ActionBase
{
    private static readonly DescriptionBuilder Builder = new DescriptionBuilder(ConceptualAbstraction.Actions,
        "Records each selection it receives.").Add(NameAttribute);

    /// <summary>Describes the optional Done attribute OnInit reads by.</summary>
    private static readonly AttributeDescription DoneAttribute = Builder.Optional("Done", AttributeKind.Bool, "true",
        "Whether the action is done as soon as it is selected.");

    /// <summary>Describes the action for the host specs that register it.</summary>
    internal static readonly ComponentDescription Description = Builder.Build();

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

        if (node.HasAttribute(DoneAttribute.Name))
        {
            if (!node.ReadBool(DoneAttribute.Name, out bool isDone))
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
