using System;
using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>A convex volume described by the planes that bound it, with their normals facing outward.</summary>
    /// <remarks>
    ///     <para>
    ///         Containment is one dot product per plane with no ray casting, since a position is inside a convex volume
    ///         exactly when it is behind every plane. A normal pointing the wrong way turns the hull inside out, and a
    ///         symmetric hull hides that, so a spec should include a shape that is not symmetric.
    ///     </para>
    ///     <para>
    ///         Random positions are guessed inside the hull's bounding box and the first one behind every plane is kept. The
    ///         box is worked out from the hull's own corners, which are wherever three of its planes meet at a point that is
    ///         behind all the others. EnsureBounds does that once, the first time it is needed after AddPlane, since it visits
    ///         every triple of planes.
    ///     </para>
    ///     <para>
    ///         Planes alone can describe an unbounded volume, such as a slab between two parallel planes, and a volume with
    ///         no corners at all has no box, so TryGetRandomPosition fails. A volume that is open on one side but still has
    ///         corners, such as a box with no lid, cannot be told apart from a closed one by its corners. Its box covers only
    ///         the closed part, so samples come from there. That is a misconfiguration rather than something the region can
    ///         report.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class ConvexHullRegion : RegionBase
    {
        /// <summary>The fewest planes that can enclose a volume.</summary>
        private const int MinPlaneCount = 4;

        /// <summary>How far outside a plane a corner may sit and still pass IsOnHull, to absorb rounding.</summary>
        private const float CornerTolerance = 0.0001f;

        /// <summary>
        ///     How close three planes may come to having coplanar normals before TryIntersect treats them as not meeting at a
        ///     single point, measured on the determinant it computes.
        /// </summary>
        private const float ParallelTolerance = 0.000001f;

        private readonly List<Plane> _planes = new();

        /// <summary>True while the cached bounds reflect the current planes, so EnsureBounds can skip recomputing.</summary>
        private bool _boundsAreCurrent;

        private Vector3 _boundsMax;
        private Vector3 _boundsMin;

        /// <summary>True if the planes enclosed any corners the last time the bounds were computed.</summary>
        private bool _hasBounds;

        /// <summary>The planes bounding the volume, normals facing outward.</summary>
        public IReadOnlyList<Plane> Planes => _planes;

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <summary>Adds another plane to the boundary.</summary>
        internal void AddPlane(Plane plane)
        {
            _planes.Add(plane);
            _boundsAreCurrent = false;
        }

        /// <summary>Finds the box around the hull's corners.</summary>
        /// <returns>True if the hull has corners to put a box around.</returns>
        public bool TryGetBounds(out Vector3 min, out Vector3 max)
        {
            EnsureBounds();

            min = _boundsMin;
            max = _boundsMax;

            return _hasBounds;
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            if (_planes.Count == 0)
            {
                return false;
            }

            foreach (Plane plane in _planes)
            {
                if (plane.SignedDistance(position) > 0f)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            if (!TryGetBounds(out Vector3 min, out Vector3 max))
            {
                position = Vector3.Zero;

                return false;
            }

            return MathHelpers.TryRejectionSample(this, min, max, out position);
        }

        /// <summary>
        ///     Recomputes the bounds around the hull's corners if a plane has been added since they were last computed. The
        ///     corners come from intersecting every combination of three planes, which is worth caching.
        /// </summary>
        private void EnsureBounds()
        {
            if (_boundsAreCurrent)
            {
                return;
            }

            _boundsAreCurrent = true;
            _hasBounds = false;

            if (_planes.Count < MinPlaneCount)
            {
                return;
            }

            for (int firstIndex = 0; firstIndex < _planes.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1; secondIndex < _planes.Count; secondIndex++)
                {
                    for (int thirdIndex = secondIndex + 1; thirdIndex < _planes.Count; thirdIndex++)
                    {
                        if (!TryIntersect(_planes[firstIndex], _planes[secondIndex], _planes[thirdIndex],
                                out Vector3 corner))
                        {
                            continue;
                        }

                        if (IsOnHull(corner))
                        {
                            IncludeInBounds(corner);
                        }
                    }
                }
            }
        }

        /// <summary>Finds the one point where three planes meet, if they meet at a point rather than along a line.</summary>
        /// <seealso>Ronald Goldman, "Intersection of Three Planes", Graphics Gems, Academic Press, 1990</seealso>
        private static bool TryIntersect(Plane first, Plane second, Plane third, out Vector3 point)
        {
            // Each plane is the equation Dot(Normal, point) = Distance, so a meeting point solves three linear
            // equations at once with the cited closed form. The determinant is zero when the normals are coplanar,
            // which is when two of the planes are parallel or all three share a line, and then no single meeting
            // point exists.
            Vector3 secondCrossThird = Vector3.Cross(second.Normal, third.Normal);
            float determinant = Vector3.Dot(first.Normal, secondCrossThird);

            if (MathF.Abs(determinant) < ParallelTolerance)
            {
                point = Vector3.Zero;

                return false;
            }

            Vector3 thirdCrossFirst = Vector3.Cross(third.Normal, first.Normal);
            Vector3 firstCrossSecond = Vector3.Cross(first.Normal, second.Normal);

            point = (secondCrossThird * first.Distance + thirdCrossFirst * second.Distance +
                     firstCrossSecond * third.Distance) / determinant;

            return true;
        }

        /// <summary>Tests whether a point where three planes meet is also behind every other plane.</summary>
        private bool IsOnHull(Vector3 corner)
        {
            foreach (Plane plane in _planes)
            {
                if (plane.SignedDistance(corner) > CornerTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Widens the bounds to take in another corner, or starts them at it for the first corner found.</summary>
        private void IncludeInBounds(Vector3 corner)
        {
            if (!_hasBounds)
            {
                _hasBounds = true;
                _boundsMin = corner;
                _boundsMax = corner;

                return;
            }

            _boundsMin = new Vector3(MathF.Min(_boundsMin.X, corner.X), MathF.Min(_boundsMin.Y, corner.Y),
                MathF.Min(_boundsMin.Z, corner.Z));

            _boundsMax = new Vector3(MathF.Max(_boundsMax.X, corner.X), MathF.Max(_boundsMax.Y, corner.Y),
                MathF.Max(_boundsMax.Z, corner.Z));
        }
    }
}
