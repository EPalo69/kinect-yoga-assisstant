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
                    InstructionAudioFolder = "mountain-pose",
                    InstructionAudioPrefix = "mountain_",

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
                            Feedback  = "Straighten your left arm.",
                            AudioFile = "corrective-feedback/arms-shoulders/straighten_left_arm.wav"
                        },
                        new JointAngleRule
                        {
                            JointName = "RightElbow",
                            MinAngle  = 165.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Straighten your right arm.",
                            AudioFile = "corrective-feedback/arms-shoulders/straighten_right_arm.wav"
                        },
                        new JointAngleRule
                        {
                            JointName = "LeftShoulder",
                            MinAngle  = 155.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Relax your left shoulder.",
                            AudioFile = "corrective-feedback/arms-shoulders/relax_left_shoulder.wav"
                        },
                        new JointAngleRule
                        {
                            JointName = "RightShoulder",
                            MinAngle  = 155.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Relax your right shoulder.",
                            AudioFile = "corrective-feedback/arms-shoulders/relax_right_shoulder.wav"
                        },
                        new JointAngleRule
                        {
                            JointName = "LeftKnee",
                            MinAngle  = 170.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Straighten your left leg.",
                            AudioFile = "corrective-feedback/legs-knees/straighten_left_leg.wav"
                        },
                        new JointAngleRule
                        {
                            JointName = "RightKnee",
                            MinAngle  = 170.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Straighten your right leg.",
                            AudioFile = "corrective-feedback/legs-knees/straighten_right_leg.wav"
                        },
                        new JointAngleRule
                        {
                            JointName = "LeftHip",
                            MinAngle  = 168.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Keep your hips neutral.",
                            AudioFile = "corrective-feedback/core-hips/keep_hips_neutral.wav"
                        },
                        new JointAngleRule
                        {
                            JointName = "RightHip",
                            MinAngle  = 168.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Keep your hips neutral.",
                            AudioFile = "corrective-feedback/core-hips/keep_hips_neutral.wav"
                        },
                        new JointAngleRule
                        {
                            JointName = "LeftWrist",
                            MinAngle  = 165.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Straighten your left wrist.",
                            AudioFile = "corrective-feedback/wrists/straighten_left_wrist.wav"
                        },
                        new JointAngleRule
                        {
                            JointName = "RightWrist",
                            MinAngle  = 165.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Straighten your right wrist.",
                            AudioFile = "corrective-feedback/wrists/straighten_right_wrist.wav"
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
            return new List<PoseDefinition>
            {
                new PoseDefinition
                {
                    Name          = "Tree Pose",
                    Description   = "Stand on one leg with the other foot resting on the inner thigh, hands raised high above your head.",
                    BriefInfo     = "Improves balance and focus while stretching your shoulders and arms overhead.",
                    Difficulty    = DifficultyLevel.Intermediate,
                    HoldSeconds   = 60,
                    ImageFileName = "tree-pose.png",
                    InstructionAudioFolder = "tree-pose",
                    InstructionAudioPrefix = "tree_",

                    Instructions = new List<string>
                    {
                        "Stand tall and shift your weight onto your left foot.",
                        "Place your right foot on your left inner thigh or calf, avoiding the knee.",
                        "Find your balance and engage your core.",
                        "Slowly raise both hands straight overhead, reaching towards the sky.",
                        "Keep your shoulders relaxed away from your ears."
                    },

                    Rules = new List<JointAngleRule>
                    {
                        new JointAngleRule
                        {
                            JointName = "LeftShoulder",
                            MinAngle  = 160.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Raise your left arm.",
                            AudioFile = "corrective-feedback/arms-shoulders/raise_left_arm.wav"
                        },
                        new JointAngleRule
                        {
                            JointName = "RightShoulder",
                            MinAngle  = 160.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Raise your right arm.",
                            AudioFile = "corrective-feedback/arms-shoulders/raise_right_arm.wav"
                        },
                        new JointAngleRule
                        {
                            JointName = "LeftKnee",
                            MinAngle  = 170.0,
                            MaxAngle  = 180.0,
                            Feedback  = "Straighten your left leg.",
                            AudioFile = "corrective-feedback/legs-knees/straighten_left_leg.wav"
                        }
                    }
                }
            };
        }

        // ---------------------------------------------------------------
        // ADVANCED POSES
        // ---------------------------------------------------------------
        private static List<PoseDefinition> GetAdvancedPoses()
        {
            // Temporarily empty
            return new List<PoseDefinition>();
        }
    }
}
