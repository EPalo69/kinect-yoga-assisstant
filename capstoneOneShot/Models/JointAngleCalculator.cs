using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;

namespace capstoneOneShot.Models
{
    public static class JointAngleCalculator
    {
        // The 16 joints selected for yoga pose analysis
        public static readonly JointType[] AnalysisJoints = new[]
        {
        JointType.Head,
        JointType.ShoulderCenter,
        JointType.ShoulderLeft,  JointType.ShoulderRight,
        JointType.ElbowLeft,     JointType.ElbowRight,
        JointType.WristLeft,     JointType.WristRight,
        JointType.HipCenter,
        JointType.HipLeft,       JointType.HipRight,
        JointType.KneeLeft,      JointType.KneeRight,
        JointType.AnkleLeft,     JointType.AnkleRight
    };

        /// <summary>
        /// Calculate angle at joint B, formed by points A-B-C (in degrees)
        /// </summary>
        public static double CalculateAngle(SkeletonPoint a, SkeletonPoint b, SkeletonPoint c)
        {
            // Vectors from B to A and B to C
            double bax = a.X - b.X, bay = a.Y - b.Y, baz = a.Z - b.Z;
            double bcx = c.X - b.X, bcy = c.Y - b.Y, bcz = c.Z - b.Z;

            double dot = bax * bcx + bay * bcy + baz * bcz;
            double magBA = Math.Sqrt(bax * bax + bay * bay + baz * baz);
            double magBC = Math.Sqrt(bcx * bcx + bcy * bcy + bcz * bcz);

            if (magBA == 0 || magBC == 0) return 0;

            double cosAngle = dot / (magBA * magBC);
            cosAngle = Math.Max(-1, Math.Min(1, cosAngle)); // clamp
            return Math.Acos(cosAngle) * (180.0 / Math.PI);
        }
    }
}
