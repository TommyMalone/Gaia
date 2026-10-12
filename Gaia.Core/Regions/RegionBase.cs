using System;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Targets;

namespace Gaia.Core.Regions
{
    /// <summary>An area of the world that a configuration can name, such as a kill zone or a line of retreat.</summary>
    /// <remarks>
    ///     <para>
    ///         A region answers two questions, whether a position is inside it and where a random position inside it would
    ///         be. The second is allowed to fail, because a shape that has to guess and check can run out of attempts, and the
    ///         caller has to be told rather than handed a position outside the region.
    ///     </para>
    ///     <para>
    ///         Every shape is described in its own coordinates, as the same shape standing unturned at the world's origin
    ///         would be, and Origin, Rotation and Scale place it in the world. While the shape follows OriginTarget, the
    ///         target's position stands in for Origin.
    ///     </para>
    ///     <para>
    ///         RandomSource is injected the same way it is for <see cref="DualUtilityReasoner" />, so a test can make
    ///         sampling deterministic.
    ///     </para>
    ///     <para>
    ///         Regions are built in code as well as loaded, so their values and RandomSource are public and a host may build
    ///         its shapes in code, the way a game sorts contacts into the entity lists its considerations read. Loading fills
    ///         the same members.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6, Listing 8.6</seealso>
    public abstract class RegionBase : IAiComponent
    {
        /// <summary>
        ///     How far outside its boundary a point may lie and still count as inside, 1 millimeter in the world at any
        ///     Scale.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         A point exactly on a boundary in the world is brought into the shape's own coordinates through the transform
        ///         that places the shape. Each operation of that rounds in float, so the point can arrive a little outside the
        ///         boundary and count as outside. A float's step, the gap between one float and the next, grows with the size of
        ///         the coordinate it holds, and so does the drift. Worked out in float, at any Scale, a point on the boundary of a
        ///         shape reaching less than about 500 meters from its own origin drifts less than a millimeter within about 8
        ///         kilometers of the world's origin when the shape is turned, and within about 16 kilometers when it is not, or 32
        ///         for a box or a rectangle. Farther out, or on a larger shape, a point on the boundary can drift past the
        ///         tolerance and count as outside. A turn's axes, as Rotation.FromEulerAngles works them out, are not quite of
        ///         length 1, which moves a point on a turned shape up to about a millimeter for each kilometer it lies from the
        ///         shape's own origin. BoundaryToleranceOverDistanceSpec holds the boundaries of shapes at fixed places from 100
        ///         meters to 32 kilometers from the world's origin, and says how far the points at each place drift.
        ///     </para>
        ///     <para>
        ///         Each shape compares in its own coordinates, where the tolerance along one of its axes is this divided by the
        ///         size of Scale on that axis. A comparison that mixes axes, a distance from a middle, an axis, a segment, a plane
        ///         or an edge, divides by the smallest size of Scale among the axes it mixes, so the tolerance is never less than
        ///         1 millimeter in the world along any of them, and is more along an axis scaled further.
        ///     </para>
        ///     <para>
        ///         Unreal's math header defines THRESH_POINT_ON_PLANE, 0.10 centimeters, as a plane's thickness, the same
        ///         millimeter, and neither Unreal's nor Unity's box test documents a tolerance. A shape a game writes picks its
        ///         own.
        ///     </para>
        /// </remarks>
        internal const float BoundaryTolerance = 0.001f;

        /// <summary>Describes the optional Origin attribute <see cref="Init" /> reads by.</summary>
        protected static readonly AttributeDescription OriginAttribute = AttributeDescription.Optional("Origin",
            "Origin", AttributeKind.Vector3, "(0, 0, 0)",
            "Where the shape's (0, 0, 0) is in the world, unless an OriginTarget stands in.");

        /// <summary>
        ///     Describes the optional OriginTarget child <see cref="LoadOriginTarget" /> reads by, for the description of
        ///     every shape that calls it.
        /// </summary>
        /// <remarks>
        ///     Circle, Cylinder and Sphere read this child and the other six shapes do not. Two of the three are flat and the
        ///     third is not, so the slot cannot ride DescribeFlatShape, which gives every flat shape its Origin and Rotation
        ///     attributes, and each shape that reads it adds the slot to its own description.
        /// </remarks>
        protected static readonly SlotDescription OriginTargetSlot = new("OriginTarget", "OriginTarget",
            ConceptualAbstraction.Targets, 0, 1);

