using System;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>A region that is a flat shape, described in a plane of its own coordinates.</summary>
    /// <remarks>
    ///     <para>
    ///         The shape is described in a plane of its own coordinates, so a rectangle's half size is a
    ///         <see cref="Vector2" /> and a footprint is a ring of Vector2 corners, and each shape's arithmetic is the flat
    ///         kind whichever way it is turned. The plane's X is local X and its Y is local Z, so an unturned shape lies on
    ///         the ground with its Y along the world's Z. Height is local Y, ignored by the shapes that are only a footprint
    ///         and bounded by the ones that are extruded.
    ///     </para>
    ///     <para>
    ///         In a 2D game the game's plane stands upright, so an unturned flat shape lies across it and holds every
    ///         position along the game's up. Such a game uses a SphereRegion or a BoxRegion, which test all three axes, or
    ///         lays the flat shape on its plane with a Rotation of (-90, 0, 0) for Unity's XY, or (-90, -90, 0) for Unreal's
    ///         Paper2D once its axes are converted, and turns it within that plane in code as that turn times a yaw, which
    ///         turns the shape about its own up. A concave footprint has no such stand-in and needs the turn.
    ///     </para>
    ///     <para>
    ///         A shape that ignores height hands out a random position in its own plane, through Origin, and projects it
    ///         onto nothing, so the game drops it onto its ground or navmesh itself.
    ///     </para>
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
