using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace capstoneOneShot.Models
{
    /// <summary>
    /// Stores the user's measured range of motion from the ROM test.
    /// Used to filter poses they are physically capable of performing.
    /// </summary>
    public class UserROMProfile
    {
        // Measured max/min angles from ROM test
        public double ShoulderFlexion { get; set; }  // how high they can raise arms
        public double KneeFlexion { get; set; }  // how deep they can bend knees
        public double HipFlexion { get; set; }  // how far they can hinge at hip
        public double LateralShoulder { get; set; }  // lateral arm raise range

        // Assigned difficulty
        public DifficultyLevel AssignedDifficulty { get; set; }

        // Whether profile has been completed
        public bool IsComplete { get; set; } = false;

        /// <summary>
        /// Returns true if the user's ROM can physically meet
        /// the minimum requirements of a given pose rule.
        /// </summary>
        public bool CanPerformRule(JointAngleRule rule)
        {
            switch (rule.JointName)
            {
                case "LeftShoulder":
                case "RightShoulder":
                    // Rule requires a min angle — check if user can reach it
                    return ShoulderFlexion >= rule.MinAngle - 10;

                case "LeftKnee":
                case "RightKnee":
                    // For knee bends, lower angle = deeper bend
                    // If rule requires a low angle, check user can bend that deep
                    if (rule.MaxAngle < 120)
                        return KneeFlexion <= rule.MaxAngle + 10;
                    return true; // straightening is always possible

                case "LeftHip":
                case "RightHip":
                    return HipFlexion <= rule.MinAngle + 10;

                default:
                    return true;
            }
        }

        /// <summary>
        /// Returns true if the user can physically perform all
        /// required rules of a pose based on their ROM.
        /// </summary>
        public bool CanPerformPose(PoseDefinition pose)
        {
            foreach (var rule in pose.Rules)
            {
                if (!CanPerformRule(rule))
                    return false;
            }
            return true;
        }
    }
}
