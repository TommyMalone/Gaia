using Gaia.Core.Specifications;

namespace Gaia.Authoring.Documents
{
    /// <summary>One change an <see cref="AuthoringDocument" /> made to its tree, as its Changed event announces it.</summary>
    /// <remarks>
    ///     A view that keeps a row per node finds the row through <see cref="Node" />, and for a child change finds the
    ///     row it now sits under through <see cref="Parent" /> and its place through <see cref="Index" />. An undo announces
    ///     the inverse of the change it takes back, so a removal undone is announced as an addition at the index the node
    ///     stood at.
    /// </remarks>
    public readonly struct DocumentChange
    {
        /// <summary>Creates the record of one change.</summary>
        internal DocumentChange(DocumentChangeKind kind, SpecificationNode node, SpecificationNode? parent, int index)
        {
            Kind = kind;
            Node = node;
            Parent = parent;
            Index = index;
        }

        /// <summary>What was done.</summary>
        public DocumentChangeKind Kind { get; }

        /// <summary>
        ///     The node the change concerns, which is the node whose attribute or comments changed, or the child that was
        ///     added, removed or moved.
        /// </summary>
        public SpecificationNode Node { get; }

        /// <summary>
        ///     For a child change, the parent the node was added beneath, removed from or moved beneath. Null for an
        ///     attribute or notes change.
        /// </summary>
        public SpecificationNode? Parent { get; }

        /// <summary>
        ///     For a child change, the index among the parent's children the node was added at, removed from or moved to. -1
        ///     for an attribute or notes change.
        /// </summary>
        public int Index { get; }
    }
}
