using Gaia.Authoring.Documents;
using Gaia.Core.Specifications;
using NUnit.Framework.Constraints;

namespace Gaia.Authoring.Tests.Documents;

/// <summary>
///     Covers the seven commands of <see cref="AuthoringDocument" /> and their inverses, through the text
///     <see cref="XmlSpecificationWriter" /> writes before and after each.
/// </summary>
/// <remarks>
///     The fixture is a hand-built brain of two options with a comment above one and inside the other, and three
///     attributes on one consideration, so that a place lost through an inverse, an attribute's, a child's or a comment's,
///     shows in the text. WrittenBefore is that text, and each undo test asserts that the command changed it and that the
///     undo gave it back.
/// </remarks>
[TestFixture]
public class CommandSpec
{
    [SetUp]
    public void BuildDocument()
    {
        _brain = new SpecificationNode("Brain");
        _brain.AddLeadingComment(" A hand-built brain. ");
        _reasoner = new SpecificationNode("Reasoner", "DualUtility");
        _fight = new SpecificationNode("Option", "ConsiderationAndAction");
        _fight.SetAttribute("Name", "Fight");
        _fight.AddLeadingComment(" Fights while a threat is known. ");
        _tuning = new SpecificationNode("Consideration", "Tuning");
        _tuning.SetAttribute("Addend", "2");
        _tuning.SetAttribute("Multiplier", "1");
        _tuning.SetAttribute("Rank", "5");
        _fightAction = new SpecificationNode("Action", "Record");
        _fightAction.SetAttribute("Name", "Fight");
        _patrol = new SpecificationNode("Option", "ConsiderationAndAction");
        _patrol.SetAttribute("Name", "Patrol");
        _patrol.AddTrailingComment(" More actions go here. ");
        _patrolAction = new SpecificationNode("Action", "Record");
        _patrolAction.SetAttribute("Name", "Patrol");

        _brain.AddChild(_reasoner);
        _reasoner.AddChild(_fight);
        _fight.AddChild(_tuning);
        _fight.AddChild(_fightAction);
        _reasoner.AddChild(_patrol);
        _patrol.AddChild(_patrolAction);

        _document = new AuthoringDocument(_brain);
    }

    private const string WrittenBefore = """
                                         <!-- A hand-built brain. -->
                                         <Brain>
                                             <Reasoner Type="DualUtility">
                                                 <!-- Fights while a threat is known. -->
                                                 <Option Type="ConsiderationAndAction" Name="Fight">
                                                     <Consideration Type="Tuning" Addend="2" Multiplier="1" Rank="5"/>
                                                     <Action Type="Record" Name="Fight"/>
                                                 </Option>
                                                 <Option Type="ConsiderationAndAction" Name="Patrol">
                                                     <Action Type="Record" Name="Patrol"/>
                                                     <!-- More actions go here. -->
                                                 </Option>
                                             </Reasoner>
                                         </Brain>

                                         """;

    private static readonly string Before = WrittenBefore.ReplaceLineEndings("\n");

    private AuthoringDocument _document;
    private SpecificationNode _brain;
    private SpecificationNode _reasoner;
    private SpecificationNode _fight;
    private SpecificationNode _tuning;
    private SpecificationNode _fightAction;
    private SpecificationNode _patrol;
    private SpecificationNode _patrolAction;

    [Test]
    public void WritesTheTreeItWasBuiltOver()
    {
        Assert.That(_document.Write(), Is.EqualTo(Before));
    }

    [Test]
    public void UndoesSettingAnAttribute()
    {
        AssertUndoGivesTheTextBack(() => _document.SetAttribute(_tuning, "Rank", "7"),
            Does.Contain("Addend=\"2\" Multiplier=\"1\" Rank=\"7\""));
    }

    [Test]
    public void RedoesSettingAnAttribute()
    {
        AssertRedoGivesTheTextAfter(() => _document.SetAttribute(_tuning, "Rank", "7"));
    }

