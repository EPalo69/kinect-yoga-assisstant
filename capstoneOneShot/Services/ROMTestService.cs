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

        public UserROMProfile BuildProfile(DifficultyLevel difficulty)
        {
            // Test 1 — Overhead Star Reach: primary = LeftShoulder (shoulder flexion)
            _romResults.TryGetValue("OverheadShoulder", out double overheadShoulder);

            // Test 2 — Lateral Arm Raise: primary = LeftShoulder abduction
            _romResults.TryGetValue("LateralShoulder", out double lateralShoulder);

            // Test 3 — Wide Squat: primary = LeftKnee (knee flexion, track minimum)
            double kneeFlexion = 180;
            if (_romResults.TryGetValue("SquatKnee", out double sk)) kneeFlexion = sk;

            // Test 4 — Single-Leg Balance: primary = HipCenter drop (track minimum Y diff)
            _romResults.TryGetValue("BalanceHipDrop", out double hipDrop);

            return new UserROMProfile
            {
                ShoulderFlexion  = overheadShoulder,
                LateralShoulder  = lateralShoulder,
                KneeFlexion      = kneeFlexion,
                HipFlexion       = hipDrop,          // repurposed: hip drop during balance
                AssignedDifficulty = difficulty,
                IsComplete = true
            };
        }

        // ---------------------------------------------------------------
        // Define the ROM test movements
        // ---------------------------------------------------------------
        private List<ROMTest> BuildTestSequence()
        {
            return new List<ROMTest>
            {
                // ── Test 1: Overhead Star Reach ─────────────────────────────
                // Arms neutral → fully overhead, feet slightly apart.
                // Primary metric: shoulder flexion (max angle).
                new ROMTest
                {
                    Name            = "Overhead Star Reach",
                    Instruction     = "Stand tall. Raise both arms as high as you can overhead, then hold. Let your feet step slightly apart.",
                    JointToMeasure  = "OverheadShoulder",
                    DurationSeconds = 8,
                    ImagePath       = "pack://application:,,,/Assets/ROM/overhead-star-reach.png",
                    TrackMinimum    = false   // want MAX (most open)
                },

                // ── Test 2: Lateral Arm Raise ────────────────────────────────
                // Arms raise sideways to ~180°.
                // Primary metric: shoulder abduction (max angle).
                new ROMTest
                {
                    Name            = "Lateral Arm Raise",
                    Instruction     = "From neutral, raise both arms out to your sides as high as possible and hold.",
                    JointToMeasure  = "LateralShoulder",
                    DurationSeconds = 7,
                    ImagePath       = "pack://application:,,,/Assets/ROM/lateral-arm-raise.png",
                    TrackMinimum    = false   // want MAX
                },

                // ── Test 3: Wide Squat ───────────────────────────────────────
                // Feet apart → squat down as deep as possible.
                // Primary metric: knee flexion (min angle = deeper bend).
                new ROMTest
                {
                    Name            = "Wide Squat",
                    Instruction     = "Step your feet wide apart, then squat down as low as comfortable. Hold at your deepest point.",
                    JointToMeasure  = "SquatKnee",
                    DurationSeconds = 8,
                    ImagePath       = "pack://application:,,,/Assets/ROM/wide-squat.png",
                    TrackMinimum    = true    // want MIN (most bent)
                },

                // ── Test 4: Single-Leg Balance ───────────────────────────────
                // Lift one leg (tree prep). Stability + asymmetry baseline.
                // Primary metric: hip drop variance (min = most stable).
                new ROMTest
                {
                    Name            = "Single-Leg Balance",
                    Instruction     = "Shift weight onto your right leg and lift your left foot off the ground. Hold still for as long as you can.",
                    JointToMeasure  = "BalanceHipDrop",
                    DurationSeconds = 8,
                    ImagePath       = "pack://application:,,,/Assets/ROM/single-leg-balance.png",
                    TrackMinimum    = true    // smaller drop = better stability
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

            // ── Test 1: Overhead Star Reach — shoulder flexion ───────────────
            // Full overhead = ~170°; decent = 130°+
            if (_romResults.TryGetValue("OverheadShoulder", out double overhead))
            {
                if (overhead >= 155) score += 2;       // near-full overhead
                else if (overhead >= 120) score += 1;  // moderate reach
            }

            // ── Test 2: Lateral Arm Raise — shoulder abduction ───────────────
            // Full abduction = ~90° at the joint angle
            if (_romResults.TryGetValue("LateralShoulder", out double lateral))
            {
                if (lateral >= 80) score += 2;         // full lateral range
                else if (lateral >= 55) score += 1;    // moderate
            }

            // ── Test 3: Wide Squat — knee flexion ────────────────────────────
            // Deeper = lower angle; full squat ~60°, moderate ~90°
            if (_romResults.TryGetValue("SquatKnee", out double squat))
            {
                if (squat <= 70)  score += 2;          // deep squat
                else if (squat <= 105) score += 1;     // moderate
            }

            // ── Test 4: Single-Leg Balance — stability ────────────────────────
            // Hip drop during balance: smaller = more stable
            // 0.02 = very stable, 0.06 = moderate sway
            if (_romResults.TryGetValue("BalanceHipDrop", out double hipDrop))
            {
                if (hipDrop <= 0.025) score += 2;      // stable
                else if (hipDrop <= 0.05) score += 1;  // moderate sway
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
        public string ImagePath { get; set; }

        /// <summary>
        /// If true, we track the minimum angle (e.g. knee bends).
        /// If false, we track the maximum angle (e.g. arm raises).
        /// </summary>
        public bool TrackMinimum { get; set; } = false;
    }


}
