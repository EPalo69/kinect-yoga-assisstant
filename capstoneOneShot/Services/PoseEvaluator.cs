using System.Collections.Generic;
using System.Linq;
using capstoneOneShot.Models;

namespace capstoneOneShot.Services
{
    //public class EvaluationResult
    //{
    //    public double Score { get; set; }
    //    public List<string> Feedback { get; set; } = new List<string>();
    //    public bool IsPoseCorrect => Score >= 70; // lowered from 80
    //}

    // ── Classification tiers ────────────────────────────────────────────
    public enum PoseClassification
    {
        Correct,      // ≥ 80% score
        Acceptable,   // ≥ 60% score
        Incorrect     // < 60% score
    }

    // ── Per-joint deviation detail ──────────────────────────────────────
    public class JointDeviation
    {
        public string JointName { get; set; }
        public double ActualAngle { get; set; }
        public double ExpectedMin { get; set; }
        public double ExpectedMax { get; set; }
        public double DeviationDeg { get; set; } // 0 if within range, else degrees off
        public bool IsPassing { get; set; }
        public string Feedback { get; set; }
        public string AudioFile { get; set; }
    }

    public class EvaluationResult
    {
        public double Score { get; set; }
        public List<string> Feedback { get; set; } = new List<string>();
        public List<string> AudioFiles { get; set; } = new List<string>();

        // ★ Per-joint breakdown
        public List<JointDeviation> JointDeviations { get; set; } = new List<JointDeviation>();

        // ★ Three-tier classification
        public PoseClassification Classification =>
            Score >= 80 ? PoseClassification.Correct :
            Score >= 60 ? PoseClassification.Acceptable :
                          PoseClassification.Incorrect;

        // ★ Convenience bools
        public bool IsPoseCorrect => Classification == PoseClassification.Correct;
        public bool IsPoseAcceptable => Classification == PoseClassification.Acceptable;
        public bool IsPoseIncorrect => Classification == PoseClassification.Incorrect;

        // ★ Worst deviation across all joints (useful for overall feedback)
        public double MaxDeviationDeg =>
            JointDeviations.Count > 0
                ? JointDeviations.Max(j => j.DeviationDeg)
                : 0;

        public override string ToString() => Score.ToString("F0");
    }

    public class PoseEvaluator
    {
        private const int WindowSize = 10;   // ~0.33s at 30fps
        private const double AngleTolerance = 15.0; // degrees wiggle room

        private readonly Dictionary<string, Queue<bool>> _history = new Dictionary<string, Queue<bool>>();
        private readonly Dictionary<string, Queue<double>> _angleHistory = new Dictionary<string, Queue<double>>();

        public EvaluationResult Evaluate(PoseDefinition pose,
                                         Dictionary<string, double> userAngles)
        {
            var result = new EvaluationResult();
            int passed = 0;

            foreach (var rule in pose.Rules)
            {
                if (!userAngles.TryGetValue(rule.JointName, out double angle))
                    continue;

                // ── Pass/fail with tolerance ────────────────────────────
                bool framePass = angle >= (rule.MinAngle - AngleTolerance)
                              && angle <= (rule.MaxAngle + AngleTolerance);

                // ── Rolling pass/fail buffer ────────────────────────────
                if (!_history.ContainsKey(rule.JointName))
                    _history[rule.JointName] = new Queue<bool>();

                var buf = _history[rule.JointName];
                buf.Enqueue(framePass);
                if (buf.Count > WindowSize) buf.Dequeue();

                double passRate = buf.Count(x => x) / (double)buf.Count;
                bool smoothedPass = passRate >= 0.6;

                // ── Rolling angle buffer for stable deviation ───────────
                if (!_angleHistory.ContainsKey(rule.JointName))
                    _angleHistory[rule.JointName] = new Queue<double>();

                var angleBuf = _angleHistory[rule.JointName];
                angleBuf.Enqueue(angle);
                if (angleBuf.Count > WindowSize) angleBuf.Dequeue();

                double smoothedAngle = angleBuf.Average();

                // ── Calculate deviation ─────────────────────────────────
                double deviation = 0;
                if (smoothedAngle < rule.MinAngle - AngleTolerance)
                    deviation = (rule.MinAngle - AngleTolerance) - smoothedAngle;
                else if (smoothedAngle > rule.MaxAngle + AngleTolerance)
                    deviation = smoothedAngle - (rule.MaxAngle + AngleTolerance);

                // ── Build per-joint record ──────────────────────────────
                result.JointDeviations.Add(new JointDeviation
                {
                    JointName = rule.JointName,
                    ActualAngle = smoothedAngle,
                    ExpectedMin = rule.MinAngle,
                    ExpectedMax = rule.MaxAngle,
                    DeviationDeg = deviation,
                    IsPassing = smoothedPass,
                    Feedback = rule.Feedback,
                    AudioFile = rule.AudioFile
                });

                result.JointDeviations = result.JointDeviations
                    .OrderByDescending(j => j.DeviationDeg)
                    .ToList();

                result.Feedback = result.JointDeviations
                    .Where(j => !j.IsPassing)
                    .Select(j => j.Feedback)
                    .ToList();

                result.AudioFiles = result.JointDeviations
                    .Where(j => !j.IsPassing)
                    .Select(j => j.AudioFile)
                    .ToList();

                if (smoothedPass)
                    passed++;
                else
                {
                    result.Feedback.Add(rule.Feedback);
                    result.AudioFiles.Add(rule.AudioFile);
                }
            }

            result.Score = pose.Rules.Count > 0
                ? (double)passed / pose.Rules.Count * 100
                : 0;

            return result;
        }

        public void ResetHistory()
        {
            _history.Clear();
            _angleHistory.Clear();
        }
    }
}