    [Test]
    public void UndoesSettingAnAttributeTheNodeDidNotCarry()
    {
        AssertUndoGivesTheTextBack(() => _document.SetAttribute(_tuning, "Veto", "true"),
            Does.Contain("Rank=\"5\" Veto=\"true\""));
    }

    [Test]
    public void RedoesSettingAnAttributeTheNodeDidNotCarry()
    {
        AssertRedoGivesTheTextAfter(() => _document.SetAttribute(_tuning, "Veto", "true"));
    }

    [Test]
    public void UndoesUnsettingAnAttribute()
    {
        AssertUndoGivesTheTextBack(() => _document.UnsetAttribute(_tuning, "Multiplier"),
            Does.Contain("<Consideration Type=\"Tuning\" Addend=\"2\" Rank=\"5\"/>"));
    }

    [Test]
    public void RedoesUnsettingAnAttribute()
    {
        AssertRedoGivesTheTextAfter(() => _document.UnsetAttribute(_tuning, "Multiplier"));
    }

    [Test]
    public void UndoesAddingANode()
    {
        SpecificationNode consideration = new("Consideration", "Tuning");

        AssertUndoGivesTheTextBack(() => _document.AddChild(_patrol, consideration, 0),
            Does.Contain("<Consideration Type=\"Tuning\"/>\n            <Action Type=\"Record\" Name=\"Patrol\"/>"));
    }

    [Test]
    public void RedoesAddingANode()
    {
        SpecificationNode consideration = new("Consideration", "Tuning");

        AssertRedoGivesTheTextAfter(() => _document.AddChild(_patrol, consideration, 0));
    }

    [Test]
    public void UndoesAddingANodeBuiltFromANameAndAType()
    {
        AssertUndoGivesTheTextBack(() => _document.AddChild(_patrol, "Consideration", "Tuning", 0),
            Does.Contain("<Consideration Type=\"Tuning\"/>\n            <Action Type=\"Record\" Name=\"Patrol\"/>"));
    }

    [Test]
    public void RedoesAddingANodeBuiltFromANameAndAType()
    {
        AssertRedoGivesTheTextAfter(() => _document.AddChild(_patrol, "Consideration", "Tuning", 0));
    }

    [Test]
    public void UndoesRemovingASubtree()
    {
        AssertUndoGivesTheTextBack(() => _document.RemoveChild(_fight), Does.Not.Contain("Fight"));
    }

    [Test]
    public void RedoesRemovingASubtree()
    {
        AssertRedoGivesTheTextAfter(() => _document.RemoveChild(_fight));
    }

    [Test]
    public void UndoesAMove()
    {
        AssertUndoGivesTheTextBack(() => _document.Move(_fightAction, _patrol, 0),
            Does.Contain(
                "<Action Type=\"Record\" Name=\"Fight\"/>\n            <Action Type=\"Record\" Name=\"Patrol\"/>"));
    }

    [Test]
    public void RedoesAMove()
    {
        AssertRedoGivesTheTextAfter(() => _document.Move(_fightAction, _patrol, 0));
    }

    [Test]
    public void UndoesSettingTheNotes()
    {
        AssertUndoGivesTheTextBack(() => _document.SetNotes(_fight, " Fights. "),
            Does.Contain("        <!-- Fights. -->\n        <Option"));
    }

    [Test]
    public void RedoesSettingTheNotes()
    {
        AssertRedoGivesTheTextAfter(() => _document.SetNotes(_fight, " Fights. "));
    }

