using System;
using System.Collections.Generic;
using Gaia.Core.Services.Log;
using Gaia.Core.Specifications;

namespace Gaia.Authoring.Documents
{
    /// <summary>
    ///     One brain being edited, a <see cref="SpecificationNode" /> tree with the commands that change it, an undo
    ///     stack, a dirty flag and a notification of every change.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A host's views edit the tree through the commands here and through nothing else, so two views over one
    ///         document agree and an undo step is what the user did. Each command applies its change at once, records the
    ///         calls that take it back, and announces it through <see cref="Changed" />. A command that would leave the tree
    ///         as it is does nothing, so it records no undo step and leaves <see cref="IsDirty" /> as it was. Every node a
    ///         command is given, other than the child AddChild adds, has to be in this document's tree, which the document
    ///         checks with <see cref="ArgumentException" /> before the command runs. RemoveChild and Move also refuse the
    ///         root, and Move refuses a new parent that is the moved node or beneath it, and an index out of range. Everything
    ///         else a command could be given wrong is refused by <see cref="SpecificationNode" />, and the document does not
    ///         check for it first. A name or a value XML cannot carry, the Type as an attribute, a child another node holds
    ///         and an AddChild index out of range are refused that way. Whichever refuses, the exception comes through with
    ///         nothing recorded.
    ///     </para>
    ///     <para>
    ///         The inverse of every command is exact. The node an add, a removal or a move concerns is the same object
    ///         through every undo and redo of it, so a map keyed by node stays right across both. The text
    ///         <see cref="XmlSpecificationWriter" /> writes is the same after a command is undone as before it ran, and the
    ///         same after a redo as after the command.
    ///     </para>
    ///     <para>
    ///         The document reads and writes text. Files, paths and encodings belong to the host. What a node may hold and
    ///         what a Type means is what the descriptions on the factories answer, and the document does not ask them. Nothing
    ///         here runs on a decision cycle, so the commands allocate what they need.
    ///     </para>
    /// </remarks>
    public sealed class AuthoringDocument
    {
        /// <summary>The value of <see cref="_savedDepth" /> when no depth of the undo stack is the saved point.</summary>
        private const int NoSavedDepth = -1;

        private readonly List<UndoStep> _redoSteps = new();
        private readonly List<UndoStep> _undoSteps = new();
        private readonly XmlSpecificationWriter _writer = new();
        private UndoStep? _openStep;
        private int _savedDepth;
        private int _transactionDepth;

        /// <summary>Creates a document over a tree, with nothing to undo and nothing unsaved.</summary>
        public AuthoringDocument(SpecificationNode root)
        {
            Root = root;
        }

        /// <summary>The root of the tree, which every command's node is beneath or is.</summary>
        public SpecificationNode Root { get; }

        /// <summary>Whether the tree differs from the last saved point.</summary>
        /// <remarks>
        ///     The saved point is a depth of the undo stack, set by <see cref="MarkSaved" /> and zero for a new document.
        ///     Undoing back to that depth clears the flag and a redo past it sets it again. A command issued while the stack is
        ///     below the saved depth makes the saved tree unreachable by undo or redo, so the point is dropped until the next
        ///     <see cref="MarkSaved" />. A command inside an open transaction counts before its step is recorded.
        /// </remarks>
        public bool IsDirty => _undoSteps.Count != _savedDepth || (_openStep != null && _openStep.Edits.Count > 0);

        /// <summary>Whether there is a step for <see cref="Undo" /> to take back.</summary>
        public bool CanUndo => _undoSteps.Count > 0;

        /// <summary>Whether there is a step for <see cref="Redo" /> to apply again.</summary>
        public bool CanRedo => _redoSteps.Count > 0;

        /// <summary>The label of the step <see cref="Undo" /> would take back, for a menu, or null when there is none.</summary>
        public string? UndoLabel => _undoSteps.Count > 0 ? _undoSteps[_undoSteps.Count - 1].Label : null;

        /// <summary>The label of the step <see cref="Redo" /> would apply again, for a menu, or null when there is none.</summary>
        public string? RedoLabel => _redoSteps.Count > 0 ? _redoSteps[_redoSteps.Count - 1].Label : null;

        /// <summary>Raised after every applied command, undo and redo, with what changed.</summary>
        /// <remarks>
        ///     A transaction raises one event per command inside it. A view that wants to refresh once per step marks what
        ///     changed here and refreshes on <see cref="StepEnded" />.
        /// </remarks>
        public event Action<DocumentChange>? Changed;

