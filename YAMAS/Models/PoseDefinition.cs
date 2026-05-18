using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace YAMAS.Models
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
        public string AudioFile { get; set; }
    }

    public class PoseDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        /// <summary>Short tagline displayed prominently on the Pose Detail screen.</summary>
        public string BriefInfo { get; set; }
        public DifficultyLevel Difficulty { get; set; }
        public int HoldSeconds { get; set; }
        public string ImageFileName { get; set; }
        public string InstructionAudioFolder { get; set; }
        public string InstructionAudioPrefix { get; set; }
        public List<string> Instructions { get; set; } = new List<string>();
        public List<JointAngleRule> Rules { get; set; } = new List<JointAngleRule>();

        // UI Properties
        public bool IsSelectable { get; set; } = true;
        public string WarningMessage { get; set; } = "";
        public Visibility WarningVisibility => IsSelectable ? Visibility.Collapsed : Visibility.Visible;
        public double CardOpacity => IsSelectable ? 1.0 : 0.5;
    }
}
