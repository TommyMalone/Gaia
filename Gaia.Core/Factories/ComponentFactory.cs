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
    ///         Registering a type that is already registered replaces it. A game adds its own components by registering new
    ///         names, and replaces a built-in by registering that name again, so the last registration is the one kept. Every
    ///         registration carries a <see cref="ComponentDescription" />, which is what a tool reads to learn what the
    ///         component's element may hold, and <see cref="Types" /> lists the registered names for it.
    ///     </para>
    ///     <para>
    ///         The factory constructs and initializes, it does not inject. A component's Init pulls whatever services and
    ///         references it wants from the creation data, so the factory never needs to know one component's needs from
    ///         another's. Failures are reported through the creation data's sink, filed under the node's source name.
    ///     </para>
    /// </remarks>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    /// <typeparam name="TBase">The conceptual abstraction every registered creator builds.</typeparam>
    public sealed class ComponentFactory<TBase> : IComponentFactory where TBase : class, IAiComponent
    {
        private readonly Dictionary<AiString, Func<TBase>> _creators = new();
        private readonly Dictionary<AiString, ComponentDescription> _descriptions = new();
        private readonly List<AiString> _types = new();

        /// <inheritdoc />
        /// <remarks>A name registered again keeps its place, since only its creator and description change.</remarks>
        public IReadOnlyList<AiString> Types => _types;

        /// <inheritdoc />
        public bool TryGetDescription(AiString type, out ComponentDescription? description)
        {
            return _descriptions.TryGetValue(type, out description);
        }

        /// <summary>Registers a creator and its description under a type name, replacing both if that name already had them.</summary>
        /// <param name="type">The Type attribute value that picks this creator.</param>
        /// <param name="creator">Constructs one uninitialized component. The factory runs Init afterward.</param>
        /// <param name="description">What the component's element may hold, for a tool that edits a document without loading it.</param>
        public void Register(AiString type, Func<TBase> creator, ComponentDescription description)
        {
            if (!_creators.ContainsKey(type))
            {
                _types.Add(type);
            }

            _creators[type] = creator;
            _descriptions[type] = description;
        }

        /// <summary>Builds and initializes the component the creation data's node describes.</summary>
        /// <returns>True when the component was built. False after reporting what went wrong.</returns>
        public bool TryCreate(AiCreationData creationData, out TBase? component)
        {
            component = null;

            SpecificationNode node = creationData.Node;

            if (node.Type.IsEmpty)
            {
                Report(creationData, $"Line {node.LineNumber}, the {node.Name.Text} element has no Type attribute");
                return false;
            }

            if (!_creators.TryGetValue(node.Type, out Func<TBase>? creator))
            {
                Report(creationData,
                    $"Line {node.LineNumber}, the {node.Name.Text} element's type {node.Type.Text} is not registered");
                return false;
            }

            TBase created = creator();

            if (!created.Init(creationData))
            {
                Report(creationData,
                    $"Line {node.LineNumber}, the {node.Name.Text} element's type {node.Type.Text} failed to initialize");
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