        /// <summary>Raised when each undo step ends.</summary>
        /// <remarks>
        ///     The event is raised after a command issued outside any transaction, after a transaction's outermost scope is
        ///     disposed, after an undo and after a redo, each time following the last <see cref="Changed" /> of the step. A
        ///     command that changes nothing, and a transaction in which no command changed anything, record no step and raise
        ///     nothing.
        /// </remarks>
        public event Action? StepEnded;

        /// <summary>Opens a document from XML text, through <see cref="XmlSpecificationReader" />.</summary>
        /// <param name="text">The document's XML.</param>
        /// <param name="sourceDocumentName">The name the reader files its reports under, usually the file's name.</param>
        /// <param name="logSink">Where the reader and every node of the tree report.</param>
        /// <param name="document">The document over the tree the reader built, or null when the reader refused the text.</param>
        /// <returns>True when the text was read. False after the reader reported why it was not, to the sink.</returns>
        public static bool TryLoad(
            string text,
            string sourceDocumentName,
            ILogSink logSink,
            out AuthoringDocument? document)
        {
            XmlSpecificationReader reader = new(logSink);

            if (reader.TryRead(text, sourceDocumentName, out SpecificationNode? root))
            {
                document = new AuthoringDocument(root!);
                return true;
            }

            document = null;
            return false;
        }

        /// <summary>Writes the tree as <see cref="XmlSpecificationWriter" /> writes it, which is what a host saves.</summary>
        public string Write()
        {
            return _writer.Write(Root);
        }

        /// <summary>Writes the tree as the overload taking nothing does, and reports the line each node was written on.</summary>
        /// <param name="lineByNode">
        ///     For every node of the tree, the line of the returned text its opening tag is on, counted from
        ///     1 as <see cref="SpecificationNode.LineNumber" /> is, which is the line a report names when the text is read back.
        /// </param>
        public string Write(out IReadOnlyDictionary<SpecificationNode, int> lineByNode)
        {
            return _writer.Write(Root, out lineByNode);
        }

        /// <summary>Makes the tree as it stands the saved point, so <see cref="IsDirty" /> is false until it changes again.</summary>
        public void MarkSaved()
        {
            _savedDepth = _undoSteps.Count;
        }

        /// <summary>
        ///     Opens a transaction, so that the commands issued until the returned scope is disposed form one step of the
        ///     undo stack.
        /// </summary>
        /// <remarks>
        ///     A scope opened inside another adds its commands to the outer scope's step, and the outermost scope's label is
        ///     the step's. A scope disposed with no command issued inside it records nothing. Disposing the scope is what ends the
        ///     transaction, so a using statement ends it on any path out of the block.
        /// </remarks>
        /// <param name="label">What a menu shows for the step, after "Undo" or "Redo".</param>
        public IDisposable Transaction(string label)
        {
            if (_transactionDepth == 0)
            {
                _openStep = new UndoStep(label);
            }

            _transactionDepth++;
            return new DocumentTransaction(this);
        }

        /// <summary>Takes back the most recent step, or does nothing when there is none.</summary>
        /// <remarks>
        ///     The step's edits are reverted last to first, each announced through <see cref="Changed" />, and the step moves
        ///     to the redo stack.
        /// </remarks>
        public void Undo()
        {
            if (_undoSteps.Count == 0)
            {
                return;
            }

            UndoStep step = _undoSteps[_undoSteps.Count - 1];
            _undoSteps.RemoveAt(_undoSteps.Count - 1);
            _redoSteps.Add(step);

            for (int index = step.Edits.Count - 1; index >= 0; index--)
            {
                // The change is taken into a local before the event is raised, since a null-conditional call does not
                // evaluate its arguments when there is no subscriber, and the revert has to run whether or not one
                // listens.
                DocumentChange change = step.Edits[index].Revert();
                Changed?.Invoke(change);
            }

            StepEnded?.Invoke();
        }

        /// <summary>Applies again the step most recently taken back, or does nothing when there is none.</summary>
        /// <remarks>
        ///     The step's edits are applied first to last, each announced through <see cref="Changed" />, and the step moves
        ///     back to the undo stack.
        /// </remarks>
        public void Redo()
        {
            if (_redoSteps.Count == 0)
            {
                return;
            }

            UndoStep step = _redoSteps[_redoSteps.Count - 1];
            _redoSteps.RemoveAt(_redoSteps.Count - 1);
            _undoSteps.Add(step);

            foreach (Edit edit in step.Edits)
            {
                // Taken into a local before the event is raised, for the reason Undo gives.
                DocumentChange change = edit.Apply();
                Changed?.Invoke(change);
            }

            StepEnded?.Invoke();
        }

