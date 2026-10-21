using System;

namespace Gaia.Authoring.Documents
{
    /// <summary>One recorded change to the tree, with the call that applies it and the call that takes it back.</summary>
    /// <remarks>
    ///     A redo runs <see cref="Apply" /> again rather than a separate call, so it reaches the same tree through the
    ///     same code that ran when the command was issued.
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
