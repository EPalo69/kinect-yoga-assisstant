using System.Collections.Generic;
using System.Linq;
using capstoneOneShot.Models;

namespace capstoneOneShot.Services
{
    public class EvaluationResult
    {
        public double Score { get; set; }
        public List<string> Feedback { get; set; } = new List<string>();
        public bool IsPoseCorrect => Score >= 70; // lowered from 80
    }

    public class PoseEvaluator
    {
        // ── Confidence window ───────────────────────────────────────────
        // Instead of judging every single frame, we keep a rolling buffer
        // of the last N frames and only report a joint as "failing" if it
        // fails in the majority of those frames. This stops a single noisy
        // frame from flashing a red correction at the user.
        private const int WindowSize = 10; // ~0.33 seconds at 30fps

        // jointName -> circular buffer of pass/fail bools
        private readonly Dictionary<string, Queue<bool>> _history
            = new Dictionary<string, Queue<bool>>();

        // ── Angle tolerance ─────────────────────────────────────────────
        // Added on top of each pose's min/max to give the user wiggle room.
        // Think of this as the "close enough" buffer — like how rhythm games
        // have a timing window so you don't have to be frame-perfect.
        private const double AngleTolerance = 15.0; // degrees

        public EvaluationResult Evaluate(PoseDefinition pose,
                                         Dictionary<string, double> userAngles)
        {
            var result = new EvaluationResult();
            int passed = 0;

            foreach (var rule in pose.Rules)
            {
                if (!userAngles.TryGetValue(rule.JointName, out double angle))
                    continue;

                // Apply tolerance — widen the acceptable window
                bool framePass = angle >= (rule.MinAngle - AngleTolerance)
                              && angle <= (rule.MaxAngle + AngleTolerance);

                // Push into history buffer
                if (!_history.ContainsKey(rule.JointName))
                    _history[rule.JointName] = new Queue<bool>();

                var buf = _history[rule.JointName];
                buf.Enqueue(framePass);
                if (buf.Count > WindowSize) buf.Dequeue();

                // A joint is considered passing if it passes in >60% of
                // the recent frames — same idea as a "perfect" window in
                // Guitar Hero / Just Dance
                double passRate = buf.Count(x => x) / (double)buf.Count;
                bool smoothedPass = passRate >= 0.6;

                if (smoothedPass)
                {
                    passed++;
                }
                else
                {
                    result.Feedback.Add(rule.Feedback);
                }
            }

            result.Score = pose.Rules.Count > 0
                ? (double)passed / pose.Rules.Count * 100
                : 0;

            return result;
        }

        // Call this when switching to a new pose so old history
        // doesn't bleed into the new pose's evaluation
        public void ResetHistory() => _history.Clear();
    }
}