        /// <summary>Sets an attribute's text, adding the attribute after the node's others when the node does not carry it.</summary>
        /// <remarks>
        ///     The name matches in any casing, as <see cref="SpecificationNode.SetAttribute" /> matches it. Renaming a
        ///     component is a set of its Name attribute. The inverse restores the previous text, or removes the attribute the
        ///     command added. When the attribute already holds the text, nothing is recorded.
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///     The node is not in this document, or the name or the value is one the node refuses,
        ///     the Type in any casing among them.
        /// </exception>
        public void SetAttribute(SpecificationNode node, string name, string value)
        {
            RequireInTree(node);

            Edit edit;

            if (node.TryGetString(name, out string previousValue))
            {
                if (previousValue == value)
                {
                    return;
                }

                edit = new Edit(() => Set(node, name, value), () => Set(node, name, previousValue));
            }
            else
            {
                edit = new Edit(() => Set(node, name, value), () => Remove(node, name));
            }

            Run($"Set {name}", edit);
        }

        /// <summary>Removes an attribute, so that a saved file does not spell out a value the component defaults to.</summary>
        /// <remarks>
        ///     The inverse puts the attribute back at the index it had, under the spelling it had, since the order of the
        ///     attributes is in the written text. An attribute the node does not carry is left as it is and nothing is recorded.
        /// </remarks>
        /// <exception cref="ArgumentException">The node is not in this document.</exception>
        public void UnsetAttribute(SpecificationNode node, string name)
        {
            RequireInTree(node);

            int index = node.IndexOfAttribute(name);

            if (index < 0)
            {
                return;
            }

            string spelling = node.AttributeSpellings[index];
            node.TryGetString(name, out string value);

            Edit edit = new(() => Remove(node, name), () => Insert(node, index, spelling, value));

            Run($"Unset {name}", edit);
        }

        /// <summary>Adds a node, and whatever is beneath it, as a child of a node in this document.</summary>
        /// <remarks>
        ///     What a host does with a subtree it built whole. The inverse removes the node again, and a redo puts the same
        ///     node object back, so its identity survives both.
        /// </remarks>
        /// <param name="parent">The node in this document to add beneath.</param>
        /// <param name="child">The node to add, which no node may hold yet.</param>
        /// <param name="index">The index among the parent's children to add at, or null for after the last.</param>
        /// <exception cref="ArgumentException">
        ///     The parent is not in this document, or the child is one the parent refuses, a node
        ///     another node holds or an ancestor of the parent.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">The index is below 0 or above the number of the parent's children.</exception>
        public void AddChild(SpecificationNode parent, SpecificationNode child, int? index = null)
        {
            RequireInTree(parent);

            int insertionIndex = index ?? parent.Children.Count;

            Edit edit = new(() => InsertChild(parent, insertionIndex, child), () => RemoveChild(parent, child));

            Run($"Add {child.NameSpelling}", edit);
        }

        /// <summary>Builds a node with no attributes and adds it as a child of a node in this document.</summary>
        /// <remarks>
        ///     What a host does to add a fresh element. The inverse removes the node, and a redo puts the same node object
        ///     back.
        /// </remarks>
        /// <param name="parent">The node in this document to add beneath.</param>
        /// <param name="name">The element name of the new node, such as Option.</param>
        /// <param name="type">The Type of the new node, or empty text for an element with no Type.</param>
        /// <param name="index">The index among the parent's children to add at, or null for after the last.</param>
        /// <returns>The node that was built and added.</returns>
        /// <exception cref="ArgumentException">
        ///     The parent is not in this document, or the name or the type is one
        ///     <see cref="SpecificationNode" />'s constructor refuses.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">The index is below 0 or above the number of the parent's children.</exception>
        public SpecificationNode AddChild(SpecificationNode parent, string name, string type, int? index = null)
        {
            RequireInTree(parent);

            SpecificationNode child = new(name, type);
            AddChild(parent, child, index);
            return child;
        }