    [Test]
    public void KeepsTheNodeObjectThroughAnUndoOfARemoval()
    {
        _document.RemoveChild(_fight);
        bool wasDetached = _fight.Parent == null;

        _document.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(wasDetached, Is.True, "the removal takes the node out of the tree");
            Assert.That(_reasoner.Children[0], Is.SameAs(_fight));
            Assert.That(_fight.Parent, Is.SameAs(_reasoner));
            Assert.That(_tuning.Parent, Is.SameAs(_fight), "the subtree beneath the node is untouched");
        });
    }

    [Test]
    public void KeepsAnAttributesPlaceThroughUnsetAndUndo()
    {
        _document.UnsetAttribute(_tuning, "Multiplier");
        string writtenWithout = _document.Write();

        _document.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(writtenWithout, Does.Contain("<Consideration Type=\"Tuning\" Addend=\"2\" Rank=\"5\"/>"));
            Assert.That(_document.Write(),
                Does.Contain("<Consideration Type=\"Tuning\" Addend=\"2\" Multiplier=\"1\" Rank=\"5\"/>"));
        });
    }

    [Test]
    public void RefusesACommandOnANodeFromAnotherDocument()
    {
        SpecificationNode stranger = new("Option", "ConsiderationAndAction");
        SpecificationNode strangerAction = new("Action", "Record");
        SpecificationNode fresh = new("Consideration", "Tuning");
        stranger.AddChild(strangerAction);

        Assert.Multiple(() =>
        {
            Assert.That(() => _document.SetAttribute(stranger, "Name", "Guard"), Throws.ArgumentException);
            Assert.That(() => _document.UnsetAttribute(stranger, "Name"), Throws.ArgumentException);
            Assert.That(() => _document.AddChild(stranger, fresh), Throws.ArgumentException);
            Assert.That(() => _document.AddChild(stranger, "Consideration", "Tuning"), Throws.ArgumentException);
            Assert.That(() => _document.RemoveChild(strangerAction), Throws.ArgumentException);
            Assert.That(() => _document.Move(strangerAction, _patrol, 0), Throws.ArgumentException);
            Assert.That(() => _document.Move(_fightAction, stranger, 0), Throws.ArgumentException);
            Assert.That(() => _document.SetNotes(stranger, " Notes. "), Throws.ArgumentException);
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanUndo, Is.False, "a refused command records nothing");
            Assert.That(strangerAction.Parent, Is.SameAs(stranger));
            Assert.That(fresh.Parent, Is.Null);
        });
    }

    [Test]
    public void RefusesAnIndexPastTheEnd()
    {
        SpecificationNode fresh = new("Consideration", "Tuning");

        Assert.Multiple(() =>
        {
            Assert.That(() => _document.AddChild(_patrol, fresh, 2), Throws.InstanceOf<ArgumentException>(),
                "Patrol holds one child, so 1 is the end");
            Assert.That(() => _document.AddChild(_patrol, "Consideration", "Tuning", 2),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => _document.Move(_fightAction, _patrol, 2), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => _document.Move(_fightAction, _fight, 2), Throws.InstanceOf<ArgumentException>(),
                "among Fight's two children the last place is 1");
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanUndo, Is.False, "a refused command records nothing");
            Assert.That(fresh.Parent, Is.Null);
            Assert.That(_fightAction.Parent, Is.SameAs(_fight));
        });
    }

    [Test]
    public void RecordsNoStepForASetToTheValueTheNodeHolds()
    {
        int changeCount = 0;
        _document.Changed += _ => changeCount++;

        _document.SetAttribute(_tuning, "Rank", "5");

        Assert.Multiple(() =>
        {
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanUndo, Is.False);
            Assert.That(_document.IsDirty, Is.False);
            Assert.That(changeCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void RecordsNothingForAnUnsetOfAnAttributeTheNodeDoesNotCarry()
    {
        _document.UnsetAttribute(_tuning, "Veto");

        Assert.Multiple(() =>
        {
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanUndo, Is.False);
            Assert.That(_document.IsDirty, Is.False);
        });
    }

    [Test]
    public void AddsAChildAfterTheLastByDefault()
    {
        SpecificationNode consideration = new("Consideration", "Tuning");

        _document.AddChild(_fight, consideration);

        Assert.Multiple(() =>
        {
            Assert.That(_fight.Children[2], Is.SameAs(consideration));
            Assert.That(consideration.Parent, Is.SameAs(_fight));
        });
    }

    [Test]
    public void ReturnsTheNodeItBuilt()
    {
        SpecificationNode built = _document.AddChild(_patrol, "Consideration", "Tuning", 0);

        Assert.Multiple(() =>
        {
            Assert.That(_patrol.Children[0], Is.SameAs(built));
            Assert.That(built.NameSpelling, Is.EqualTo("Consideration"));
            Assert.That(built.TypeSpelling, Is.EqualTo("Tuning"));
            Assert.That(built.AttributeSpellings, Is.Empty);
        });
    }

    [Test]
    public void KeepsTheNodeObjectThroughUndoAndRedoOfAnAdd()
    {
        SpecificationNode consideration = new("Consideration", "Tuning");
        _document.AddChild(_patrol, consideration, 0);
        _document.Undo();
        bool wasDetached = consideration.Parent == null;

        _document.Redo();

        Assert.Multiple(() =>
        {
            Assert.That(wasDetached, Is.True, "the undo takes the node out of the tree");
            Assert.That(_patrol.Children[0], Is.SameAs(consideration));
            Assert.That(consideration.Parent, Is.SameAs(_patrol));
        });
    }

    [Test]
    public void KeepsTheNodeObjectThroughAnUndoOfAMove()
    {
        _document.Move(_fightAction, _patrol, 0);
        bool wasUnderPatrol = _fightAction.Parent == _patrol;

        _document.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(wasUnderPatrol, Is.True, "the move puts the node beneath the other option");
            Assert.That(_fight.Children[1], Is.SameAs(_fightAction));
            Assert.That(_fightAction.Parent, Is.SameAs(_fight));
        });
    }

    [Test]
    public void MovesANodeToTheFrontOfItsParent()
    {
        AssertUndoGivesTheTextBack(() => _document.Move(_fightAction, _fight, 0),
            Does.Contain("<Action Type=\"Record\" Name=\"Fight\"/>\n            <Consideration Type=\"Tuning\""));
    }

    [Test]
    public void MovesANodeToTheLastPlaceOfItsParent()
    {
        AssertUndoGivesTheTextBack(() => _document.Move(_tuning, _fight, 1),
            Does.Contain("<Action Type=\"Record\" Name=\"Fight\"/>\n            <Consideration Type=\"Tuning\""));
    }

    [Test]
    public void MovesANodeToTheEndOfAnotherParent()
    {
        AssertUndoGivesTheTextBack(() => _document.Move(_fightAction, _patrol, 1),
            Does.Contain(
                "<Action Type=\"Record\" Name=\"Patrol\"/>\n            <Action Type=\"Record\" Name=\"Fight\"/>\n            <!-- More actions go here. -->"));
    }

    [Test]
    public void RecordsNoStepForAMoveToWhereTheNodeStands()
    {
        int changeCount = 0;
        _document.Changed += _ => changeCount++;

        _document.Move(_fightAction, _fight, 1);

        Assert.Multiple(() =>
        {
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanUndo, Is.False);
            Assert.That(_document.IsDirty, Is.False);
            Assert.That(changeCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void SetsNotesOnANodeThatHadNone()
    {
        AssertUndoGivesTheTextBack(() => _document.SetNotes(_patrol, " Walks the wall. "),
            Does.Contain(
                "        <!-- Walks the wall. -->\n        <Option Type=\"ConsiderationAndAction\" Name=\"Patrol\">"));
    }

    [Test]
    public void ClearsTheNotesForEmptyText()
    {
        // The node's comments are counted between the command and its undo, since the text written back lacks the
        // old note whether the notes were cleared or an empty comment was left in their place.
        int commentsAfterTheCommand = -1;

        AssertUndoGivesTheTextBack(() =>
        {
            _document.SetNotes(_fight, string.Empty);
            commentsAfterTheCommand = _fight.LeadingComments.Count;
        }, Does.Not.Contain("Fights while a threat is known"));

        Assert.That(commentsAfterTheCommand, Is.Zero, "the node carries no comments");
    }

    [Test]
    public void RecordsNoStepForNotesTheNodeAlreadyCarries()
    {
        int changeCount = 0;
        _document.Changed += _ => changeCount++;

        _document.SetNotes(_fight, " Fights while a threat is known. ");

        Assert.Multiple(() =>
        {
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanUndo, Is.False);
            Assert.That(_document.IsDirty, Is.False);
            Assert.That(changeCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ReplacesEveryCommentAboveTheNodeWithOne()
    {
        _fight.AddLeadingComment(" A second comment. ");
        string writtenWithBoth = _document.Write();

        _document.SetNotes(_fight, " Only this. ");
        string writtenWithOne = _document.Write();
        _document.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(writtenWithOne, Does.Not.Contain("Fights while"));
            Assert.That(writtenWithOne, Does.Not.Contain("A second comment"));
            Assert.That(writtenWithOne, Does.Contain("<!-- Only this. -->"));
            Assert.That(_document.Write(), Is.EqualTo(writtenWithBoth));
        });
    }

    [Test]
    public void LeavesTheCommentsWhenTheNodeRefusesTheNotes()
    {
        _fight.AddLeadingComment(" A second comment. ");
        string writtenWithBoth = _document.Write();

        Assert.Multiple(() =>
        {
            Assert.That(() => _document.SetNotes(_fight, "two hyphens -- end a comment"), Throws.ArgumentException);
            Assert.That(_document.Write(), Is.EqualTo(writtenWithBoth));
            Assert.That(_document.CanUndo, Is.False);
        });
    }

    [Test]
    public void RefusesToRemoveTheRoot()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => _document.RemoveChild(_brain), Throws.ArgumentException);
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanUndo, Is.False);
        });
    }

    [Test]
    public void RefusesToMoveANodeBeneathItself()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => _document.Move(_fight, _fightAction, 0), Throws.ArgumentException,
                "beneath its own child");
            Assert.That(() => _document.Move(_fight, _fight, 0), Throws.ArgumentException, "beneath itself");
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_fight.Parent, Is.SameAs(_reasoner));
            Assert.That(_document.CanUndo, Is.False);
        });
    }

    [Test]
    public void RefusesAnIndexBelowZero()
    {
        SpecificationNode fresh = new("Consideration", "Tuning");

        Assert.Multiple(() =>
        {
            Assert.That(() => _document.AddChild(_patrol, fresh, -1), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => _document.AddChild(_patrol, "Consideration", "Tuning", -1),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => _document.Move(_fightAction, _patrol, -1), Throws.InstanceOf<ArgumentException>());
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_document.CanUndo, Is.False);
        });
    }

    [Test]
    public void RefusesAChildAnotherNodeHolds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => _document.AddChild(_patrol, _fightAction), Throws.ArgumentException);
            Assert.That(_document.Write(), Is.EqualTo(Before));
            Assert.That(_fightAction.Parent, Is.SameAs(_fight));
            Assert.That(_document.CanUndo, Is.False, "the node's refusal records nothing");
        });
    }

    /// <summary>Issues the command, asserts the text changed as described, undoes it and asserts the text is as before.</summary>
    private void AssertUndoGivesTheTextBack(Action command, IResolveConstraint textAfterTheCommand)
    {
        command();
        string writtenAfter = _document.Write();

        _document.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(writtenAfter, textAfterTheCommand);
            Assert.That(_document.Write(), Is.EqualTo(Before));
        });
    }

    /// <summary>Issues the command, undoes it and redoes it, and asserts the text is what the command wrote.</summary>
    private void AssertRedoGivesTheTextAfter(Action command)
    {
        command();
        string writtenAfter = _document.Write();
        _document.Undo();
        string writtenUndone = _document.Write();

        _document.Redo();

        Assert.Multiple(() =>
        {
            Assert.That(writtenAfter, Is.Not.EqualTo(Before), "the command changes the text");
            Assert.That(writtenUndone, Is.EqualTo(Before), "the undo the redo replays");
            Assert.That(_document.Write(), Is.EqualTo(writtenAfter));
        });
    }
}
