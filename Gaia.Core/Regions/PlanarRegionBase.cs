using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>A region that is a flat shape, described in a plane of its own coordinates.</summary>
    /// <remarks>
    ///     The shape is described in a plane of its own coordinates, so a rectangle's half size is a
    ///     <see cref="Vector2" /> and a footprint is a ring of Vector2 corners, and each shape's arithmetic is the flat kind
    ///     whichever way it is turned. The plane's X is local X and its Y is local Z, so an unturned shape lies on the ground
    ///     with its Y along the world's Z. Height is local Y, ignored by the shapes that are only a footprint and bounded by
    ///     the ones that are extruded.
    /// </remarks>
    public abstract class PlanarRegionBase : RegionBase
    {
        /// <summary>
        ///     <see cref="RegionBase.BoundaryTolerance" /> in the shape's own units for a comparison that mixes the plane's
        ///     two axes, local X and Z, a distance from the shape's axis or from an edge. It is the larger of ToleranceAlongAxes
        ///     on those two, 1 millimeter in the world divided by the smaller size of Scale on them, so it is never less than 1
        ///     millimeter in the world along either.
        /// </summary>
        internal float ToleranceInPlane => MathF.Max(ToleranceAlongAxes.X, ToleranceAlongAxes.Z);

        /// <summary>
        ///     Starts the description of a flat shape, carrying the Origin and Rotation attributes
        ///     <see cref="RegionBase.Init" /> reads by, so a shape adds what it reads of its own and nothing else.
        /// </summary>
        /// <param name="summary">One line saying what the shape is, in a designer's words.</param>
        protected static DescriptionBuilder DescribeFlatShape(string summary)
        {
            return new DescriptionBuilder(ConceptualAbstraction.Regions, summary).Add(OriginAttribute)
                .Add(RotationAttribute);
        }

        /// <summary>Splits a point in the shape's own coordinates into a point of the plane and its height above the plane.</summary>
        protected static Vector2 InPlane(Vector3 local, out float height)
        {
            height = local.Y;

            return new Vector2(local.X, local.Z);
        }

        /// <summary>Raises a point of the plane to a height above it, giving a point in the shape's own coordinates.</summary>
        protected static Vector3 OutOfPlane(Vector2 point, float height)
        {
            return new Vector3(point.X, height, point.Y);
        }
    }
}