        /// <summary>Removes a node, and whatever is beneath it, from its parent.</summary>
        /// <remarks>
        ///     The subtree is kept whole for the inverse, which puts the same node object back at the index it stood at. The
        ///     root is not removed, since there would be no tree.
        /// </remarks>
        /// <exception cref="ArgumentException">The node is not in this document, or is its root.</exception>
        public void RemoveChild(SpecificationNode node)
        {
            SpecificationNode parent = RequireParentInTree(node);
            int index = IndexOfChild(parent, node);

            Edit edit = new(() => RemoveChild(parent, node), () => InsertChild(parent, index, node));

            Run($"Remove {node.NameSpelling}", edit);
        }

        /// <summary>
        ///     Moves a node, and whatever is beneath it, to an index among another node's children, or among its own
        ///     parent's, which reorders it.
        /// </summary>
        /// <remarks>
        ///     The index is where the node stands once the move is done, so within one parent the last place is one less than
        ///     the number of children, and beneath another parent it is the number of children. The inverse moves the node back to
        ///     the parent and the index it left. The refusals are checked before the node leaves its parent, so a refused move
        ///     changes nothing. When the new parent is the node's own and the index is the one it stands at, nothing is recorded.
        /// </remarks>
        /// <param name="node">The node to move, which is not the root.</param>
        /// <param name="newParent">The node in this document to move beneath, which is not the node or beneath it.</param>
        /// <param name="index">The index among the new parent's children the node stands at afterwards.</param>
        /// <exception cref="ArgumentException">
        ///     The node or the new parent is not in this document, the node is the root, or the
        ///     new parent is the node or is beneath it.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">The index is below 0 or past the last place the node could stand at.</exception>
        public void Move(SpecificationNode node, SpecificationNode newParent, int index)
        {
            SpecificationNode oldParent = RequireParentInTree(node);
            RequireInTree(newParent);

            for (SpecificationNode? ancestor = newParent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor == node)
                {
                    throw new ArgumentException(
                        $"The {newParent.NameSpelling} node is the {node.NameSpelling} node being moved, or is beneath it.",
                        nameof(newParent));
                }
            }

            int lastIndex = newParent == oldParent ? newParent.Children.Count - 1 : newParent.Children.Count;

            if (index < 0 || index > lastIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    $"a child is moved to an index from 0 to {lastIndex}, the last place among the {newParent.NameSpelling} node's children once it is there");
            }

            int oldIndex = IndexOfChild(oldParent, node);

            if (newParent == oldParent && index == oldIndex)
            {
                return;
            }

            Edit edit = new(() => MoveChild(node, oldParent, newParent, index),
                () => MoveChild(node, newParent, oldParent, oldIndex));

