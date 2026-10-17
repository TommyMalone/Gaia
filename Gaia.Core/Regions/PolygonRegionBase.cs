using System;
using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;

namespace Gaia.Core.Regions
{
    /// <summary>
    ///     What <see cref="PolygonRegion" /> and <see cref="ExtrudedPolygonRegion" /> share, which is a footprint made of
    ///     corners in its own plane.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The footprint may be concave, which is what makes a polygon worth having over a rectangle. IsInFootprint
    ///         counts a point on an edge or at a corner as inside, and a point within 1 millimeter of one in the world too,
    ///         which absorbs the rounding of bringing it into the shape's plane. Any other point is judged by the PNPOLY
    ///         even-odd crossing test, in which a ray cast from the point along X crosses the boundary an odd number of times
    ///         if the point is inside.
    ///     </para>
    ///     <para>
    ///         A random point is found by guessing inside the footprint's bounds and keeping the first guess inside the
    ///         footprint. AddCorner keeps the bounds up to date, so reading them walks no corners. Each containment test walks
    ///         the corners once for the crossings, and once more for the edges' tolerance only when the crossings call the
    ///         point outside. Subclasses raise the point to the height they decide.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    /// <seealso>W. Randolph Franklin, PNPOLY, described at https://wrfranklin.org/Research/Short_Notes/pnpoly.html</seealso>
    public abstract class PolygonRegionBase : PlanarRegionBase
    {
        /// <summary>The fewest corners that enclose any area at all.</summary>
        private const int MinCornerCount = 3;

        private static readonly DescriptionBuilder CornerBuilder = new("One corner of the footprint.");

        /// <summary>Describes the Position attribute <see cref="LoadFootprint" /> reads a corner by.</summary>
        private static readonly AttributeDescription PositionAttribute = CornerBuilder.Required("Position",
            AttributeKind.Vector2,
            "Where the corner is in the shape's plane, measured from Origin, the first number along the shape's right and the second along its forward, the world's X and Z while the shape is unturned.");

        /// <summary>Describes the Corner children <see cref="LoadFootprint" /> reads by.</summary>
        private static readonly SlotDescription CornerSlot = new("Corner", "Corner", CornerBuilder.Build(),
            MinCornerCount, SlotDescription.Unbounded);

        private readonly List<Vector2> _corners = new();
        private Vector2 _boundsMax;
        private Vector2 _boundsMin;

        /// <summary>The corners of the footprint, in order around its edge, measured from Origin.</summary>
        public IReadOnlyList<Vector2> Corners => _corners;

        /// <summary>True once Corners has at least MinCornerCount entries.</summary>
        public bool HasArea => _corners.Count >= MinCornerCount;

        /// <summary>Loads the footprint's corners from the node's Corner children.</summary>
        /// <remarks>
        ///     Only the Corner children are read, so an OriginTarget beside them, which Init has loaded, is left alone. A
        ///     Corner with no Position fails the read here. The factory refuses fewer than three corners once the shape's Init
        ///     returns, since a footprint without area contains nothing and can only have been a mistake.
        /// </remarks>
        /// <returns>True when the corners loaded. False after reporting what went wrong.</returns>
        protected bool LoadFootprint(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name != CornerSlot.Name)
                {
                    continue;
                }

                if (!child.ReadVector2(PositionAttribute.Name, out Vector2 corner))
                {
                    return false;
                }

                AddCorner(corner);
            }

