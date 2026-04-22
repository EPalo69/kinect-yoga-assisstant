using capstoneOneShot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace capstoneOneShot.Services
{
    /// <summary>
    /// Runs a series of Range of Motion (ROM) tests at the start of a session
    /// to determine the user's flexibility level and assign an appropriate difficulty.
    /// </summary>
    public class ROMTestService
    {
        // Stores the measured max angle for each ROM test movement
        private Dictionary<string, double> _romResults = new Dictionary<string, double>();

        // The tests to run in order
        public List<ROMTest> Tests { get; private set; }

        // Index of the currently active test
        public int CurrentTestIndex { get; private set; } = 0;

        public bool IsComplete => CurrentTestIndex >= Tests.Count;

        public ROMTestService()
        {
            Tests = BuildTestSequence();
        }

        // ---------------------------------------------------------------
        // Define the ROM test movements
        // ---------------------------------------------------------------
        private List<ROMTest> BuildTestSequence()
        {
            return new List<ROMTest>
            {
                new ROMTest
                {
                    Name        = "Shoulder Raise",
                    Instruction = "Slowly raise both arms as high as you can overhead and hold.",
                    JointToMeasure = "LeftShoulder",
                    DurationSeconds = 5
                },
                new ROMTest
                {
                    Name        = "Knee Bend",
                    Instruction = "Slowly bend your knees as deep as you can as if sitting down, then hold.",
                    JointToMeasure = "LeftKnee",
                    DurationSeconds = 5
                },
                new ROMTest
                {
                    Name        = "Hip Hinge",
                    Instruction = "Stand straight then slowly bend forward at the hips as far as comfortable.",
                    JointToMeasure = "LeftHip",
                    DurationSeconds = 5
                },
                new ROMTest
                {
                    Name        = "Lateral Arm Raise",
                    Instruction = "Raise both arms out to your sides as high as you can and hold.",
                    JointToMeasure = "RightShoulder",
                    DurationSeconds = 5
                }
            };
        }

        // ---------------------------------------------------------------
        // Called every frame to track the best (max/min) angle seen
        // ---------------------------------------------------------------
        public void RecordFrame(Dictionary<string, double> currentAngles)
        {
            if (IsComplete) return;

            var test = Tests[CurrentTestIndex];

            if (currentAngles.TryGetValue(test.JointToMeasure, out double angle))
            {
                // Track the best (most extreme) angle seen during this test
                if (!_romResults.ContainsKey(test.JointToMeasure))
                    _romResults[test.JointToMeasure] = angle;
                else
                {
                    // For shoulder/hip raises we want the MAX angle (most open)
                    // For knee bends we want the MIN angle (most bent)
                    if (test.TrackMinimum)
                        _romResults[test.JointToMeasure] = System.Math.Min(_romResults[test.JointToMeasure], angle);
                    else
                        _romResults[test.JointToMeasure] = System.Math.Max(_romResults[test.JointToMeasure], angle);
                }
            }
        }

        // ---------------------------------------------------------------
        // Advance to the next test
        // ---------------------------------------------------------------
        public void AdvanceToNextTest()
        {
            CurrentTestIndex++;
        }

        public ROMTest GetCurrentTest()
        {
            if (IsComplete) return null;
            return Tests[CurrentTestIndex];
        }

        // ---------------------------------------------------------------
        // Evaluate all results and assign a difficulty level
        // ---------------------------------------------------------------
        public DifficultyLevel EvaluateDifficulty()
        {
            int score = 0;

            // Shoulder mobility — can they raise arms overhead?
            if (_romResults.TryGetValue("LeftShoulder", out double leftShoulder))
            {
                if (leftShoulder >= 150) score += 2;       // full range
                else if (leftShoulder >= 100) score += 1;  // moderate
            }

            // Knee flexibility — how deep can they bend?
            if (_romResults.TryGetValue("LeftKnee", out double leftKnee))
            {
                if (leftKnee <= 90) score += 2;           // deep bend
                else if (leftKnee <= 120) score += 1;      // moderate bend
            }

            // Hip hinge
            if (_romResults.TryGetValue("LeftHip", out double leftHip))
            {
                if (leftHip <= 60) score += 2;            // good forward fold
                else if (leftHip <= 90) score += 1;        // moderate
            }

            // Lateral shoulder
            if (_romResults.TryGetValue("RightShoulder", out double rightShoulder))
            {
                if (rightShoulder >= 80) score += 2;
                else if (rightShoulder >= 50) score += 1;
            }

            // Max possible score = 8
            if (score >= 6) return DifficultyLevel.Advanced;
            if (score >= 3) return DifficultyLevel.Intermediate;
            return DifficultyLevel.Beginner;
        }

        public void Reset()
        {
            CurrentTestIndex = 0;
            _romResults.Clear();
        }
    }

    // ---------------------------------------------------------------
    // Data class representing a single ROM test movement
    // ---------------------------------------------------------------
    public class ROMTest
    {
        public string Name { get; set; }
        public string Instruction { get; set; }
        public string JointToMeasure { get; set; }
        public int DurationSeconds { get; set; }

        /// <summary>
        /// If true, we track the minimum angle (e.g. knee bends).
        /// If false, we track the maximum angle (e.g. arm raises).
        /// </summary>
        public bool TrackMinimum { get; set; } = false;
    }
}
