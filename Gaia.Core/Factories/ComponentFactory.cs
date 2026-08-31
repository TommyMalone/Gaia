using System;
using System.Collections.Generic;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Factories
{
    /// <summary>
    ///     Builds the components of one conceptual abstraction, looking each specification node's Type up among the
    ///     registered creators and initializing what it constructs.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Registering a type that is already registered replaces it, which is both halves of extensibility in one rule.
    ///         A game adds its own components by registering new names, and replaces a built-in by registering that name
    ///         again, so the last registration always wins.
    ///     </para>
    ///     <para>
    ///         The factory constructs and initializes, it does not inject. A component's Init pulls whatever services and
    ///         references it wants from the creation data, so the factory never needs to know one component's needs from
    ///         another's. Failures are reported through the creation data's sink, filed under the node's source name.
    ///     </para>
    /// </remarks>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    /// <typeparam name="TBase">The conceptual abstraction every registered creator builds.</typeparam>
    public sealed class ComponentFactory<TBase> where TBase : class, IAiComponent
    {
        private readonly Dictionary<AiString, Func<TBase>> _creators = new();

        /// <summary>Registers a creator under a type name, replacing any creator that name already had.</summary>
        /// <param name="type">The Type attribute value that picks this creator.</param>
        /// <param name="creator">Constructs one uninitialized component. The factory runs Init afterward.</param>
        public void Register(AiString type, Func<TBase> creator)
        {
            _creators[type] = creator;
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
