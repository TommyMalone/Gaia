using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;

namespace Gaia.Core.Regions
{
    /// <summary>
    ///     A slice of a disc, opening an angle either side of its forward, extruded a half-height either side of its
    ///     plane.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Origin is where the slice's two straight edges meet, halfway up its height. The slice opens along the shape's
    ///         forward by half of Angle to either side, so unturned it stands upright and opens along the world's Z, and a
    ///         sector that follows OriginTarget opens the way the target faces, turned further by its own Rotation, or by its
    ///         own Rotation alone while AbsoluteRotation is true. Range is measured from Origin in the plane, so the far edge
    ///         is an arc.
    ///     </para>
    ///     <para>
    ///         A cone, such as the cone Unreal's sight sense describes, bounds a point above or below its axis by its angle,
    ///         as it bounds one to either side. The sector bounds its height by HalfHeight alone, so a port of such a cone
    ///         sets HalfHeight from the heights the sector must cover above and below Origin.
    ///     </para>
    ///     <para>
    ///         Angle and Range are in the sector's own coordinates, so a Scale that differs between its right and its
    ///         forward widens or narrows the opening in the world, as such a Scale stretches a sphere into an ellipsoid.
    ///     </para>
    ///     <para>
    ///         A random point is picked directly, so the sector's own pick never fails. Its distance from Origin is Range
    ///         times the square root of a roll, which spreads the points evenly over the slice, and its turn from forward and
    ///         its height are each rolled evenly between their bounds.
    ///     </para>
    /// </remarks>
    public sealed class SectorRegion : PlanarRegionBase
    {
        private static readonly DescriptionBuilder Builder =
            DescribeShape("An upright slice of a cylinder around its Origin, opening ahead, such as a field of view.");

        private static readonly AttributeDescription AngleAttribute = Builder.Required("Angle", AttributeKind.Float,
            "How wide the sector opens, in degrees, half to either side of its forward. Above 0 and at most 360, where 360 is the whole disc. A half-angle from forward to one edge, such as Unreal's Peripheral Vision Half Angle, is doubled to give Angle.");

        private static readonly AttributeDescription RangeAttribute = Builder.Required("Range", AttributeKind.Float,
            "How far the sector reaches from its Origin, measured in its plane. Above 0. Scale multiplies it, and so does the scale of an OriginTarget the sector follows unless AbsoluteScale is true.");

        private static readonly AttributeDescription HalfHeightAttribute = Builder.Required("HalfHeight",
            AttributeKind.Float,
            "How far the sector reaches either side of its plane, which runs through Origin at its middle. A negative value is read as its size.");

        internal static readonly ComponentDescription Description = Builder.Build();

        private float _angle;
        private float _cosineOfHalfAngle = 1f;
        private float _halfAngleInRadians;
        private float _halfHeight;
        private float _sineOfHalfAngle;

        /// <summary>
        ///     How wide the sector opens, in degrees, half to either side of its forward. Above 0 and at most 360, where 360
        ///     is the whole disc.
        /// </summary>
        /// <remarks>
        ///     The angle from forward to either edge is half of Angle, which is the angle Unreal's sight sense configures as
        ///     its peripheral vision, so a value taken from it is doubled. Unreal's camera FieldOfView is horizontal and copies
        ///     across. Unity's Camera.fieldOfView is vertical, so it is converted with Camera.VerticalToHorizontalFieldOfView
        ///     first.
        /// </remarks>
        public float Angle
        {
            get => _angle;
            set
            {
                _angle = value;

                // Halved, converted to radians and its cosine and sine taken once here rather than on every containment
                // test and pick.
                _halfAngleInRadians = value * 0.5f * MathHelpers.DegreesToRadians;
                _cosineOfHalfAngle = MathF.Cos(_halfAngleInRadians);
                _sineOfHalfAngle = MathF.Sin(_halfAngleInRadians);
            }
        }

        /// <summary>
        ///     How far the sector reaches from Origin, measured in its plane. Above 0. Scale multiplies it in the world, and
        ///     so does the scale of an OriginTarget the sector follows unless AbsoluteScale is true.
        /// </summary>
        public float Range { get; set; }

