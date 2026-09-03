using System;
using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Regions
{
    /// <summary>
    ///     What <see cref="PolygonRegion" /> and <see cref="ExtrudedPolygonRegion" /> share, which is a footprint in the
    ///     ground plane made of corners.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The footprint may be concave, which is what makes a polygon worth having over a rectangle. IsInFootprint is
    ///         the PNPOLY even-odd crossing test. A ray cast from the position along X crosses the boundary an odd number of
    ///         times if the position is inside. A position exactly on an edge can land either way, which no caller here
    ///         depends on.
    ///     </para>
    ///     <para>
    ///         Random positions are found by guessing inside BoundsMin and BoundsMax and keeping the first guess inside the
    ///         footprint. The bounds are kept up to date by AddCorner, so neither operation walks the corners more than once.
    ///         Subclasses decide the height, since that is the only thing that separates them.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    /// <seealso>W. Randolph Franklin, PNPOLY, described at https://wrfranklin.org/Research/Short_Notes/pnpoly.html</seealso>
    public abstract class PolygonRegionBase : RegionBase
    {
        /// <summary>The fewest corners that enclose any area at all.</summary>
        private const int MinCornerCount = 3;

        private static readonly AiString CornerElement = "Corner";
        private static readonly AiString PositionAttribute = "Position";

        private readonly List<Vector3> _corners = new();
        private float _boundsMaxX;
        private float _boundsMaxZ;
        private float _boundsMinX;
        private float _boundsMinZ;

        /// <summary>The corners of the footprint, in order around its edge. Only X and Z are used.</summary>
        public IReadOnlyList<Vector3> Corners => _corners;

        /// <summary>True once Corners has at least MinCornerCount entries.</summary>
        public bool HasArea => _corners.Count >= MinCornerCount;

        /// <summary>The smallest X and Z of any corner.</summary>
        protected Vector3 BoundsMin => new(_boundsMinX, 0f, _boundsMinZ);

        /// <summary>The largest X and Z of any corner.</summary>
        protected Vector3 BoundsMax => new(_boundsMaxX, 0f, _boundsMaxZ);

        /// <summary>Adds the next corner around the edge.</summary>
        /// <summary>Loads the footprint's corners from the node's Corner children.</summary>
        /// <remarks>
        ///     Every child must be a Corner element carrying a Position, and fewer than three corners is refused, since a
        ///     footprint without area contains nothing and can only have been a mistake.
        /// </remarks>
        internal bool LoadFootprint(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name != CornerElement)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
                }

                if (!child.ReadVector3(PositionAttribute, out Vector3 corner))
                {
                    return false;
                }

                AddCorner(corner);
            }

            if (!HasArea)
            {
                creationData.ReportError(
                    $"Line {node.LineNumber}, the {node.Name.Text} element has fewer than {MinCornerCount} Corner elements");
                return false;
            }

            return true;
        }

        internal void AddCorner(Vector3 corner)
        {
            if (_corners.Count == 0)
            {
                _boundsMinX = _boundsMaxX = corner.X;
                _boundsMinZ = _boundsMaxZ = corner.Z;
            }
            else
            {
                _boundsMinX = MathF.Min(_boundsMinX, corner.X);
                _boundsMaxX = MathF.Max(_boundsMaxX, corner.X);
                _boundsMinZ = MathF.Min(_boundsMinZ, corner.Z);
                _boundsMaxZ = MathF.Max(_boundsMaxZ, corner.Z);
            }

            _corners.Add(corner);
        }

        /// <summary>Tests whether a position is inside the footprint, ignoring height.</summary>
        protected bool IsInFootprint(Vector3 position)
        {
            if (!HasArea)
            {
                return false;
            }

            bool isInside = false;

            // Each edge runs from the previous corner to the current one. The previous index starts at the last corner
            // so that the first edge tested is the closing edge, from the last corner back around to the first.
            int previousCornerIndex = _corners.Count - 1;

            for (int cornerIndex = 0; cornerIndex < _corners.Count; cornerIndex++)
            {
                Vector3 edgeStart = _corners[previousCornerIndex];
                Vector3 edgeEnd = _corners[cornerIndex];

                if (EdgeCrossesRay(edgeStart, edgeEnd, position))
                {
                    isInside = !isInside;
                }

                previousCornerIndex = cornerIndex;
            }

            return isInside;
        }

        /// <summary>Tests whether an edge crosses the ray cast from the position toward positive X.</summary>
        private static bool EdgeCrossesRay(Vector3 edgeStart, Vector3 edgeEnd, Vector3 position)
        {
            // An edge can only cross the ray if its two ends lie on opposite sides of the ray's Z.
            bool straddlesRay = edgeStart.Z > position.Z != edgeEnd.Z > position.Z;

            if (!straddlesRay)
            {
                return false;
            }

            // Where along X the edge passes through the ray's Z. The division is safe, since straddling the ray means
            // the two ends have different Z.
            float fractionAlongEdge = (position.Z - edgeStart.Z) / (edgeEnd.Z - edgeStart.Z);
            float crossingX = edgeStart.X + (edgeEnd.X - edgeStart.X) * fractionAlongEdge;

            // The ray runs toward positive X, so only a crossing ahead of the position counts.
            return crossingX > position.X;
        }
    }
}
