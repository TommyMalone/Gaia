using Gaia.Core.Actions;
using Gaia.Core.Factories;

namespace Gaia.Core.Tests.Helpers;

/// <summary>
///     An action that throws from Update, and from Deselect too when asked, for specs about what a brain does with a
///     fault.
/// </summary>
/// <remarks>
///     Never done, so its option keeps running and every cycle reaches OnUpdate. Throwing from Deselect as well is
///     the shape a fault in a game usually has, the same bug meeting the same action on the way down.
/// </remarks>
internal sealed class ThrowingAction : ActionBase
{
    private readonly bool _throwsOnDeselect;

    /// <summary>Creates an action that throws every Update, and every Deselect when throwsOnDeselect is set.</summary>
    internal ThrowingAction(bool throwsOnDeselect = false)
    {
        _throwsOnDeselect = throwsOnDeselect;
    }

    /// <summary>Never, so the option stays selected and keeps updating.</summary>
    public override bool IsDone => false;

    /// <inheritdoc />
    protected override bool OnInit(AiCreationData creationData)
    {
        // Built in code with its behavior set by hand, so a load attempt is a spec mistake, reported as failure.
        return false;
    }

    /// <summary>Throws.</summary>
    protected override void OnUpdate()
    {
        throw new InvalidOperationException("the action failed");
    }

    /// <summary>Throws when the action was built to, which is on the way down after a failed Update.</summary>
    protected override void OnDeselect()
    {
        if (_throwsOnDeselect)
        {
            throw new InvalidOperationException("the action failed again");
        }
    }
}