            Run($"Move {node.NameSpelling}", edit);
        }

        /// <summary>Replaces the comments above a node with one holding the text, or with none for empty text.</summary>
        /// <remarks>
        ///     The comments above an element are the designer's notes on it, and the text is kept as given, spaces at either
        ///     end included. The inverse puts back every comment that stood there. A text the node refuses as a comment leaves the
        ///     comments as they were. When the comments above the node are already the ones the text would leave there, nothing is
        ///     recorded.
        /// </remarks>
        /// <exception cref="ArgumentException">The node is not in this document, or the text is one the node refuses as a comment.</exception>
        public void SetNotes(SpecificationNode node, string text)
        {
            RequireInTree(node);

            List<string> previousComments = new(node.LeadingComments);
            List<string> comments = new();

            if (text.Length > 0)
            {
                comments.Add(text);
            }

            // The new list holds one comment at most, so equal counts and equal comments at index 0 make equal lists.
            if (previousComments.Count == comments.Count && (comments.Count == 0 || previousComments[0] == comments[0]))
            {
                return;
            }

            Edit edit = new(() => ReplaceLeadingComments(node, comments, previousComments),
                () => ReplaceLeadingComments(node, previousComments, comments));

            Run("Set notes", edit);
        }

        /// <summary>Ends the transaction the most recently opened scope began, for <see cref="DocumentTransaction" />.</summary>
        /// <remarks>
        ///     Ending the outermost scope records the open step, when a command inside it changed the tree, and raises
        ///     <see cref="StepEnded" /> for it.
        /// </remarks>
        internal void EndTransaction()
        {
            _transactionDepth--;

            if (_transactionDepth > 0)
            {
                return;
            }

            UndoStep step = _openStep!;
            _openStep = null;

            if (step.Edits.Count == 0)
            {
                return;
            }

            _undoSteps.Add(step);
            StepEnded?.Invoke();
        }

        /// <summary>Applies an edit, records it and announces it, as every command does once it has built its edit.</summary>
        /// <remarks>
        ///     The edit applies first, so one the node refuses throws before anything is recorded. A command clears the redo
        ///     stack, and drops the saved point when the stack stood below it, since the saved tree can no longer be reached.
        ///     Inside a transaction the edit joins the open step, otherwise it is a step of its own, under the label, and
        ///     <see cref="StepEnded" /> follows it.
        /// </remarks>
        private void Run(string label, Edit edit)
        {
            DocumentChange change = edit.Apply();

            _redoSteps.Clear();

            if (_savedDepth > _undoSteps.Count)
            {
                _savedDepth = NoSavedDepth;
            }

            if (_openStep != null)
            {
                _openStep.Add(edit);
                Changed?.Invoke(change);
                return;
            }

            UndoStep step = new(label);
            step.Add(edit);
            _undoSteps.Add(step);
            Changed?.Invoke(change);
            StepEnded?.Invoke();
        }

        /// <summary>Refuses a node that is not the root or beneath it.</summary>
        /// <exception cref="ArgumentException">The node is not in this document.</exception>
        private void RequireInTree(SpecificationNode node)
        {
            for (SpecificationNode? ancestor = node; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor == Root)
                {
                    return;
                }
            }

            throw new ArgumentException($"The {node.NameSpelling} node is not in this document.", nameof(node));
        }

        /// <summary>Refuses a node that is not beneath the root, and hands back its parent.</summary>
        /// <exception cref="ArgumentException">The node is not in this document, or is its root.</exception>
        private SpecificationNode RequireParentInTree(SpecificationNode node)
        {
            RequireInTree(node);

            if (node.Parent == null)
            {
                throw new ArgumentException(
                    $"The {node.NameSpelling} node is the document's root, which has no parent to take it from.",
                    nameof(node));
            }

            return node.Parent;
        }

        /// <summary>The index of a child among its parent's children, or -1 when the parent does not hold it.</summary>
        private static int IndexOfChild(SpecificationNode parent, SpecificationNode child)
        {
            for (int index = 0; index < parent.Children.Count; index++)
            {
                if (parent.Children[index] == child)
                {
                    return index;
                }
            }

            return -1;
        }

        private static DocumentChange Set(SpecificationNode node, string name, string value)
        {
            node.SetAttribute(name, value);
            return new DocumentChange(DocumentChangeKind.AttributeSet, node, null, -1);
        }

        private static DocumentChange Insert(SpecificationNode node, int index, string spelling, string value)
        {
            node.InsertAttribute(index, spelling, value);
            return new DocumentChange(DocumentChangeKind.AttributeSet, node, null, -1);
        }

        private static DocumentChange Remove(SpecificationNode node, string name)
        {
            node.RemoveAttribute(name);
            return new DocumentChange(DocumentChangeKind.AttributeRemoved, node, null, -1);
        }

        private static DocumentChange InsertChild(SpecificationNode parent, int index, SpecificationNode child)
        {
            parent.InsertChild(index, child);
            return new DocumentChange(DocumentChangeKind.ChildAdded, child, parent, index);
        }

        private static DocumentChange RemoveChild(SpecificationNode parent, SpecificationNode child)
        {
            int index = parent.RemoveChild(child);
            return new DocumentChange(DocumentChangeKind.ChildRemoved, child, parent, index);
        }

        private static DocumentChange MoveChild(
            SpecificationNode node,
            SpecificationNode fromParent,
            SpecificationNode toParent,
            int index)
        {
            fromParent.RemoveChild(node);
            toParent.InsertChild(index, node);
            return new DocumentChange(DocumentChangeKind.ChildMoved, node, toParent, index);
        }

        /// <summary>
        ///     Clears the comments above a node and adds the given ones, putting the previous ones back when the node refuses
        ///     one.
        /// </summary>
        /// <exception cref="ArgumentException">A comment is one the node refuses.</exception>
        private static DocumentChange ReplaceLeadingComments(
            SpecificationNode node,
            List<string> comments,
            List<string> previousComments)
        {
            node.ClearLeadingComments();

            try
            {
                foreach (string comment in comments)
                {
                    node.AddLeadingComment(comment);
                }
            }
            catch (ArgumentException)
            {
                node.ClearLeadingComments();

                foreach (string comment in previousComments)
                {
                    node.AddLeadingComment(comment);
                }

                throw;
            }

            return new DocumentChange(DocumentChangeKind.NotesSet, node, null, -1);
        }
    }
}
