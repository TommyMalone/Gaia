using Gaia.Authoring.Documents;
using Gaia.Authoring.Tests.Helpers;
using Gaia.Core.Specifications;
using NUnit.Framework.Constraints;

namespace Gaia.Authoring.Tests.Documents;

/// <summary>
///     Covers <see cref="AuthoringDocument" />, opening from text and writing, the dirty flag, and the change
///     notification.
/// </summary>
/// <remarks>
///     The commands and their inverses are covered in CommandSpec and the undo stack in UndoStackSpec. The fixture is
///     a hand-built brain of two options with one action each, wrapped in a document. The setup adds every change the
///     document announces to a list and counts every step it ends, and every comparison of written text is of what
///     <see cref="XmlSpecificationWriter" /> writes.
/// </remarks>
[TestFixture]
public class AuthoringDocumentSpec
{
    [SetUp]
    public void BuildDocument()
    {
        _logSink = new RecordingLogSink();
        _writer = new XmlSpecificationWriter();

        SpecificationNode brain = new("Brain");
        SpecificationNode reasoner = new("Reasoner", "DualUtility");
        _fight = new SpecificationNode("Option", "ConsiderationAndAction");
        _fight.SetAttribute("Name", "Fight");
        _fightAction = new SpecificationNode("Action", "Record");
        _fightAction.SetAttribute("Name", "Fight");
        _patrol = new SpecificationNode("Option", "ConsiderationAndAction");
        _patrol.SetAttribute("Name", "Patrol");
        _patrolAction = new SpecificationNode("Action", "Record");
        _patrolAction.SetAttribute("Name", "Patrol");
        brain.AddChild(reasoner);
        reasoner.AddChild(_fight);
        _fight.AddChild(_fightAction);
        reasoner.AddChild(_patrol);
        _patrol.AddChild(_patrolAction);

        _document = new AuthoringDocument(brain);
        _changes = new List<DocumentChange>();
        _stepEndedCount = 0;
        _document.Changed += _changes.Add;
        _document.StepEnded += () => _stepEndedCount++;
    }

    private RecordingLogSink _logSink;
    private XmlSpecificationWriter _writer;
    private AuthoringDocument _document;
    private SpecificationNode _fight;
    private SpecificationNode _fightAction;
    private SpecificationNode _patrol;
    private SpecificationNode _patrolAction;
    private List<DocumentChange> _changes;
    private int _stepEndedCount;

