using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Targets;

namespace Gaia.Core.Regions
{
    /// <summary>A region that is a flat shape, placed and turned by a <see cref="RegionFrame" />.</summary>
    /// <remarks>
    ///     <para>
    ///         The shape is described in the plane of its frame, so a rectangle is two <see cref="Vector2" /> corners and a
    ///         footprint is a ring of them. A position is brought into the plane before the shape tests it, and a random
    ///         position is picked in the plane and then carried back out into the world, so each shape's arithmetic is the
    ///         flat kind whichever way it is turned. Height, the distance from the plane, is ignored by the shapes that are
    ///         only a footprint and bounded by the ones that are extruded.
    ///     </para>
    ///     <para>
    ///         Origin and Rotation are read from attributes of the same names, and both are optional, so a shape that writes
    ///         neither lies on the ground at the world's origin, with its Y along the world's Z.
    ///     </para>
    /// </remarks>
    public abstract class PlanarRegionBase : RegionBase
    {
        /// <summary>Describes the optional Origin attribute <see cref="LoadFrame" /> reads by.</summary>
        private static readonly AttributeDescription OriginAttribute = AttributeDescription.Optional("Origin", "Origin",
            AttributeKind.Vector3, "(0, 0, 0)", "Where the shape's (0, 0) sits in the world.");

        /// <summary>Describes the optional Rotation attribute <see cref="LoadFrame" /> reads by.</summary>
        private static readonly AttributeDescription RotationAttribute = AttributeDescription.Optional("Rotation",
            "Rotation", AttributeKind.Rotation, "(0, 0, 0)",
            "How far the shape's plane is turned from the ground, as pitch, yaw and roll in degrees.");

        private RegionFrame _frame = RegionFrame.Identity;

        /// <summary>The world position of the shape's (0, 0).</summary>
        public Vector3 Origin
        {
            get => _frame.Origin;
            set => _frame = _frame.WithOrigin(value);
        }

        /// <summary>How the shape's plane is turned from the ground, which is not at all until it is set.</summary>
        public Rotation Rotation
        {
            get => _frame.Rotation;
            set => _frame = new RegionFrame(_frame.Origin, value);
        }

        /// <summary>The frame the shape is placed by.</summary>
        protected RegionFrame Frame => _frame;

        /// <summary>Reads the Origin and Rotation attributes, keeping the identity for whichever is absent.</summary>
        /// <returns>True when the frame loaded. False after reporting malformed text.</returns>
        protected bool LoadFrame(AiCreationData creationData)
        {
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

            _frame = new RegionFrame(origin, rotation);
            return true;
        }

        /// <summary>
        ///     Starts the description of a flat shape, carrying the Origin and Rotation attributes <see cref="LoadFrame" />
        ///     reads by, so a shape adds what it reads of its own and nothing else.
        /// </summary>
        /// <param name="summary">One line saying what the shape is, in a designer's words.</param>
        protected static DescriptionBuilder DescribeFlatShape(string summary)
        {
            return new DescriptionBuilder(ConceptualAbstraction.Regions, summary).Add(OriginAttribute)
                .Add(RotationAttribute);
        }

        /// <summary>
        ///     Finds the frame at the time of the call, moved to the target's position when a target is given. Fails only
        ///     when the target cannot answer, which leaves the region containing nothing.
        /// </summary>
        protected bool TryGetFrame(TargetBase? originTarget, out RegionFrame frame)
        {
            if (originTarget == null)
            {
                frame = _frame;

                return true;
            }

            if (!originTarget.TryGetPosition(out Vector3 origin))
            {
                frame = default;

                return false;
            }

            frame = _frame.WithOrigin(origin);

            return true;
        }
    }
}
