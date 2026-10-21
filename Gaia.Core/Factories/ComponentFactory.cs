using System;
using System.Collections.Generic;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Factories
{
    /// <summary>
    ///     Builds the components of the kind whose abstract base is TBase, looking each specification node's Type up
    ///     among the registered creators and initializing what it constructs.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A game adds its own components by registering new names, and replaces a built-in by registering that name
    ///         again.
    ///     </para>
    ///     <para>
    ///         The factory constructs and initializes, it does not inject. A component's Init pulls whatever services and
    ///         references it wants from the creation data, so the factory never needs to know one component's needs from
    ///         another's.
    ///     </para>
    /// </remarks>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    /// <typeparam name="TBase">The conceptual abstraction every registered creator builds.</typeparam>
    public sealed class ComponentFactory<TBase> : IComponentFactory where TBase : class, IAiComponent
    {
        private readonly Dictionary<AiString, Func<TBase>> _creators = new();
        private readonly Dictionary<AiString, ComponentDescription> _descriptions = new();
        private readonly List<string> _typeSpellings = new();
        private readonly List<AiString> _types = new();

        /// <inheritdoc />
        /// <remarks>A name registered again keeps its place, since only its creator, description and spelling change.</remarks>
        public IReadOnlyList<AiString> Types => _types;

        /// <inheritdoc />
        public IReadOnlyList<string> TypeSpellings => _typeSpellings;

        /// <inheritdoc />
        public bool TryGetDescription(AiString type, out ComponentDescription? description)
        {
            return _descriptions.TryGetValue(type, out description);
        }

        /// <summary>Registers a creator and its description under a type name, replacing both if that name already had them.</summary>
        /// <param name="type">The Type attribute value that picks this creator, in the spelling a tool writes it.</param>
        /// <param name="creator">Constructs one uninitialized component. The factory runs Init afterward.</param>
        /// <param name="description">
        ///     What the component's element may hold, which the factory holds the element to and a tool
        ///     reads.
        /// </param>
        public void Register(string type, Func<TBase> creator, ComponentDescription description)
        {
            AiString name = type;
            int index = _types.IndexOf(name);

            if (index < 0)
            {
                _types.Add(name);
                _typeSpellings.Add(type);
            }
            else
            {
                _typeSpellings[index] = type;
            }

            _creators[name] = creator;
            _descriptions[name] = description;
        }

        /// <summary>Builds and initializes the component the creation data's node describes.</summary>
        /// <remarks>
        ///     The element is refused for having no Type, then for a Type nothing registered, then for anything its type's
        ///     description does not name. Init runs next, and an Init that fails is refused. Once Init returns true, a slot short
        ///     of its minimum is refused. Where an Init refuses a short slot itself, its sentence is reported rather than the one
        ///     the minimum check writes, since Init runs before the minimums are counted. The description is checked before the
        ///     creator runs, so a refused element constructs nothing. The component is handed out only once every check has
        ///     passed.
        /// </remarks>
        /// <returns>True when the component was built. False after reporting what went wrong.</returns>
        public bool TryCreate(AiCreationData creationData, out TBase? component)
        {
            component = null;

            SpecificationNode node = creationData.Node;

            if (node.Type.IsEmpty)
            {
                Report(creationData, $"Line {node.LineNumber}, the {node.NameSpelling} element has no Type attribute");
                return false;
            }

            if (!_creators.TryGetValue(node.Type, out Func<TBase>? creator))
            {
                Report(creationData,
                    $"Line {node.LineNumber}, the {node.NameSpelling} element's type {node.TypeSpelling} is not registered");
                return false;
            }

            ComponentDescription description = _descriptions[node.Type];

            if (!description.RefuseUndescribed(creationData, false))
            {
                return false;
            }

            TBase created = creator();

            if (!created.Init(creationData))
            {
                Report(creationData,
                    $"Line {node.LineNumber}, the {node.NameSpelling} element's type {node.TypeSpelling} failed to initialize");
                return false;
            }

            if (!description.RefuseShortSlots(creationData))
            {
                return false;
            }

            component = created;
            return true;
        }

        /// <summary>Reports a creation failure, filed under the node's source name.</summary>
        private static void Report(AiCreationData creationData, string message)
        {
            creationData.ReportError(message);
        }
    }
}