            return true;
        }

        /// <summary>Adds the next corner around the edge, widening the bounds to hold it.</summary>
        public void AddCorner(Vector2 corner)
        {
            if (_corners.Count == 0)
            {
                _boundsMin = _boundsMax = corner;
            }
            else
            {
                _boundsMin = new Vector2(MathF.Min(_boundsMin.X, corner.X), MathF.Min(_boundsMin.Y, corner.Y));
                _boundsMax = new Vector2(MathF.Max(_boundsMax.X, corner.X), MathF.Max(_boundsMax.Y, corner.Y));
            }

            _corners.Add(corner);
        }

        /// <summary>
        ///     Starts the description of a footprint shape, carrying the Corner children <see cref="LoadFootprint" /> reads
        ///     by on top of what DescribeShape carries, so a shape adds what it reads of its own and nothing else.
        /// </summary>
        /// <param name="summary">One line saying what the shape is, in a designer's words.</param>
        protected static DescriptionBuilder DescribeFootprint(string summary)
        {
            return DescribeShape(summary).Add(CornerSlot);
        }

        /// <summary>Tests whether a point of the plane is inside the footprint.</summary>
        protected bool IsInFootprint(Vector2 point)
        {
            // The on-edge walk can only turn an outside into an inside, so it runs only for the points the crossings
            // call outside. A point they call inside measures no edge's distance.
            return HasArea && (CrossesAnOddNumberOfEdges(point) || IsOnAnEdge(point));
        }

        /// <summary>Tests whether the ray cast from a point toward positive X crosses an odd number of edges.</summary>
        private bool CrossesAnOddNumberOfEdges(Vector2 point)
        {
            bool isInside = false;

            // Each edge runs from the previous corner to the current one. The previous index starts at the last corner
            // so that the first edge tested is the closing edge, from the last corner back around to the first.
            int previousCornerIndex = _corners.Count - 1;

            for (int cornerIndex = 0; cornerIndex < _corners.Count; cornerIndex++)
            {
                if (EdgeCrossesRay(_corners[previousCornerIndex], _corners[cornerIndex], point))
                {
                    isInside = !isInside;
                }

                previousCornerIndex = cornerIndex;
            }

            return isInside;
        }

        /// <summary>Tests whether a point lies on an edge or a corner of the footprint, or within ToleranceInPlane of one.</summary>
        private bool IsOnAnEdge(Vector2 point)
        {
            // Squared once for the walk rather than at every edge.
            float tolerance = ToleranceInPlane;
            float squaredTolerance = tolerance * tolerance;

            int previousCornerIndex = _corners.Count - 1;

            for (int cornerIndex = 0; cornerIndex < _corners.Count; cornerIndex++)
            {
                if (IsOnEdge(_corners[previousCornerIndex], _corners[cornerIndex], point, squaredTolerance))
                {
                    return true;
                }

                previousCornerIndex = cornerIndex;
            }

            return false;
        }

        /// <summary>Guesses points inside the bounds until one is inside the footprint, or the attempts run out.</summary>
        /// <returns>True if a guess landed inside. False when there is no area, or every attempt missed.</returns>
        protected bool TryGetRandomFootprintPoint(out Vector2 point)
        {
            if (HasArea)
            {
                for (int attempt = 0; attempt < MathHelpers.MaxSampleAttempts; attempt++)
                {
                    point = new Vector2(MathHelpers.RandomInRange(RandomSource, _boundsMin.X, _boundsMax.X),
                        MathHelpers.RandomInRange(RandomSource, _boundsMin.Y, _boundsMax.Y));

                    if (IsInFootprint(point))
                    {
                        return true;
                    }
                }
            }

            point = Vector2.Zero;

            return false;
        }

        /// <summary>Tests whether an edge crosses the ray cast from the point toward positive X.</summary>
        private static bool EdgeCrossesRay(Vector2 edgeStart, Vector2 edgeEnd, Vector2 point)
        {
            // An edge can only cross the ray if its two ends lie on opposite sides of the ray's Y.
            bool straddlesRay = edgeStart.Y > point.Y != edgeEnd.Y > point.Y;

            if (!straddlesRay)
            {
                return false;
            }

            // Where along X the edge passes through the ray's Y. The division is safe, since straddling the ray means
            // the two ends have different Y.
            float fractionAlongEdge = (point.Y - edgeStart.Y) / (edgeEnd.Y - edgeStart.Y);
            float crossingX = edgeStart.X + (edgeEnd.X - edgeStart.X) * fractionAlongEdge;

            // The ray runs toward positive X, so only a crossing ahead of the point counts.
            return crossingX > point.X;
        }

        /// <summary>
        ///     Tests whether a point lies on the edge between two corners, either corner included, or within a tolerance of
        ///     it, which is given squared.
        /// </summary>
        private static bool IsOnEdge(Vector2 edgeStart, Vector2 edgeEnd, Vector2 point, float squaredTolerance)
        {
            Vector2 edge = edgeEnd - edgeStart;
            Vector2 startToPoint = point - edgeStart;
            float squaredEdgeLength = edge.SquaredMagnitude;

            // Where the edge's nearest point to the point lies along it, from 0 at its start to 1 at its end. Held to
            // that range, a point beyond either end is measured from the corner there. Two corners at the same place
            // make an edge with no length, whose nearest point is that place.
            float fractionAlongEdge = squaredEdgeLength > 0f
                ? Math.Clamp(Vector2.Dot(startToPoint, edge) / squaredEdgeLength, 0f, 1f)
                : 0f;
            Vector2 nearestOnEdge = edgeStart + edge * fractionAlongEdge;

            return Vector2.SquaredDistance(nearestOnEdge, point) <= squaredTolerance;
        }
    }
}
