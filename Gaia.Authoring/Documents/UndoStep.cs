using System.Collections.Generic;

namespace Gaia.Authoring.Documents
{
    /// <summary>One entry of the undo stack, the edits one Undo takes back together and the label a menu shows for it.</summary>
    /// <remarks>
    ///     A command issued outside a transaction is an undo step of one edit. A transaction is an undo step of every
    ///     edit issued inside it.
    /// </remarks>
    internal sealed class UndoStep
    {
        private readonly List<Edit> _edits = new();

        /// <summary>Creates an empty undo step under the given label.</summary>
        public UndoStep(string label)
        {
            Label = label;
        }

        /// <summary>What a menu shows for the undo step, after "Undo" or "Redo".</summary>
        public string Label { get; }

        /// <summary>The edits in the order they were applied, which an undo walks backwards.</summary>
        public IReadOnlyList<Edit> Edits => _edits;

        /// <summary>Adds an edit after the ones the undo step already holds.</summary>
        public void Add(Edit edit)
        {
            _edits.Add(edit);
        }
    }
}
