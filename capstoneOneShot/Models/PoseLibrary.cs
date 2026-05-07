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

        /// <summary>
        /// Returns only poses the user can physically perform
        /// based on their ROM profile.
        /// </summary>
        public static List<PoseDefinition> GetPosesForUser(UserROMProfile profile)
        {
            var all = GetAllPoses();

            // If no ROM profile yet, return all poses
            if (!profile.IsComplete) return all;

            var capable = new List<PoseDefinition>();
            foreach (var pose in all)
            {
                if (profile.CanPerformPose(pose))
                    capable.Add(pose);
            }

            // Always return at least beginner poses as fallback
            if (capable.Count == 0)
                return GetBeginnerPoses();

            return capable;
        }

        // ---------------------------------------------------------------
        // BEGINNER POSES
        // ---------------------------------------------------------------
        private static List<PoseDefinition> GetBeginnerPoses()
        {
            return new List<PoseDefinition>
            {
                new PoseDefinition
                {
                    Name          = "Mountain Pose",
                    Description   = "Stand tall with feet together, arms at your sides, and spine straight.",
                    BriefInfo     = "A foundational standing pose that builds awareness, balance, and upright posture.",
                    Difficulty    = DifficultyLevel.Beginner,
                    HoldSeconds   = 120,
                    ImageFileName = "mountain-pose.png",

                    // ── Phase 1: spoken step-by-step instructions ──────────────
                    Instructions = new List<string>
                    {
                        "Stand upright with feet together or hip-width apart, weight evenly distributed.",
                        "Engage your thighs and lift your kneecaps slightly, keeping knees soft.",
                        "Keep your pelvis neutral and gently engage your core.",
                        "Lengthen your spine, lifting through the crown of your head with chin level.",
                        "Roll shoulders back and down; arms relaxed at sides, palms facing forward.",
                        "Maintain balance and breathe slowly, holding the position."
                    },

                    // ── Phase 2: joint rules — failures become corrections ──────
                    Rules = new List<JointAngleRule>
                    {
                        new JointAngleRule
                        {
                            JointName = "LeftElbow",
                            MinAngle  = 165.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Straighten your left arm fully at your side."
                        },
                        new JointAngleRule
                        {
                            JointName = "RightElbow",
                            MinAngle  = 165.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Straighten your right arm fully at your side."
                        },
                        new JointAngleRule
                        {
                            JointName = "LeftShoulder",
                            MinAngle  = 155.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Relax your left shoulder and keep your arm close to your side."
                        },
                        new JointAngleRule
                        {
                            JointName = "RightShoulder",
                            MinAngle  = 155.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Relax your right shoulder and keep your arm close to your side."
                        },
                        new JointAngleRule
                        {
                            JointName = "LeftKnee",
                            MinAngle  = 170.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Straighten your left knee without locking it."
                        },
                        new JointAngleRule
                        {
                            JointName = "RightKnee",
                            MinAngle  = 170.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Straighten your right knee without locking it."
                        },
                        new JointAngleRule
                        {
                            JointName = "LeftHip",
                            MinAngle  = 168.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Keep your hips neutral — do not tilt forward or back."
                        },
                        new JointAngleRule
                        {
                            JointName = "RightHip",
                            MinAngle  = 168.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Keep your hips neutral — do not tilt forward or back."
                        },
                        new JointAngleRule
                        {
                            JointName = "LeftWrist",
                            MinAngle  = 165.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Keep your left wrist straight and relaxed."
                        },
                        new JointAngleRule
                        {
                            JointName = "RightWrist",
                            MinAngle  = 165.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Keep your right wrist straight and relaxed."
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
