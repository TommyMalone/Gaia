using Gaia.Core.Actions;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;

namespace Gaia.Core.Tests.Helpers;

/// <summary>
///     An action that records every lifecycle call it receives as "Name.Phase". Stands in for a game's own actions,
///     such as Move or FireWeapon. Recording Select, Deselect, and Update separately is what lets a spec distinguish an
///     option that kept running from one that was torn down and restarted.
/// </summary>
/// <remarks>Specs that compare whole logs write the expected entries grouped a cycle per line.</remarks>
internal sealed class RecordAction : ActionBase
{
    private static readonly DescriptionBuilder Builder = new DescriptionBuilder(ConceptualAbstraction.Actions,
        "Records each lifecycle call it receives.").Add(NameAttribute);

    /// <summary>Describes the optional Done attribute OnInit reads by.</summary>
    private static readonly AttributeDescription DoneAttribute = Builder.Optional("Done", AttributeKind.Bool, "true",
        "Whether the action is done as soon as it is selected.");

    /// <summary>Describes the action for a spec that registers it, the Name every action reads and the optional Done.</summary>
    internal static readonly ComponentDescription Description = Builder.Build();

    private readonly List<string> _log;
    private bool _isDone;

    /// <summary>
    ///     Creates an action a spec registers on a <see cref="FactorySet" /> for loading to build by type name, appending
    ///     to the given log under its loaded Name.
    /// </summary>
    /// <param name="log">The shared list every lifecycle call is appended to. The spec owns it and reads it back.</param>
    internal RecordAction(List<string> log)
    {
        _log = log;
        _isDone = true;
    }

    /// <summary>Creates an action that appends to the given log under the given name.</summary>
    /// <param name="name">The prefix each recorded entry is written with, as name.Phase.</param>
    /// <param name="log">The shared list every lifecycle call is appended to. The spec owns it and reads it back.</param>
    /// <param name="isDone">
    ///     The initial IsDone. Defaults to true, so the option is done as soon as it is selected. Pass false
    ///     for an action that keeps its option running until SetDone is called.
    /// </param>
    internal RecordAction(string name, List<string> log, bool isDone = true)
    {
        _log = log;
        _isDone = isDone;

        Name = name;
    }

    /// <summary>Whatever the constructor, a loaded Done attribute or SetDone last set.</summary>
    public override bool IsDone => _isDone;

    /// <summary>Takes an optional Done attribute for actions that keep running.</summary>
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

    /// <summary>Records "Name.Select".</summary>
    protected override void OnSelect()
    {
        Record("Select");
    }

    /// <summary>Records "Name.Deselect".</summary>
    protected override void OnDeselect()
    {
        Record("Deselect");
    }

    /// <summary>Records "Name.Update".</summary>
    protected override void OnUpdate()
    {
        Record("Update");
    }

    /// <summary>Replaces the done flag from here on.</summary>
    internal void SetDone(bool isDone)
    {
        _isDone = isDone;
    }

    /// <summary>Appends the entry for the given phase to the log, as "Name.Phase".</summary>
    private void Record(string phase)
    {
        _log.Add($"{Name.Text}.{phase}");
    }
}
