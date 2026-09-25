using System;

namespace Gaia.Authoring.Documents
{
    /// <summary>One recorded change to the tree, with the call that applies it and the call that takes it back.</summary>
    /// <remarks>
    ///     Each call returns the change to announce. <see cref="Apply" /> is what ran when the command was issued and
    ///     runs again on redo, so a redo reaches the same tree through the same code, and <see cref="Revert" /> runs on undo.
    /// </remarks>
    internal sealed class Edit
    {
        /// <summary>Creates an edit from its two calls.</summary>
        public Edit(Func<DocumentChange> apply, Func<DocumentChange> revert)
        {
            Apply = apply;
            Revert = revert;
        }

        /// <summary>Makes the change and returns what to announce for it.</summary>
        public Func<DocumentChange> Apply { get; }

        /// <summary>Takes the change back and returns what to announce for that.</summary>
        public Func<DocumentChange> Revert { get; }
    }
}
