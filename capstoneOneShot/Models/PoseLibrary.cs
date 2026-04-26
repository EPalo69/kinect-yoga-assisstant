using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace capstoneOneShot.Models
{
    /// <summary>
    /// Central library of all yoga pose definitions organized by difficulty.
    /// Add new poses here as the system grows.
    /// </summary>
    public static class PoseLibrary
    {
        public static List<PoseDefinition> GetAllPoses()
        {
            var poses = new List<PoseDefinition>();
            poses.AddRange(GetBeginnerPoses());
            poses.AddRange(GetIntermediatePoses());
            poses.AddRange(GetAdvancedPoses());
            return poses;
        }

        public static List<PoseDefinition> GetPosesByDifficulty(DifficultyLevel difficulty)
        {
            switch (difficulty)
            {
                case DifficultyLevel.Beginner: return GetBeginnerPoses();
                case DifficultyLevel.Intermediate: return GetIntermediatePoses();
                case DifficultyLevel.Advanced: return GetAdvancedPoses();
                default: return GetBeginnerPoses();
            }
        }

        // ---------------------------------------------------------------
        // BEGINNER POSES
        // ---------------------------------------------------------------
        private static List<PoseDefinition> GetBeginnerPoses()
        {
            return new List<PoseDefinition>
            {
                // --- Mountain Pose (Tadasana) ---
                // Standing straight, arms at sides, feet together
                new PoseDefinition
                {
                    Name        = "Mountain Pose",
                    Description = "Stand tall with feet together, arms at your sides, and spine straight.",
                    Difficulty  = DifficultyLevel.Beginner,
                    HoldSeconds = 20,
                    ImageFileName = "mountain-pose.png",
                    Rules = new List<JointAngleRule>
                    {
                        new JointAngleRule
                        {
                            JointName  = "LeftKnee",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Straighten your left knee."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightKnee",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Straighten your right knee."
                        },
                        new JointAngleRule
                        {
                            JointName  = "LeftElbow",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Relax and straighten your left arm."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightElbow",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Relax and straighten your right arm."
                        }
                    }
                }
            };
        }

        // ---------------------------------------------------------------
        // INTERMEDIATE POSES
        // ---------------------------------------------------------------
        private static List<PoseDefinition> GetIntermediatePoses()
        {
            // Temporarily empty — focusing on Mountain Pose calibration
            return new List<PoseDefinition>();
        }

        // ---------------------------------------------------------------
        // ADVANCED POSES
        // ---------------------------------------------------------------
        private static List<PoseDefinition> GetAdvancedPoses()
        {
            // Temporarily empty — focusing on Mountain Pose calibration
            return new List<PoseDefinition>();
        }
    }
}
