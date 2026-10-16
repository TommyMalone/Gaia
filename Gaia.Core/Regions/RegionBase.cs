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
    ///         would be, and Origin, Rotation and Scale place it in the world, or inside its target's transform while it
    ///         follows OriginTarget.
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
        ///         size of its scale in the world on that axis, Scale multiplied by its target's while it stretches with one. A
        ///         comparison that mixes axes, a distance from a middle, an axis, a segment, a plane or an edge, divides by the
        ///         smallest size of that scale among the axes it mixes, so the tolerance is never less than 1 millimeter in the
        ///         world along any of them, and is more along an axis scaled further.
        ///     </para>
        ///     <para>
        ///         Unreal's math header defines THRESH_POINT_ON_PLANE, 0.10 centimeters, as a plane's thickness, the same
        ///         millimeter, and neither Unreal's nor Unity's box test documents a tolerance. A shape a game writes picks its
        ///         own.
        ///     </para>
        /// </remarks>
        internal const float BoundaryTolerance = 0.001f;

        /// <summary>Describes the optional Origin attribute <see cref="Init" /> reads by.</summary>
        private static readonly AttributeDescription OriginAttribute = AttributeDescription.Optional("Origin", "Origin",
            AttributeKind.Vector3, "(0, 0, 0)",
            "Where the shape's (0, 0, 0) is, in its parent transform's coordinates while it follows an OriginTarget, or else in the world.");

        /// <summary>Describes the optional Rotation attribute <see cref="Init" /> reads by.</summary>
        private static readonly AttributeDescription RotationAttribute = AttributeDescription.Optional("Rotation",
            "Rotation", AttributeKind.Rotation, "(0, 0, 0)",
            "How the shape is turned, as pitch, yaw and roll in degrees, pitch positive toward the ground, yaw positive to the right and roll positive raising its right side, relative to its parent transform while it follows an OriginTarget without AbsoluteRotation, or else in the world.");

        /// <summary>Describes the optional Scale attribute <see cref="Init" /> reads by.</summary>
        private static readonly AttributeDescription ScaleAttribute = AttributeDescription.Optional("Scale", "Scale",
            AttributeKind.Vector3, "(1, 1, 1)",
            "How the shape is stretched along its own right, up and forward, multiplied by its parent transform's scale while it follows an OriginTarget without AbsoluteScale. An uneven Scale stretches a round shape into an ellipse or an ellipsoid, and a negative component mirrors the shape.");

        /// <summary>Describes the optional AbsoluteRotation attribute <see cref="Init" /> reads by.</summary>
        private static readonly AttributeDescription AbsoluteRotationAttribute = AttributeDescription.Optional(
            "AbsoluteRotation", "AbsoluteRotation", AttributeKind.Bool, "false",
            "Whether the shape keeps its Rotation relative to the world while it follows an OriginTarget, rather than turning with its parent transform.");

        /// <summary>Describes the optional AbsoluteScale attribute <see cref="Init" /> reads by.</summary>
        private static readonly AttributeDescription AbsoluteScaleAttribute = AttributeDescription.Optional(
            "AbsoluteScale", "AbsoluteScale", AttributeKind.Bool, "false",
            "Whether the shape keeps its Scale as its whole scale while it follows an OriginTarget, rather than stretching with its parent transform.");

        /// <summary>Describes the optional OriginTarget child <see cref="Init" /> loads, which DescribeShape gives every shape.</summary>
        /// <remarks>
        ///     Init reads only the child of this name, so a hull's Plane children and a footprint's Corner children are read
        ///     beside it as what they are.
        /// </remarks>
        private static readonly SlotDescription OriginTargetSlot = new("OriginTarget", "OriginTarget",
            ConceptualAbstraction.Targets, 0, 1);

        private Vector3 _ownTolerance = new(BoundaryTolerance, BoundaryTolerance, BoundaryTolerance);

        private Transform _transform = Transform.Identity;

        /// <summary>
        ///     Where the shape's (0, 0, 0) is, in its target's coordinates while it follows OriginTarget, or else in the
        ///     world. The world's origin until it is set.
        /// </summary>
        public Vector3 Origin
        {
            get => _transform.Origin;
            set => _transform = _transform.WithOrigin(value);
        }

        /// <summary>
        ///     How the shape is turned, relative to the world, or to its target while it follows OriginTarget without
        ///     AbsoluteRotation. No turn until it is set.
        /// </summary>
        public Rotation Rotation
        {
            get => _transform.Rotation;
            set => _transform = new Transform(_transform.Origin, value, _transform.Scale);
        }

        /// <summary>
        ///     How far one of the shape's own units stretches along its right, up and forward, multiplied by its target's
        ///     scale while it follows OriginTarget without AbsoluteScale. (1, 1, 1) until it is set.
        /// </summary>
        /// <remarks>
        ///     A zero component of Scale, or of the target's scale while the shape follows it without AbsoluteScale, leaves
        ///     the shape containing nothing and its sampling failing. An uneven Scale stretches a round shape, a sphere into an
        ///     ellipsoid and a circle or a cylinder into an ellipse across, where Unity's and Unreal's round colliders stay round.
        ///     A negative component mirrors the shape along that axis, as both engines mirror a placed object.
        /// </remarks>
        public Vector3 Scale
        {
            get => _transform.Scale;
            set
            {
                _transform = new Transform(_transform.Origin, _transform.Rotation, value);
                _ownTolerance = TolerancesFor(value);
                ToleranceAlongAxes = _ownTolerance;
            }
        }

        /// <summary>
        ///     Whether the shape keeps Rotation relative to the world while it follows OriginTarget, rather than turning with
        ///     the target. False until it is set.
        /// </summary>
        /// <remarks>
        ///     The target's turn still carries Origin around the target, so only the shape's own orientation is kept. Without
        ///     a target it changes nothing. A cylinder or a capsule around a character sets it to stay upright while the character
        ///     leans. A round shape loses nothing by it, since a turn about its own up leaves it as it was.
        /// </remarks>
        public bool AbsoluteRotation { get; set; }

        /// <summary>
        ///     Whether the shape keeps Scale as its whole scale while it follows OriginTarget, rather than stretching with
        ///     the target. False until it is set.
        /// </summary>
        /// <remarks>
        ///     The target's scale still stretches Origin's offset from the target, so only the shape's own size is kept.
        ///     Without a target it changes nothing.
        /// </remarks>
        public bool AbsoluteScale { get; set; }

        /// <summary>The target the shape follows, or null to place the shape in the world.</summary>
        /// <remarks>
        ///     While it is set, the target's transform is read at every query and the shape's own is placed inside it, so the
        ///     shape turns by the target's rotation and then by its own about the target's axes, and the two scales multiply axis
        ///     by axis. A target mirrored by a negative scale mirrors the shape's offset, and its turn as well unless
        ///     AbsoluteRotation keeps it. A target that cannot answer leaves the shape containing nothing and its sampling
        ///     failing.
        /// </remarks>
        public TargetBase? OriginTarget { get; set; }

        /// <summary>Where the region draws the numbers that pick a random position.</summary>
        public IRandomSource RandomSource { get; set; } = new Xoshiro128RandomSource();

        /// <summary>
        ///     <see cref="BoundaryTolerance" /> in the shape's own units along each of its own axes, 1 millimeter in the
        ///     world divided by the size of the shape's scale in the world on that axis, for a comparison along one axis.
        /// </summary>
        /// <remarks>
        ///     Divided when Scale is set, and again at each query while the shape stretches with a target, whose scale can
        ///     change between queries, so a shape reads it inside IsInShape or TryGetRandomLocalPosition and keeps nothing worked
        ///     out from it.
        /// </remarks>
        internal Vector3 ToleranceAlongAxes { get; private set; } =
            new(BoundaryTolerance, BoundaryTolerance, BoundaryTolerance);

        /// <summary>
        ///     <see cref="BoundaryTolerance" /> in the shape's own units for a comparison that mixes all three of its axes, a
        ///     distance from its middle, its segment or a plane. It is the largest of ToleranceAlongAxes, 1 millimeter in the
        ///     world divided by the smallest size of the shape's scale in the world, so it is never less than 1 millimeter in the
        ///     world along any axis.
        /// </summary>
        internal float ToleranceInAnyDirection =>
            MathF.Max(MathF.Max(ToleranceAlongAxes.X, ToleranceAlongAxes.Y), ToleranceAlongAxes.Z);

        /// <summary>Loads this region's configuration from the creation data's node.</summary>
        /// <remarks>
        ///     Every region samples through the creation data's random source, is placed by its Origin, Rotation and Scale
        ///     attributes and its two absolute flags, and may follow an OriginTarget child, so the base takes the source, reads
        ///     the five attributes and loads the child before OnInit, keeping the default for whichever is absent. A Scale with a
        ///     zero component is refused with its line.
        /// </remarks>
        /// <returns>True when the configuration loaded. False after reporting what went wrong.</returns>
        public bool Init(AiCreationData creationData)
        {
            RandomSource = creationData.RandomSource;

            SpecificationNode node = creationData.Node;

            Vector3 origin = Vector3.Zero;
            Rotation rotation = Rotation.Identity;
            Vector3 scale = new(1f, 1f, 1f);
            bool absoluteRotation = false;
            bool absoluteScale = false;

            if (node.HasAttribute(OriginAttribute.Name) && !node.ReadVector3(OriginAttribute.Name, out origin))
            {
                return false;
            }

            if (node.HasAttribute(RotationAttribute.Name) && !node.ReadRotation(RotationAttribute.Name, out rotation))
            {
                return false;
            }

            if (node.HasAttribute(ScaleAttribute.Name))
            {
                if (!node.ReadVector3(ScaleAttribute.Name, out scale))
                {
                    return false;
                }

                if (scale.X == 0f || scale.Y == 0f || scale.Z == 0f)
                {
                    creationData.ReportError(
                        $"Line {node.LineNumber}, the {node.NameSpelling} element's Scale attribute has a zero component, which leaves the shape containing nothing");
                    return false;
                }
            }

            if (node.HasAttribute(AbsoluteRotationAttribute.Name) &&
                !node.ReadBool(AbsoluteRotationAttribute.Name, out absoluteRotation))
            {
                return false;
            }

            if (node.HasAttribute(AbsoluteScaleAttribute.Name) &&
                !node.ReadBool(AbsoluteScaleAttribute.Name, out absoluteScale))
            {
                return false;
            }

            if (!LoadOriginTarget(creationData, out TargetBase? originTarget))
            {
                return false;
            }

            Origin = origin;
            Rotation = rotation;
            Scale = scale;
            AbsoluteRotation = absoluteRotation;
            AbsoluteScale = absoluteScale;
            OriginTarget = originTarget;

            return OnInit(creationData);
        }

        /// <summary>
        ///     Starts the description of a shape, carrying the Origin, Rotation, Scale, AbsoluteRotation and AbsoluteScale
        ///     attributes and the OriginTarget child that <see cref="Init" /> reads, so a shape adds what it reads of its own and
        ///     nothing else.
        /// </summary>
        /// <remarks>They are recorded first, so they come before the shape's own attributes and children in its description.</remarks>
        /// <param name="summary">One line saying what the shape is, in a designer's words.</param>
        protected static DescriptionBuilder DescribeShape(string summary)
        {
            return new DescriptionBuilder(ConceptualAbstraction.Regions, summary).Add(OriginAttribute)
                .Add(RotationAttribute).Add(ScaleAttribute).Add(AbsoluteRotationAttribute).Add(AbsoluteScaleAttribute)
                .Add(OriginTargetSlot);
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

        /// <summary>Loads the node's OriginTarget child, if it has one.</summary>
        /// <remarks>
        ///     The factory has already refused a second OriginTarget against the shape's description. Every other child is
        ///     the shape's own and is left to it.
        /// </remarks>
        /// <param name="creationData">The creation data whose node carries the OriginTarget child, and whose factories build it.</param>
        /// <param name="originTarget">The loaded target, or null when the node has no OriginTarget child.</param>
        /// <returns>True when the OriginTarget child loaded, or there is none. False after reporting what went wrong.</returns>
        private static bool LoadOriginTarget(AiCreationData creationData, out TargetBase? originTarget)
        {
            SpecificationNode node = creationData.Node;

            originTarget = null;

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name != OriginTargetSlot.Name)
                {
                    continue;
                }

                if (!creationData.Factories.Targets.TryCreate(creationData.ForChild(child), out originTarget))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Tests whether a position is inside the region.</summary>
        /// <remarks>A position on the shape's boundary counts as inside.</remarks>
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
        ///     Finds the transform that places the shape at the time of the call, the stored one, or the stored one placed
        ///     inside OriginTarget's transform while a target is set, keeping the shape's own rotation under AbsoluteRotation and
        ///     its own scale under AbsoluteScale.
        /// </summary>
        /// <remarks>
        ///     It fails when the target cannot answer, and when a component of the world scale is zero, before ToLocal
        ///     divides by that zero. A zero there comes from the shape's own Scale, or from the target's scale while the shape
        ///     stretches with it. Either failure leaves the shape containing nothing and its sampling failing. On success it sets
        ///     ToleranceAlongAxes from the world scale, so a shape that stretches with its target holds 1 millimeter in the world
        ///     past its boundary and not 1 millimeter multiplied by the target's scale.
        /// </remarks>
        private bool TryGetWorldTransform(out Transform world)
        {
            if (OriginTarget == null)
            {
                world = _transform;
            }
            else
            {
                if (!OriginTarget.TryGetTransform(out Transform target))
                {
                    world = default;

                    return false;
                }

                world = target.ToWorld(_transform);

                if (AbsoluteRotation || AbsoluteScale)
                {
                    world = new Transform(world.Origin, AbsoluteRotation ? _transform.Rotation : world.Rotation,
                        AbsoluteScale ? _transform.Scale : world.Scale);
                }
            }

            if (world.Scale.X == 0f || world.Scale.Y == 0f || world.Scale.Z == 0f)
            {
                world = default;

                return false;
            }

            ToleranceAlongAxes = OriginTarget != null && !AbsoluteScale ? TolerancesFor(world.Scale) : _ownTolerance;

            return true;
        }

        /// <summary>Divides <see cref="BoundaryTolerance" /> by the size of each component of a scale.</summary>
        private static Vector3 TolerancesFor(Vector3 scale)
        {
            return new Vector3(ToleranceAlong(scale.X), ToleranceAlong(scale.Y), ToleranceAlong(scale.Z));
        }

        /// <summary>Divides <see cref="BoundaryTolerance" /> by the size of one component of a scale.</summary>
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
