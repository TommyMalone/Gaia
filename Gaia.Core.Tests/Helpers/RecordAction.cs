using Gaia.Core.Actions;
using Gaia.Core.Temp;

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

    private bool _isDone = true;

    internal RecordAction(string name, List<string> log)
    {
        _name = name;
        _log = log;
    }

    /// <summary>Whatever SetDone last set. True until it is called.</summary>
    public override bool IsDone()
    {
        return _isDone;
    }

    /// <inheritdoc />
    public override bool Init(AiCreationData creationData)
    {
        // Configurations cannot presently be loaded successfully.
        return false;
    }

    /// <summary>Records "Name.Select".</summary>
    public override void Select()
    {
        Record("Select");
    }

    /// <summary>Records "Name.Deselect".</summary>
    public override void Deselect()
    {
        Record("Deselect");
    }

    /// <summary>Records "Name.Update".</summary>
    public override void Update()
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