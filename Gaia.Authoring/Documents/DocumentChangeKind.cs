namespace Gaia.Authoring.Documents
{
    /// <summary>What one <see cref="DocumentChange" /> did to the tree.</summary>
    public enum DocumentChangeKind
    {
        /// <summary>An attribute's value was set, or the attribute was added when the node did not carry it.</summary>
        AttributeSet,

        /// <summary>An attribute was removed.</summary>
        AttributeRemoved,

        /// <summary>A node was added beneath a parent, the subtree beneath it included.</summary>
        ChildAdded,

        /// <summary>A node was removed from its parent, the subtree beneath it included.</summary>
        ChildRemoved,

        /// <summary>A node was moved to a parent and an index, which within one parent is a reorder.</summary>
        ChildMoved,

        /// <summary>The comments above a node were replaced.</summary>
        NotesSet,
    }
}
