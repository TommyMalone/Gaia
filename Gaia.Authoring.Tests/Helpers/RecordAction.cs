using Gaia.Core.Actions;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;

namespace Gaia.Authoring.Tests.Helpers;

/// <summary>An action a spec registers under a type the shipped factory set lacks, recording each selection it receives.</summary>
/// <remarks>
///     The description names Name and Done, the two attributes the shipped files' Record actions carry, since a load
///     refuses an attribute the description does not name.
/// </remarks>
internal sealed class RecordAction : ActionBase
{
    private static readonly DescriptionBuilder Builder = new DescriptionBuilder(ConceptualAbstraction.Actions,
        "Records each selection it receives.").Add(NameAttribute);

    /// <summary>Describes the optional Done attribute OnInit reads by.</summary>
    private static readonly AttributeDescription DoneAttribute = Builder.Optional("Done", AttributeKind.Bool, "true",
        "Whether the action is done as soon as it is selected.");

    /// <summary>Describes the action for the specs that register it.</summary>
    internal static readonly ComponentDescription Description = Builder.Build();

    private readonly List<string> _selections;

    private bool _isDone = true;

    /// <summary>Creates an action for the factories to build, appending each selection to the given list.</summary>
    internal RecordAction(List<string> selections)
    {
        _selections = selections;
    }

    /// <inheritdoc />
    public override bool IsDone => _isDone;

    /// <summary>Takes an optional Done attribute, so a document can mark an action that keeps running.</summary>
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
