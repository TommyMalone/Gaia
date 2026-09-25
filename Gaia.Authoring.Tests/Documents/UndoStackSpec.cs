using Gaia.Authoring.Documents;
using Gaia.Core.Specifications;

namespace Gaia.Authoring.Tests.Documents;

/// <summary>Covers the undo stack of <see cref="AuthoringDocument" />, transactions, redo and the labels.</summary>
/// <remarks>
///     The fixture is one option with one attribute, since the stack is the subject and the tree is what it acts on.
///     Every comparison of text is of what <see cref="XmlSpecificationWriter" /> writes.
/// </remarks>
[TestFixture]
public class UndoStackSpec
{
    [SetUp]
    public void BuildDocument()
    {
        _option = new SpecificationNode("Option", "ConsiderationAndAction");
        _option.SetAttribute("Name", "Patrol");
        _document = new AuthoringDocument(_option);
    }

    private const string Before = "<Option Type=\"ConsiderationAndAction\" Name=\"Patrol\"/>\n";

    private AuthoringDocument _document;
    private SpecificationNode _option;

    [Test]
    public void RevertsThreeCommandsOfATransactionWithOneUndo()
    {
        using (_document.Transaction("Rename, rank and add an action"))
        {
            _document.SetAttribute(_option, "Name", "Guard");
            _document.SetAttribute(_option, "Rank", "1");
            _document.AddChild(_option, "Action", "Record");
        }

        string writtenAfter = _document.Write();

        _document.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(writtenAfter, Does.Contain("Name=\"Guard\" Rank=\"1\">\n    <Action Type=\"Record\"/>"));
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanUndo, Is.False, "the three commands were one step");
        });
    }

    [Test]
    public void FoldsANestedScopeIntoTheOuterOne()
    {
        using (_document.Transaction("Rename and add an action"))
        {
            _document.SetAttribute(_option, "Name", "Guard");

            using (_document.Transaction("Add an action"))
            {
                _document.AddChild(_option, "Action", "Record");
            }

            _document.SetAttribute(_option, "Rank", "1");
        }

        _document.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanUndo, Is.False, "the inner scope recorded no step of its own");
        });
    }

    [Test]
    public void RecordsNothingForAnEmptyTransaction()
    {
        using (_document.Transaction("Nothing"))
        {
        }

        Assert.Multiple(() =>
        {
            Assert.That(_document.CanUndo, Is.False);
            Assert.That(_document.IsDirty, Is.False);
            Assert.That(_document.UndoLabel, Is.Null);
        });
    }

    [Test]
    public void ClearsRedoWhenANewCommandRuns()
    {
        _document.SetAttribute(_option, "Name", "Guard");
        _document.Undo();
        bool couldRedo = _document.CanRedo;

        _document.SetAttribute(_option, "Name", "Walk");

        Assert.Multiple(() =>
        {
            Assert.That(couldRedo, Is.True, "the undo left a step to redo");
            Assert.That(_document.CanRedo, Is.False);
        });
    }

    [Test]
    public void CanNeitherUndoNorRedoAtFirst()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_document.CanUndo, Is.False);
            Assert.That(_document.CanRedo, Is.False);
            Assert.That(_document.UndoLabel, Is.Null);
            Assert.That(_document.RedoLabel, Is.Null);
        });
    }

    [Test]
    public void CanUndoAfterACommand()
    {
        _document.SetAttribute(_option, "Name", "Guard");

        Assert.Multiple(() =>
        {
            Assert.That(_document.CanUndo, Is.True);
            Assert.That(_document.CanRedo, Is.False);
        });
    }

    [Test]
    public void LabelsALoneCommandAfterTheCommand()
    {
        SpecificationNode action = new("Action", "Record");
        SpecificationNode wait = new("Action", "Wait");

        // The option holds another action before the added one, so the move to the front changes where it stands and
        // is recorded.
        _option.AddChild(wait);

        Assert.Multiple(() =>
        {
            _document.SetAttribute(_option, "Name", "Guard");
            Assert.That(_document.UndoLabel, Is.EqualTo("Set Name"));
            _document.UnsetAttribute(_option, "Name");
            Assert.That(_document.UndoLabel, Is.EqualTo("Unset Name"));
            _document.AddChild(_option, action);
            Assert.That(_document.UndoLabel, Is.EqualTo("Add Action"));
            _document.Move(action, _option, 0);
            Assert.That(_document.UndoLabel, Is.EqualTo("Move Action"));
            _document.RemoveChild(action);
            Assert.That(_document.UndoLabel, Is.EqualTo("Remove Action"));
            _document.SetNotes(_option, " Walks the wall. ");
            Assert.That(_document.UndoLabel, Is.EqualTo("Set notes"));
        });
    }

    [Test]
    public void LabelsATransactionWithTheLabelItWasOpenedUnder()
    {
        using (_document.Transaction("Reorder options"))
        {
            _document.SetAttribute(_option, "Name", "Guard");
            _document.SetAttribute(_option, "Rank", "1");
        }

        Assert.That(_document.UndoLabel, Is.EqualTo("Reorder options"));
    }

    [Test]
    public void LabelsTheNextRedoAfterAnUndo()
    {
        _document.SetAttribute(_option, "Name", "Guard");

        _document.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(_document.RedoLabel, Is.EqualTo("Set Name"));
            Assert.That(_document.UndoLabel, Is.Null);
        });
    }

    [Test]
    public void LabelsAFoldedTransactionWithTheOutermostLabel()
    {
        using (_document.Transaction("Outer"))
        {
            using (_document.Transaction("Inner"))
            {
                _document.SetAttribute(_option, "Name", "Guard");
            }
        }

        Assert.That(_document.UndoLabel, Is.EqualTo("Outer"));
    }

    [Test]
    public void RecordsATransactionOfOneCommandUnderTheTransactionsLabel()
    {
        using (_document.Transaction("Rename"))
        {
            _document.SetAttribute(_option, "Name", "Guard");
        }

        Assert.That(_document.UndoLabel, Is.EqualTo("Rename"));
    }

    [Test]
    public void DoesNothingWhenThereIsNothingToUndo()
    {
        int changeCount = 0;
        _document.Changed += _ => changeCount++;

        _document.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(changeCount, Is.EqualTo(0));
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanRedo, Is.False);
        });
    }

    [Test]
    public void DoesNothingWhenThereIsNothingToRedo()
    {
        _document.SetAttribute(_option, "Name", "Guard");
        int changeCount = 0;
        _document.Changed += _ => changeCount++;

        _document.Redo();

        Assert.Multiple(() =>
        {
            Assert.That(changeCount, Is.EqualTo(0));
            Assert.That(_document.CanUndo, Is.True);
        });
    }

    [Test]
    public void RevertsATransactionsCommandsLastToFirst()
    {
        // The set to 2 changes the attribute the set to 1 added. Reverted in the order they ran, the removal of the
        // attribute would run before the set back to 1, and Rank="1" would be left in the text.
        using (_document.Transaction("Rank twice"))
        {
            _document.SetAttribute(_option, "Rank", "1");
            _document.SetAttribute(_option, "Rank", "2");
        }

        _document.Undo();

        Assert.That(_document.Write(), Is.EqualTo(Before));
    }

    [Test]
    public void ReappliesATransactionsCommandsFirstToLast()
    {
        // Applied last to first, the set to 2 would add the attribute and the set to 1 would then overwrite it.
        using (_document.Transaction("Rank twice"))
        {
            _document.SetAttribute(_option, "Rank", "1");
            _document.SetAttribute(_option, "Rank", "2");
        }

        _document.Undo();
        _document.Redo();

        Assert.That(_document.Write(), Does.Contain("Rank=\"2\""));
    }

    [Test]
    public void EndsATransactionOnceWhenItsScopeIsDisposedTwice()
    {
        IDisposable scope = _document.Transaction("Rename");
        _document.SetAttribute(_option, "Name", "Guard");
        scope.Dispose();
        scope.Dispose();

        _document.SetAttribute(_option, "Name", "Walk");
        string? labelAfterTheSecondCommand = _document.UndoLabel;
        _document.Undo();
        _document.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(labelAfterTheSecondCommand, Is.EqualTo("Set Name"), "the second command is a step of its own");
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanUndo, Is.False, "two steps were recorded, the transaction and the command");
        });
    }

    [Test]
    public void RedoesATransactionAsOneStep()
    {
        using (_document.Transaction("Rename, rank and add an action"))
        {
            _document.SetAttribute(_option, "Name", "Guard");
            _document.SetAttribute(_option, "Rank", "1");
            _document.AddChild(_option, "Action", "Record");
        }

        string writtenAfter = _document.Write();
        _document.Undo();

        _document.Redo();

        Assert.Multiple(() =>
        {
            Assert.That(_document.Write(), Is.EqualTo(writtenAfter));
            Assert.That(_document.CanRedo, Is.False);
            Assert.That(_document.UndoLabel, Is.EqualTo("Rename, rank and add an action"));
        });
    }
}
