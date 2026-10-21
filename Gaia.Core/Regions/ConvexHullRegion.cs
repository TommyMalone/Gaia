using System;
using System.Collections.Generic;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;

namespace Gaia.Core.Regions
{
    /// <summary>A convex volume described by the planes that bound it, with their normals facing outward.</summary>
    /// <remarks>
    ///     <para>
    ///         Containment is one dot product per plane with no ray casting, since a position is inside a convex volume
    ///         exactly when it is behind every plane. Neither OnInit nor AddPlane checks which way a normal faces. A plane
    ///         whose normal points into the volume has the volume in front of it, so IsInRegion rejects the positions the hull
    ///         was meant to hold and accepts only those past that plane that the other planes allow.
    ///     </para>
    ///     <para>
    ///         The planes are written in the hull's own coordinates, each Distance measured along its normal from Origin, so
    ///         a hull left at the world's origin, unturned and unscaled, reads them as world planes. Nothing about the hull is
    ///         stored in the world's coordinates, so moving or turning it changes no plane and no bound.
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

        private static readonly DescriptionBuilder PlaneBuilder = new("One plane bounding the volume.");

        private static readonly AttributeDescription NormalAttribute = PlaneBuilder.Required("Normal",
            AttributeKind.Vector3, "Which way the plane faces, pointing out of the volume.");

        private static readonly AttributeDescription DistanceAttribute = PlaneBuilder.Required("Distance",
            AttributeKind.Float,
            "How far the plane sits from the hull's Origin along its normal, in the hull's own coordinates. The Normal's length does not change it. This is the negative of Unity's Plane.distance.");

        private static readonly DescriptionBuilder Builder = DescribeShape("A volume bounded by planes.");

        private static readonly SlotDescription PlaneSlot = Builder.Slot("Plane", PlaneBuilder.Build(), MinPlaneCount,
            SlotDescription.Unbounded);

        internal static readonly ComponentDescription Description = Builder.Build();

        private readonly List<Plane> _planes = new();

        /// <summary>True while the cached bounds reflect the current planes, so EnsureBounds can skip recomputing.</summary>
        private bool _boundsAreCurrent;

        private Vector3 _boundsMax;
        private Vector3 _boundsMin;

        /// <summary>True if the planes enclosed any corners the last time the bounds were computed.</summary>
        private bool _hasBounds;

        /// <summary>The planes bounding the volume, in the hull's own coordinates, normals facing outward.</summary>
        public IReadOnlyList<Plane> LocalPlanes => _planes;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            // The factory refused any child the description does not name, and Init has loaded the OriginTarget, so
            // only the Plane children are read here.
            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name != PlaneSlot.Name)
                {
                    continue;
                }

                // Stryker disable once all : a mutant of an out variable's condition used after the if does not compile
                if (!child.ReadVector3(NormalAttribute.Name, out Vector3 normal) ||
                    !child.ReadFloat(DistanceAttribute.Name, out float distance))
                {
                    return false;
                }

                if (normal.SquaredMagnitude == 0f)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {child.NameSpelling} element's Normal has no length");
                    return false;
                }

                // The Normal is brought to unit length here, so the plane stands at the Distance written. The Plane
                // constructor would otherwise divide the Distance by the Normal's length as well.
                AddPlane(new Plane(normal / normal.Magnitude, distance));
            }

            // Refused here rather than left to the factory's count of the slot, since the sentence gives the reason
            // for the minimum, which a count alone cannot.
            if (_planes.Count < MinPlaneCount)
            {
                creationData.ReportError(
                    $"Line {node.LineNumber}, the {node.NameSpelling} element has fewer than {MinPlaneCount} Plane elements, too few to close a volume");
                return false;
            }

            return true;
        }

        /// <summary>Adds another plane to the boundary, in the hull's own coordinates.</summary>
        public void AddPlane(Plane plane)
        {
            _planes.Add(plane);
            _boundsAreCurrent = false;
        }

        /// <summary>Finds the box around the hull's corners, in the hull's own coordinates.</summary>
        /// <returns>True if the hull has corners to put a box around.</returns>
        public bool TryGetLocalBounds(out Vector3 min, out Vector3 max)
        {
            EnsureBounds();

            min = _boundsMin;
            max = _boundsMax;

            return _hasBounds;
        }

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            if (_planes.Count == 0)
            {
                return false;
            }

            float tolerance = ToleranceInAnyDirection;

            foreach (Plane plane in _planes)
            {
                if (plane.SignedDistance(local) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        /// <remarks>
        ///     <para>
        ///         The point is guessed inside the box <see cref="TryGetLocalBounds" /> finds, in the hull's own coordinates,
        ///         and the first guess behind every plane is kept.
        ///     </para>
        ///     <para>
        ///         A hull whose planes meet at no corner has no box, so the pick fails. A hull open on one side can still have
        ///         corners, and then its box spans those corners alone, so every guess comes from the closed end. A box with no
        ///         lid is one. Its corners do not show that it is open, so the region cannot report the mistake.
        ///     </para>
        /// </remarks>
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            if (!TryGetLocalBounds(out Vector3 min, out Vector3 max))
            {
                local = Vector3.Zero;

                return false;
            }

            return TryRejectionSample(min, max, out local);
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
            // and then the planes have no single meeting point.
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
