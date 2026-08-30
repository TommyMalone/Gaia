using Gaia.Core.Actions;
using Gaia.Core.Factories;

namespace Gaia.Core.Tests.Helpers;

/// <summary>
///     An action that records every lifecycle call it receives as "Name.Phase". Stands in for Move, FireWeapon, etc.
///     Recording Select, Deselect, and Update separately is what lets a spec distinguish an option that kept running from
///     one that was torn down and restarted.
/// </summary>
/// <remarks>Specs that compare whole logs write the expected entries grouped a cycle per line.</remarks>
internal sealed class RecordAction : ActionBase
{
    private readonly List<string> _log;
    private readonly string _name;
    private bool _isDone;

    /// <summary>Creates an action that appends to the given log under the given name.</summary>
    /// <param name="name">The prefix each recorded entry is written with, as name.Phase.</param>
    /// <param name="log">The shared list every lifecycle call is appended to. The spec owns it and reads it back.</param>
    /// <param name="isDone">
    ///     The initial IsDone. Defaults to true, which is what <see cref="ActionBase" /> reports. Pass false
    ///     for an action that keeps its option running until SetDone is called.
    /// </param>
    internal RecordAction(string name, List<string> log, bool isDone = true)
    {
        _name = name;
        _log = log;
        _isDone = isDone;

        Name = name;
    }

    /// <summary>Whatever the constructor or SetDone last set.</summary>
    public override bool IsDone => _isDone;

    /// <inheritdoc />
    protected override bool OnInit(AiCreationData creationData)
    {
        // Configurations cannot presently be loaded successfully.
        return false;
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
        _log.Add($"{_name}.{phase}");
    }
}