        /// <summary>Describes the optional Rotation attribute <see cref="Init" /> reads by.</summary>
        protected static readonly AttributeDescription RotationAttribute = AttributeDescription.Optional("Rotation",
            "Rotation", AttributeKind.Rotation, "(0, 0, 0)",
            "How the shape is turned from the world's axes, as pitch, yaw and roll in degrees, pitch positive toward the ground and yaw positive to the right.");

        private Transform _transform = Transform.Identity;

        /// <summary>
        ///     Where the shape's (0, 0, 0) is in the world, unless OriginTarget's position stands in for it. The world's
        ///     origin until it is set.
        /// </summary>
        public Vector3 Origin
        {
            get => _transform.Origin;
            set => _transform = _transform.WithOrigin(value);
        }

        /// <summary>How the shape is turned, relative to the world. No turn until it is set.</summary>
        public Rotation Rotation
        {
            get => _transform.Rotation;
            set => _transform = new Transform(_transform.Origin, value, _transform.Scale);
        }

        /// <summary>How far one of the shape's own units stretches along its right, up and forward. (1, 1, 1) until it is set.</summary>
        /// <remarks>A zero component of Scale leaves the shape containing nothing and its sampling failing.</remarks>
        public Vector3 Scale
        {
            get => _transform.Scale;
            set
            {
                _transform = new Transform(_transform.Origin, _transform.Rotation, value);
                ToleranceAlongAxes = new Vector3(ToleranceAlong(value.X), ToleranceAlong(value.Y),
                    ToleranceAlong(value.Z));
            }
        }

        /// <summary>The target whose position stands in for Origin while one is set, or null to place the shape at Origin.</summary>
        /// <remarks>
        ///     While it is set, the target's position is read at every query, and the shape keeps its own Rotation and Scale.
        ///     A target that cannot answer leaves the shape containing nothing and its sampling failing.
        /// </remarks>
        public TargetBase? OriginTarget { get; set; }

        /// <summary>Where the region draws the numbers that pick a random position.</summary>
        public IRandomSource RandomSource { get; set; } = new Xoshiro128RandomSource();

        /// <summary>
        ///     <see cref="BoundaryTolerance" /> in the shape's own units along each of its own axes, 1 millimeter in the
        ///     world divided by the size of Scale on that axis, for a comparison along one axis.
        /// </summary>
        /// <remarks>Divided when Scale is set rather than on every containment test.</remarks>
        internal Vector3 ToleranceAlongAxes { get; private set; } =
            new(BoundaryTolerance, BoundaryTolerance, BoundaryTolerance);

        /// <summary>
        ///     <see cref="BoundaryTolerance" /> in the shape's own units for a comparison that mixes all three of its axes, a
        ///     distance from its middle, its segment or a plane. It is the largest of ToleranceAlongAxes, 1 millimeter in the
        ///     world divided by the smallest size of Scale, so it is never less than 1 millimeter in the world along any axis.
        /// </summary>
        internal float ToleranceInAnyDirection =>
            MathF.Max(MathF.Max(ToleranceAlongAxes.X, ToleranceAlongAxes.Y), ToleranceAlongAxes.Z);

        /// <summary>Loads this region's configuration from the creation data's node.</summary>
        /// <remarks>
        ///     Every region samples through the creation data's random source and is placed by the Origin and Rotation
        ///     attributes where its element declares them, so the base takes the source and reads both before OnInit, keeping the
        ///     default for whichever is absent.
        /// </remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            RandomSource = creationData.RandomSource;

            SpecificationNode node = creationData.Node;

            Vector3 origin = Vector3.Zero;
            Rotation rotation = Rotation.Identity;

            if (node.HasAttribute(OriginAttribute.Name) && !node.ReadVector3(OriginAttribute.Name, out origin))
            {
                return false;
            }

            if (node.HasAttribute(RotationAttribute.Name) && !node.ReadRotation(RotationAttribute.Name, out rotation))
            {
                return false;
            }

            _transform = new Transform(origin, rotation, _transform.Scale);
            return OnInit(creationData);
        }

        /// <summary>Loads the derived region's own configuration.</summary>
        /// <remarks>
        ///     The factory has already refused whatever the description does not name, and the default returns true, so a
        ///     region with nothing of its own to load leaves this alone.
        /// </remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        protected virtual bool OnInit(AiCreationData creationData)
        {
            return true;
        }