        /// <summary>How far the sector reaches either side of its plane.</summary>
        /// <remarks>A negative value is stored as its size, so -1.5 reads back as 1.5.</remarks>
        public float HalfHeight
        {
            get => _halfHeight;
            set => _halfHeight = MathF.Abs(value);
        }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            float angle;
            float range;
            float halfHeight;

            if (!node.ReadFloat(AngleAttribute.Name, out angle))
            {
                return false;
            }

            // A width at or below 0 or past 360 could mean the rest of the disc or more than a whole turn, so a file
            // that writes one is refused rather than read.
            if (angle <= 0f || angle > 360f)
            {
                node.TryGetString(AngleAttribute.Name, out string angleText);
                node.ReportMalformed(AngleAttribute.Name, angleText, "above 0 and at most 360");
                return false;
            }

            if (!node.ReadFloat(RangeAttribute.Name, out range))
            {
                return false;
            }

            // A range of 0 holds nothing, and a negative one could mean a slice opening behind Origin, so a file that
            // writes either is refused rather than read.
            if (range <= 0f)
            {
                node.TryGetString(RangeAttribute.Name, out string rangeText);
                node.ReportMalformed(RangeAttribute.Name, rangeText, "above 0");
                return false;
            }

            if (!node.ReadFloat(HalfHeightAttribute.Name, out halfHeight))
            {
                return false;
            }

            Angle = angle;
            Range = range;
            HalfHeight = halfHeight;
            return true;
        }

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            Vector2 point = InPlane(local, out float height);
            float squaredDistance = point.SquaredMagnitude;

            // Height is local Y, so the half height takes the tolerance along it.
            if (MathF.Abs(height) > _halfHeight + ToleranceAlongAxes.Y ||
                squaredDistance > MathHelpers.SquaredReach(Range, ToleranceInPlane))
            {
                return false;
            }

            // A point is within half of Angle of forward when its forward component is at least its distance times the
            // cosine of that half. Both sides are squared to leave out the root, which needs a second form once the
            // opening passes a half turn and the cosine is negative.
            float ahead = point.Y;
            float squaredBound = _cosineOfHalfAngle * _cosineOfHalfAngle * squaredDistance;

            bool isWithinTheOpening = _cosineOfHalfAngle >= 0f
                ? ahead >= 0f && ahead * ahead >= squaredBound
                : ahead >= 0f || ahead * ahead <= squaredBound;

            return isWithinTheOpening || IsNearAStraightSide(point);
        }

        /// <inheritdoc />
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            // The square root keeps the points even across the area, as in MathHelpers.RandomInDisc. The turn is
            // measured from forward toward the right, so its sine gives the plane's X and its cosine the plane's Y.
            float distance = Range * MathF.Sqrt(RandomSource.NextFloat());
            float turn = MathHelpers.RandomInRange(RandomSource, -_halfAngleInRadians, _halfAngleInRadians);
            float height = MathHelpers.RandomInRange(RandomSource, -_halfHeight, _halfHeight);

            local = OutOfPlane(new Vector2(distance * MathF.Sin(turn), distance * MathF.Cos(turn)), height);
            return true;
        }

        /// <summary>
        ///     Tests whether a point of the plane outside the opening lies within ToleranceInPlane of the nearer straight
        ///     side, the segment from Origin out to Range along one edge of the opening.
        /// </summary>
        private bool IsNearAStraightSide(Vector2 point)
        {
            // Folded across forward, the point lies nearer the right side, which runs from Origin along the
            // half-angle's sine and cosine. The side's nearest point to it is the point's projection onto the side,
            // held between Origin and Range, so a point past either end is measured from that end, as PolygonRegionBase
            // measures a footprint's edge.
            Vector2 folded = new(MathF.Abs(point.X), point.Y);
            Vector2 alongTheSide = new(_sineOfHalfAngle, _cosineOfHalfAngle);
            float distanceAlong = MathF.Max(MathF.Min(Vector2.Dot(folded, alongTheSide), Range), 0f);
            float tolerance = ToleranceInPlane;

            return Vector2.SquaredDistance(alongTheSide * distanceAlong, folded) <= tolerance * tolerance;
        }
    }
}
