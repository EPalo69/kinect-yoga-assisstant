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
                },

                // --- Warrior I (Virabhadrasana I) ---
                // Front knee bent ~90°, back leg straight, arms raised overhead
                new PoseDefinition
                {
                    Name        = "Warrior I",
                    Description = "Step one foot forward, bend the front knee to 90°, and raise both arms overhead.",
                    Difficulty  = DifficultyLevel.Beginner,
                    HoldSeconds = 30,
                    Rules = new List<JointAngleRule>
                    {
                        new JointAngleRule
                        {
                            JointName  = "LeftKnee",
                            MinAngle   = 80,
                            MaxAngle   = 100,
                            Feedback   = "Bend your front knee closer to 90 degrees."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightKnee",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Keep your back leg straight."
                        },
                        new JointAngleRule
                        {
                            JointName  = "LeftShoulder",
                            MinAngle   = 150,
                            MaxAngle   = 180,
                            Feedback   = "Raise your left arm higher overhead."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightShoulder",
                            MinAngle   = 150,
                            MaxAngle   = 180,
                            Feedback   = "Raise your right arm higher overhead."
                        }
                    }
                },

                // --- Tree Pose (Vrksasana) ---
                // Standing on one leg, other foot on inner thigh, arms overhead or at chest
                new PoseDefinition
                {
                    Name        = "Tree Pose",
                    Description = "Balance on one leg with the other foot pressed to the inner thigh. Hands at heart or overhead.",
                    Difficulty  = DifficultyLevel.Beginner,
                    HoldSeconds = 20,
                    Rules = new List<JointAngleRule>
                    {
                        new JointAngleRule
                        {
                            JointName  = "LeftKnee",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Keep your standing (left) leg straight."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightHip",
                            MinAngle   = 60,
                            MaxAngle   = 90,
                            Feedback   = "Open your right hip outward more."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightKnee",
                            MinAngle   = 60,
                            MaxAngle   = 90,
                            Feedback   = "Bend your right knee and press foot to inner thigh."
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
                // --- Warrior II (Virabhadrasana II) ---
                // Front knee bent ~90°, arms extended out to sides parallel to floor
                new PoseDefinition
                {
                    Name        = "Warrior II",
                    Description = "Bend front knee to 90°, extend arms out to the sides at shoulder height.",
                    Difficulty  = DifficultyLevel.Intermediate,
                    HoldSeconds = 30,
                    Rules = new List<JointAngleRule>
                    {
                        new JointAngleRule
                        {
                            JointName  = "LeftKnee",
                            MinAngle   = 80,
                            MaxAngle   = 100,
                            Feedback   = "Bend your front knee to 90 degrees."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightKnee",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Keep your back leg straight."
                        },
                        new JointAngleRule
                        {
                            JointName  = "LeftShoulder",
                            MinAngle   = 80,
                            MaxAngle   = 100,
                            Feedback   = "Extend your left arm to shoulder height."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightShoulder",
                            MinAngle   = 80,
                            MaxAngle   = 100,
                            Feedback   = "Extend your right arm to shoulder height."
                        },
                        new JointAngleRule
                        {
                            JointName  = "LeftElbow",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Keep your left arm straight."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightElbow",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Keep your right arm straight."
                        }
                    }
                },

                // --- Chair Pose (Utkatasana) ---
                // Knees bent as if sitting in a chair, arms raised overhead
                new PoseDefinition
                {
                    Name        = "Chair Pose",
                    Description = "Bend your knees as if sitting in a chair, arms extended overhead.",
                    Difficulty  = DifficultyLevel.Intermediate,
                    HoldSeconds = 25,
                    Rules = new List<JointAngleRule>
                    {
                        new JointAngleRule
                        {
                            JointName  = "LeftKnee",
                            MinAngle   = 90,
                            MaxAngle   = 120,
                            Feedback   = "Bend your left knee deeper into chair pose."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightKnee",
                            MinAngle   = 90,
                            MaxAngle   = 120,
                            Feedback   = "Bend your right knee deeper into chair pose."
                        },
                        new JointAngleRule
                        {
                            JointName  = "LeftShoulder",
                            MinAngle   = 150,
                            MaxAngle   = 180,
                            Feedback   = "Raise your left arm higher."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightShoulder",
                            MinAngle   = 150,
                            MaxAngle   = 180,
                            Feedback   = "Raise your right arm higher."
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
            return new List<PoseDefinition>
            {
                // --- Triangle Pose (Trikonasana) ---
                // Legs wide, torso bent sideways, one arm down to ankle, other up
                new PoseDefinition
                {
                    Name        = "Triangle Pose",
                    Description = "Wide stance, bend sideways reaching one hand to ankle and the other straight up.",
                    Difficulty  = DifficultyLevel.Advanced,
                    HoldSeconds = 30,
                    Rules = new List<JointAngleRule>
                    {
                        new JointAngleRule
                        {
                            JointName  = "LeftKnee",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Straighten your left leg."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightKnee",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Straighten your right leg."
                        },
                        new JointAngleRule
                        {
                            JointName  = "LeftShoulder",
                            MinAngle   = 80,
                            MaxAngle   = 100,
                            Feedback   = "Extend your top arm straight up."
                        },
                        new JointAngleRule
                        {
                            JointName  = "LeftHip",
                            MinAngle   = 50,
                            MaxAngle   = 80,
                            Feedback   = "Bend further at the hip to reach lower."
                        }
                    }
                },

                // --- Warrior III (Virabhadrasana III) ---
                // Balancing on one leg, body and other leg parallel to floor, arms forward
                new PoseDefinition
                {
                    Name        = "Warrior III",
                    Description = "Balance on one leg with your body and raised leg parallel to the floor, arms extended forward.",
                    Difficulty  = DifficultyLevel.Advanced,
                    HoldSeconds = 20,
                    Rules = new List<JointAngleRule>
                    {
                        new JointAngleRule
                        {
                            JointName  = "LeftKnee",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Keep your standing leg straight."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightKnee",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Extend your raised leg straight back."
                        },
                        new JointAngleRule
                        {
                            JointName  = "LeftHip",
                            MinAngle   = 85,
                            MaxAngle   = 95,
                            Feedback   = "Tilt your torso forward to be parallel to the floor."
                        },
                        new JointAngleRule
                        {
                            JointName  = "LeftShoulder",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Extend your arms forward in line with your body."
                        },
                        new JointAngleRule
                        {
                            JointName  = "RightShoulder",
                            MinAngle   = 160,
                            MaxAngle   = 180,
                            Feedback   = "Keep both arms extended forward."
                        }
                    }
                }
            };
        }
    }
}