        /// <summary>Loads the node's OriginTarget child, if it has one, for the shapes whose element takes one.</summary>
        /// <remarks>
        ///     The factory has already refused any other child and a second OriginTarget against the shape's description, so
        ///     every child met here is the one OriginTarget.
        /// </remarks>
        /// <param name="creationData">The creation data whose node carries the OriginTarget child, and whose factories build it.</param>
        /// <param name="originTarget">The loaded target, or null when the node has no OriginTarget child.</param>
        /// <returns>True when the children loaded. False after reporting what went wrong.</returns>
        protected static bool LoadOriginTarget(AiCreationData creationData, out TargetBase? originTarget)
        {
            SpecificationNode node = creationData.Node;

            originTarget = null;

            foreach (SpecificationNode child in node.Children)
            {
                if (!creationData.Factories.Targets.TryCreate(creationData.ForChild(child), out originTarget))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Tests whether a position is inside the region.</summary>
        public bool IsInRegion(Vector3 position)
        {
            return TryGetWorldTransform(out Transform world) && IsInShape(world.ToLocal(position));
        }

        /// <summary>Picks a random position inside the region.</summary>
        /// <returns>True if a position was found. Some shapes can fail, and then the position is unspecified.</returns>
        public bool TryGetRandomPosition(out Vector3 position)
        {
            if (!TryGetWorldTransform(out Transform world) || !TryGetRandomLocalPosition(out Vector3 local))
            {
                position = Vector3.Zero;

                return false;
            }

            position = world.ToWorld(local);

            return true;
        }

        /// <summary>Tests whether a point in the shape's own coordinates is inside the shape.</summary>
        /// <remarks>
        ///     The point has been brought into the shape's own coordinates through the transform that places it, so the shape
        ///     is tested as though it stood unturned at the world's origin. That step rounds, so every shape here counts a point
        ///     no more than 1 millimeter outside its boundary in the world as inside, at any Scale.
        /// </remarks>
        protected abstract bool IsInShape(Vector3 local);

        /// <summary>Picks a random point inside the shape, in its own coordinates.</summary>
        /// <returns>True if a point was found. Some shapes can fail, and then the point is unspecified.</returns>
        protected abstract bool TryGetRandomLocalPosition(out Vector3 local);

        /// <summary>
        ///     Guesses points inside a box in the shape's own coordinates until IsInShape accepts one, or the attempts run
        ///     out.
        /// </summary>
        /// <param name="min">The lowest corner of the box to guess inside.</param>
        /// <param name="max">The highest corner of the box to guess inside.</param>
        /// <param name="local">A point inside the shape, if one was found.</param>
        /// <returns>True if a guess landed inside the shape.</returns>
        protected bool TryRejectionSample(Vector3 min, Vector3 max, out Vector3 local)
        {
            for (int attempt = 0; attempt < MathHelpers.MaxSampleAttempts; attempt++)
            {
                local = MathHelpers.RandomInBox(RandomSource, min, max);

                if (IsInShape(local))
                {
                    return true;
                }
            }

            local = Vector3.Zero;

            return false;
        }

        /// <summary>
        ///     Finds the transform that places the shape at the time of the call, the stored one, or the stored one moved to
        ///     OriginTarget's position while a target is set.
        /// </summary>
        /// <remarks>
        ///     It fails when the target cannot answer, and when a component of the scale is zero, before ToLocal divides by
        ///     that zero. Either leaves the shape containing nothing and its sampling failing.
        /// </remarks>
        private bool TryGetWorldTransform(out Transform world)
        {
            if (OriginTarget == null)
            {
                world = _transform;
            }
            else
            {
                if (!OriginTarget.TryGetPosition(out Vector3 targetPosition))
                {
                    world = default;

                    return false;
                }

                world = _transform.WithOrigin(targetPosition);
            }

            if (world.Scale.X == 0f || world.Scale.Y == 0f || world.Scale.Z == 0f)
            {
                world = default;

                return false;
            }

            return true;
        }

        /// <summary>Divides <see cref="BoundaryTolerance" /> by the size of one component of Scale.</summary>
        /// <remarks>
        ///     A zero component leaves the shape containing nothing, since IsInRegion and TryGetRandomPosition refuse it
        ///     before asking the shape, so the shapes here never compare with the tolerance along it, and it is 0 rather than a
        ///     division by zero.
        /// </remarks>
        private static float ToleranceAlong(float scale)
        {
            return scale == 0f ? 0f : BoundaryTolerance / MathF.Abs(scale);
        }
    }
}
