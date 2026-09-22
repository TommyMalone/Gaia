namespace Gaia.Core.Factories
{
    /// <summary>The kinds of value an attribute is read as, which is what a tool edits it as.</summary>
    /// <remarks>
    ///     The first seven are the typed accessors of <see cref="Specifications.SpecificationNode" />, ReadBool through
    ///     ReadRotation. An Enumeration is read as one of a fixed set of names, which
    ///     <see cref="AttributeDescription.Choices" /> lists. A VariableName is the name of a value on a data store, which
    ///     <see cref="AttributeDescription.VariableKind" /> says the kind of.
    /// </remarks>
    public enum AttributeKind
    {
        Bool,
        Int,
        Float,
        String,
        Vector2,
        Vector3,
        Rotation,
        Enumeration,
        VariableName,
    }
}