    [TestCase("guard.xml")]
    [TestCase("sniper.xml")]
    public void WritesTheTreeItOpenedAsTheWriterWritesIt(string fileName)
    {
        string text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName));
        XmlSpecificationReader reader = new(_logSink);
        reader.TryRead(text, fileName, out SpecificationNode? root);

        bool wasLoaded = AuthoringDocument.TryLoad(text, fileName, _logSink, out AuthoringDocument? document);

        Assert.Multiple(() =>
        {
            Assert.That(wasLoaded, Is.True);
            Assert.That(document!.Write(), Is.EqualTo(_writer.Write(root!)));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void GivesNoDocumentAndAReasonForATextTheReaderRefuses()
    {
        bool wasLoaded = AuthoringDocument.TryLoad("<Brain>", "broken.xml", _logSink, out AuthoringDocument? document);

        Assert.Multiple(() =>
        {
            Assert.That(wasLoaded, Is.False);
            Assert.That(document, Is.Null);
            Assert.That(_logSink.Entries, Has.Count.EqualTo(1));
            Assert.That(_logSink.Entries,
                Has.One.StartsWith("Error broken.xml Line 1, the document could not be parsed."));
        });
    }

    [Test]
    public void IsNotDirtyWhenOpened()
    {
        AuthoringDocument.TryLoad("<Brain/>", "brain.xml", _logSink, out AuthoringDocument? document);

        Assert.That(document!.IsDirty, Is.False);
    }

    [Test]
    public void IsDirtyAfterACommand()
    {
        _document.SetAttribute(_fight, "Name", "Guard");

        Assert.That(_document.IsDirty, Is.True);
    }

    [Test]
    public void IsNotDirtyAfterUndoingBackToTheSavedPoint()
    {
        _document.SetAttribute(_fight, "Name", "Guard");

        _document.Undo();

        Assert.That(_document.IsDirty, Is.False);
    }

    [Test]
    public void IsNotDirtyAfterMarkSaved()
    {
        _document.SetAttribute(_fight, "Name", "Guard");

        _document.MarkSaved();

        Assert.That(_document.IsDirty, Is.False);
    }

    [Test]
    public void IsDirtyAfterUndoingPastTheSavedPoint()
    {
        _document.SetAttribute(_fight, "Name", "Guard");
        _document.MarkSaved();

        _document.Undo();

        Assert.That(_document.IsDirty, Is.True);
    }

    [Test]
    public void AnnouncesASetAttributeOnce()
    {
        _document.SetAttribute(_fight, "Name", "Guard");

        AssertOneChange(DocumentChangeKind.AttributeSet, _fight, null, -1);
    }

    [Test]
    public void AnnouncesAnUnsetAttributeOnce()
    {
        _document.UnsetAttribute(_fight, "Name");

        AssertOneChange(DocumentChangeKind.AttributeRemoved, _fight, null, -1);
    }

    [Test]
    public void AnnouncesAnAddedChildWithItsParentAndIndex()
    {
        SpecificationNode consideration = new("Consideration", "Tuning");

        _document.AddChild(_fight, consideration, 0);

        AssertOneChange(DocumentChangeKind.ChildAdded, consideration, _fight, 0);
    }

    [Test]
    public void AnnouncesAChildBuiltFromANameAndATypeAtTheEnd()
    {
        SpecificationNode built = _document.AddChild(_fight, "Consideration", "Tuning");

        AssertOneChange(DocumentChangeKind.ChildAdded, built, _fight, 1);
    }

    [Test]
    public void AnnouncesARemovedChildWithTheIndexItLeft()
    {
        _document.RemoveChild(_fightAction);

        AssertOneChange(DocumentChangeKind.ChildRemoved, _fightAction, _fight, 0);
    }

    [Test]
    public void AnnouncesAMovedChildWithTheParentAndIndexItArrivedAt()
    {
        _document.Move(_fightAction, _patrol, 1);

        AssertOneChange(DocumentChangeKind.ChildMoved, _fightAction, _patrol, 1);
    }

    [Test]
    public void AnnouncesSetNotesOnce()
    {
        _document.SetNotes(_fight, " Fights while a threat is known. ");

        AssertOneChange(DocumentChangeKind.NotesSet, _fight, null, -1);
    }

    [Test]
    public void AnnouncesAnUndoAsTheInverseChange()
    {
        _document.RemoveChild(_fightAction);
        _changes.Clear();

        _document.Undo();

        AssertOneChange(DocumentChangeKind.ChildAdded, _fightAction, _fight, 0);
    }

    [Test]
    public void AnnouncesARedoAsTheChangeAgain()
    {
        _document.RemoveChild(_fightAction);
        _document.Undo();
        _changes.Clear();

        _document.Redo();

        AssertOneChange(DocumentChangeKind.ChildRemoved, _fightAction, _fight, 0);
    }

    [Test]
    public void AnnouncesEachCommandOfATransactionAndOneEnd()
    {
        int stepEndedInsideTheScope;

        using (_document.Transaction("Rename both and move the action"))
        {
            _document.SetAttribute(_fight, "Name", "Guard");
            _document.SetAttribute(_patrol, "Name", "Walk");
            _document.Move(_fightAction, _patrol, 0);
            stepEndedInsideTheScope = _stepEndedCount;
        }

        Assert.Multiple(() =>
        {
            Assert.That(_changes, Has.Count.EqualTo(3));
            Assert.That(stepEndedInsideTheScope, Is.EqualTo(0), "the step ends when the scope is disposed");
            Assert.That(_stepEndedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ReportsTheLineOfEachNodeWhenWriting()
    {
        string writtenWithLines = _document.Write(out IReadOnlyDictionary<SpecificationNode, int> lineByNode);

        Assert.Multiple(() =>
        {
            Assert.That(writtenWithLines, Is.EqualTo(_document.Write()));
            Assert.That(lineByNode[_fight], Is.EqualTo(3));
            Assert.That(lineByNode[_patrolAction], Is.EqualTo(7));
        });
    }

    [Test]
    public void IsDirtyAgainAfterRedoingPastTheSavedPoint()
    {
        _document.SetAttribute(_fight, "Name", "Guard");
        _document.MarkSaved();
        _document.SetAttribute(_patrol, "Name", "Walk");
        _document.Undo();
        _document.Undo();
        _document.Redo();
        bool wasDirtyAtTheSavedPoint = _document.IsDirty;

        _document.Redo();

        Assert.Multiple(() =>
        {
            Assert.That(wasDirtyAtTheSavedPoint, Is.False, "the first redo reaches the saved point");
            Assert.That(_document.IsDirty, Is.True);
        });
    }

    [Test]
    public void StaysDirtyWhenACommandFollowsAnUndoBelowTheSavedPoint()
    {
        // Saved two steps deep and undone to one, a third command makes the stack two steps deep again, but the tree
        // is not the saved one and no undo or redo reaches it any more.
        _document.SetAttribute(_fight, "Name", "Guard");
        _document.SetAttribute(_patrol, "Name", "Walk");
        _document.MarkSaved();
        _document.Undo();

        _document.SetAttribute(_patrol, "Name", "Wander");

        Assert.That(_document.IsDirty, Is.True);
    }

    [Test]
    public void IsDirtyInsideATransactionThatHasIssuedACommand()
    {
        bool isDirtyInsideTheScope;

        using (_document.Transaction("Rename"))
        {
            _document.SetAttribute(_fight, "Name", "Guard");
            isDirtyInsideTheScope = _document.IsDirty;
        }

        Assert.That(isDirtyInsideTheScope, Is.True);
    }

    [Test]
    public void IsNotDirtyInsideATransactionThatHasChangedNothing()
    {
        // Opening a transaction changes nothing in the tree, so the document is as it was saved until a command runs.
        bool isDirtyBeforeTheCommand;
        bool isDirtyAfterTheCommand;

        using (_document.Transaction("Rename"))
        {
            isDirtyBeforeTheCommand = _document.IsDirty;
            _document.SetAttribute(_fight, "Name", "Guard");
            isDirtyAfterTheCommand = _document.IsDirty;
        }

        Assert.Multiple(() =>
        {
            Assert.That(isDirtyBeforeTheCommand, Is.False);
            Assert.That(isDirtyAfterTheCommand, Is.True);
        });
    }

    [Test]
    public void AnnouncesTheUndoOfAnAddedAttributeAsARemoval()
    {
        _document.SetAttribute(_fight, "Rank", "1");
        _changes.Clear();

        _document.Undo();

        AssertOneChange(DocumentChangeKind.AttributeRemoved, _fight, null, -1);
    }

    [Test]
    public void AnnouncesTheUndoOfAnUnsetAsASet()
    {
        _document.UnsetAttribute(_fight, "Name");
        _changes.Clear();

        _document.Undo();

        AssertOneChange(DocumentChangeKind.AttributeSet, _fight, null, -1);
    }

    [Test]
    public void AnnouncesTheUndoOfAMoveAtTheParentAndIndexTheNodeLeft()
    {
        _document.Move(_fightAction, _patrol, 1);
        _changes.Clear();

        _document.Undo();

        AssertOneChange(DocumentChangeKind.ChildMoved, _fightAction, _fight, 0);
    }

    [Test]
    public void EndsAStepAfterALoneCommand()
    {
        _document.SetAttribute(_fight, "Name", "Guard");

        Assert.That(_stepEndedCount, Is.EqualTo(1));
    }

    [Test]
    public void EndsAStepAfterARedo()
    {
        _document.SetAttribute(_fight, "Name", "Guard");
        _document.Undo();
        _stepEndedCount = 0;

        _document.Redo();

        Assert.That(_stepEndedCount, Is.EqualTo(1));
    }

    [Test]
    public void EndsAStepAfterItsLastChange()
    {
        List<string> order = new();
        _document.Changed += _ => order.Add("changed");
        _document.StepEnded += () => order.Add("step ended");

        using (_document.Transaction("Rename both"))
        {
            _document.SetAttribute(_fight, "Name", "Guard");
            _document.SetAttribute(_patrol, "Name", "Walk");
        }

        _document.Undo();

        Assert.That(order,
            Is.EqualTo(new[] { "changed", "changed", "step ended", "changed", "changed", "step ended" }));
    }

    /// <summary>Asserts that exactly one change was announced and that it carries the given facts.</summary>
    private void AssertOneChange(DocumentChangeKind kind, SpecificationNode node, SpecificationNode? parent, int index)
    {
        Assert.That(_changes, Has.Count.EqualTo(1));

        DocumentChange change = _changes[0];
        Constraint parentConstraint = parent == null ? Is.Null : Is.SameAs(parent);

        Assert.Multiple(() =>
        {
            Assert.That(change.Kind, Is.EqualTo(kind));
            Assert.That(change.Node, Is.SameAs(node));
            Assert.That(change.Parent, parentConstraint);
            Assert.That(change.Index, Is.EqualTo(index));
        });
    }
}
