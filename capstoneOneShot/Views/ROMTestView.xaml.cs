using capstoneOneShot.Models;
using capstoneOneShot.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;


namespace capstoneOneShot.Views
{
    public partial class ROMTestView : Window
    {
        private readonly ROMTestService _romService;
        private readonly KinectManager _kinectManager;

        private DispatcherTimer _countdownTimer;
        private int _secondsRemaining;
        private bool _testRunning = false;

        // The difficulty result — read this after the window closes
        public DifficultyLevel ResultDifficulty { get; private set; } = DifficultyLevel.Beginner;

        public ROMTestView(KinectManager kinectManager)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _romService = new ROMTestService();

            // Hook into skeleton frames for live angle display
            _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;

            LoadCurrentTest();
        }

        // ---------------------------------------------------------------
        // Load the current test instructions into the UI
        // ---------------------------------------------------------------
        private void LoadCurrentTest()
        {
            if (_romService.IsComplete)
            {
                FinishAllTests();
                return;
            }

            var test = _romService.GetCurrentTest();

            TestNameLabel.Text = test.Name;
            InstructionLabel.Text = test.Instruction;
            ProgressLabel.Text = $"Test {_romService.CurrentTestIndex + 1} of {_romService.Tests.Count}";
            ProgressBar.Value = _romService.CurrentTestIndex + 1;
            AngleReadout.Text = "-- °";
            BestAngleReadout.Text = "-- °";
            NextButton.Content = "Start Test";
            _testRunning = false;
        }

        // ---------------------------------------------------------------
        // Start / Next button logic
        // ---------------------------------------------------------------
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_testRunning)
                StartCountdown();
            else
                AdvanceTest();
        }

        private void StartCountdown()
        {
            _testRunning = true;
            NextButton.Content = "Recording...";
            NextButton.IsEnabled = false;

            var test = _romService.GetCurrentTest();
            _secondsRemaining = test.DurationSeconds;
            CountdownText.Text = _secondsRemaining.ToString();

            _countdownTimer = new DispatcherTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += CountdownTick;
            _countdownTimer.Start();
        }

        private void CountdownTick(object sender, EventArgs e)
        {
            _secondsRemaining--;
            CountdownText.Text = _secondsRemaining.ToString();

            if (_secondsRemaining <= 0)
            {
                _countdownTimer.Stop();
                NextButton.Content = _romService.CurrentTestIndex < _romService.Tests.Count - 1
                                       ? "Next Test →"
                                       : "See Results";
                NextButton.IsEnabled = true;
            }
        }

        private void AdvanceTest()
        {
            _romService.AdvanceToNextTest();
            LoadCurrentTest();
        }

        // ---------------------------------------------------------------
        // Skeleton frame — record angles and update UI readouts
        // ---------------------------------------------------------------
        private void OnSkeletonFrameReady(Microsoft.Kinect.Skeleton[] skeletons)
        {
            if (!_testRunning || _secondsRemaining <= 0) return;
            if (skeletons == null || skeletons.Length == 0) return;

            var skeleton = skeletons[0];

            // Build angle dictionary from skeleton
            var angles = BuildAngleDictionary(skeleton);

            // Let the service record the best angle
            _romService.RecordFrame(angles);

            // Update UI on dispatcher thread
            var test = _romService.GetCurrentTest();
            if (test == null) return;

            if (angles.TryGetValue(test.JointToMeasure, out double currentAngle))
            {
                Dispatcher.Invoke(() =>
                {
                    AngleReadout.Text = $"{currentAngle:F1} °";
                    BestAngleReadout.Text = $"{currentAngle:F1} °"; // ROMTestService tracks the best internally
                });
            }
        }

        // ---------------------------------------------------------------
        // Build a simple angle dictionary from skeleton joints
        // ---------------------------------------------------------------
        private Dictionary<string, double> BuildAngleDictionary(Microsoft.Kinect.Skeleton skeleton)
        {
            var joints = skeleton.Joints;
            var angles = new Dictionary<string, double>();

            // Left Shoulder angle (ShoulderCenter - ShoulderLeft - ElbowLeft)
            angles["LeftShoulder"] = JointAngleCalculator.CalculateAngle(
                joints[Microsoft.Kinect.JointType.ShoulderCenter].Position,
                joints[Microsoft.Kinect.JointType.ShoulderLeft].Position,
                joints[Microsoft.Kinect.JointType.ElbowLeft].Position);

            // Right Shoulder angle
            angles["RightShoulder"] = JointAngleCalculator.CalculateAngle(
                joints[Microsoft.Kinect.JointType.ShoulderCenter].Position,
                joints[Microsoft.Kinect.JointType.ShoulderRight].Position,
                joints[Microsoft.Kinect.JointType.ElbowRight].Position);

            // Left Knee angle (HipLeft - KneeLeft - AnkleLeft)
            angles["LeftKnee"] = JointAngleCalculator.CalculateAngle(
                joints[Microsoft.Kinect.JointType.HipLeft].Position,
                joints[Microsoft.Kinect.JointType.KneeLeft].Position,
                joints[Microsoft.Kinect.JointType.AnkleLeft].Position);

            // Left Hip angle (ShoulderCenter - HipCenter - KneeLeft)
            angles["LeftHip"] = JointAngleCalculator.CalculateAngle(
                joints[Microsoft.Kinect.JointType.ShoulderCenter].Position,
                joints[Microsoft.Kinect.JointType.HipCenter].Position,
                joints[Microsoft.Kinect.JointType.KneeLeft].Position);

            return angles;
        }

        // ---------------------------------------------------------------
        // All tests done — evaluate and close
        // ---------------------------------------------------------------
        private void FinishAllTests()
        {
            ResultDifficulty = _romService.EvaluateDifficulty();

            string levelText = ResultDifficulty.ToString();
            MessageBox.Show(
                $"ROM Test complete!\n\nYour recommended difficulty level is: {levelText}\n\nWe'll start your session with {levelText} poses.",
                "Assessment Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
            DialogResult = true;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _countdownTimer?.Stop();
            _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
            base.OnClosed(e);
        }
    }
}
