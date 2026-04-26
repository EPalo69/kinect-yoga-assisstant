using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace capstoneOneShot.Models
{
    public enum DifficultyLevel
    {
        Beginner,
        Intermediate,
        Advanced
    }

    public class JointAngleRule
    {
        public string JointName { get; set; }
        public double MinAngle { get; set; }
        public double MaxAngle { get; set; }
        public string Feedback { get; set; }
    }

    public class PoseDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DifficultyLevel Difficulty { get; set; }
        public int HoldSeconds { get; set; }
        /// <summary>Filename of the reference image inside Assets/Poses/ (e.g. "mountain-pose.png").</summary>
        public string ImageFileName { get; set; }
        public List<JointAngleRule> Rules { get; set; } = new List<JointAngleRule>();
    }
}
