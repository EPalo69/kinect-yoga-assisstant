using capstoneOneShot.Models;
using capstoneOneShot.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Kinect;

namespace capstoneOneShot.Views
{
    public partial class ROMTestView : Window
    {
        private readonly ROMTestService _romService;
        private readonly KinectManager _kinectManager;

        private DispatcherTimer _countdownTimer;
        private int _secondsRemaining;
        private bool _testRunning = false;

        private const double JointRadius = 6;

        public DifficultyLevel ResultDifficulty { get; private set; } = DifficultyLevel.Beginner;

        public ROMTestView(KinectManager kinectManager)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _romService = new ROMTestService();

            _kinectManager.ColorFrameReady += OnColorFrameReady;
            _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;
            _kinectManager.BodyStatusChanged += OnBodyStatusChanged;

            LoadCurrentTest();
        }

        // ---------------------------------------------------------------
        // Load current test into UI
        // ---------------------------------------------------------------
        private void LoadCurrentTest()
        {
            if (_romService.IsComplete) { FinishAllTests(); return; }

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
        // Camera feed
        // ---------------------------------------------------------------
        private void OnColorFrameReady(System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            Dispatcher.Invoke(() => CameraFeed.Source = bitmap);
        }

        // ---------------------------------------------------------------
        // Body detection warnings — reuse same logic as SessionView
        // ---------------------------------------------------------------
        private void OnBodyStatusChanged(BodyDetectionStatus status)
        {
            Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case BodyDetectionStatus.Detected:
                        NoBodyOverlay.Visibility = Visibility.Collapsed;
                        PartialBodyBanner.Visibility = Visibility.Collapsed;
                        SetTrackingStatus("Fully Tracked", "#4CAF50");
                        break;
                    case BodyDetectionStatus.PartialDetect:
                        NoBodyOverlay.Visibility = Visibility.Collapsed;
                        PartialBodyBanner.Visibility = Visibility.Visible;
                        SetTrackingStatus("Partial Tracking", "#F59E0B");
                        break;
                    case BodyDetectionStatus.NotDetected:
                        NoBodyOverlay.Visibility = Visibility.Visible;
                        PartialBodyBanner.Visibility = Visibility.Collapsed;
                        SetTrackingStatus("Not Detected", "#EF4444");
                        AngleReadout.Text = "-- °";
                        BestAngleReadout.Text = "-- °";
                        break;
                }
            });
        }

        private void SetTrackingStatus(string text, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            TrackingStatusLabel.Text = text;
            TrackingStatusLabel.Foreground = new SolidColorBrush(color);
            TrackingDot.Fill = new SolidColorBrush(color);
        }

        // ---------------------------------------------------------------
        // Skeleton frame — record angles, draw skeleton, update readouts
        // ---------------------------------------------------------------
        private void OnSkeletonFrameReady(Skeleton[] skeletons)
        {
            if (skeletons == null || skeletons.Length == 0) return;

            var skeleton = skeletons[0];
            var angles = BuildAngleDictionary(skeleton);

            if (_testRunning && _secondsRemaining > 0)
                _romService.RecordFrame(angles);

            var test = _romService.GetCurrentTest();
            if (test == null) return;

            angles.TryGetValue(test.JointToMeasure, out double currentAngle);

            Dispatcher.Invoke(() =>
            {
                AngleReadout.Text = currentAngle.ToString("F1") + " °";
                BestAngleReadout.Text = currentAngle.ToString("F1") + " °";
                DrawSkeleton(skeleton);
            });
        }

        // ---------------------------------------------------------------
        // Skeleton drawing
        // ---------------------------------------------------------------
        private void DrawSkeleton(Skeleton skeleton)
        {
            SkeletonCanvas.Children.Clear();

            var bones = new[]
            {
                (JointType.Head,           JointType.ShoulderCenter),
                (JointType.ShoulderCenter, JointType.ShoulderLeft),
                (JointType.ShoulderCenter, JointType.ShoulderRight),
                (JointType.ShoulderLeft,   JointType.ElbowLeft),
                (JointType.ElbowLeft,      JointType.WristLeft),
                (JointType.ShoulderRight,  JointType.ElbowRight),
                (JointType.ElbowRight,     JointType.WristRight),
                (JointType.ShoulderCenter, JointType.HipCenter),
                (JointType.HipCenter,      JointType.HipLeft),
                (JointType.HipCenter,      JointType.HipRight),
                (JointType.HipLeft,        JointType.KneeLeft),
                (JointType.KneeLeft,       JointType.AnkleLeft),
                (JointType.HipRight,       JointType.KneeRight),
                (JointType.KneeRight,      JointType.AnkleRight),
            };

            foreach (var (a, b) in bones) DrawBone(skeleton, a, b);
            foreach (var jt in JointAngleCalculator.AnalysisJoints) DrawJoint(skeleton.Joints[jt]);
        }

        private void DrawBone(Skeleton skeleton, JointType a, JointType b)
        {
            var j1 = skeleton.Joints[a];
            var j2 = skeleton.Joints[b];
            if (j1.TrackingState == JointTrackingState.NotTracked ||
                j2.TrackingState == JointTrackingState.NotTracked) return;

            var p1 = MapToCanvas(j1.Position);
            var p2 = MapToCanvas(j2.Position);
            SkeletonCanvas.Children.Add(new Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 100, 181, 246)),
                StrokeThickness = 3
            });
        }

        private void DrawJoint(Joint joint)
        {
            if (joint.TrackingState == JointTrackingState.NotTracked) return;
            var p = MapToCanvas(joint.Position);
            var c = new Ellipse
            {
                Width = JointRadius * 2,
                Height = JointRadius * 2,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(100, 181, 246)),
                StrokeThickness = 2
            };
            Canvas.SetLeft(c, p.X - JointRadius);
            Canvas.SetTop(c, p.Y - JointRadius);
            SkeletonCanvas.Children.Add(c);
        }

        private Point MapToCanvas(SkeletonPoint pos)
        {
            double w = SkeletonCanvas.ActualWidth > 0 ? SkeletonCanvas.ActualWidth : 640;
            double h = SkeletonCanvas.ActualHeight > 0 ? SkeletonCanvas.ActualHeight : 480;
            return new Point((pos.X + 1.0) / 2.0 * w, (1.0 - (pos.Y + 1.0) / 2.0) * h);
        }

        // ---------------------------------------------------------------
        // Angle dictionary
        // ---------------------------------------------------------------
        private Dictionary<string, double> BuildAngleDictionary(Skeleton skeleton)
        {
            var j = skeleton.Joints;
            return new Dictionary<string, double>
            {
                ["LeftShoulder"] = JointAngleCalculator.CalculateAngle(
                    j[JointType.ShoulderCenter].Position, j[JointType.ShoulderLeft].Position, j[JointType.ElbowLeft].Position),
                ["RightShoulder"] = JointAngleCalculator.CalculateAngle(
                    j[JointType.ShoulderCenter].Position, j[JointType.ShoulderRight].Position, j[JointType.ElbowRight].Position),
                ["LeftKnee"] = JointAngleCalculator.CalculateAngle(
                    j[JointType.HipLeft].Position, j[JointType.KneeLeft].Position, j[JointType.AnkleLeft].Position),
                ["LeftHip"] = JointAngleCalculator.CalculateAngle(
                    j[JointType.ShoulderCenter].Position, j[JointType.HipCenter].Position, j[JointType.KneeLeft].Position),
            };
        }

        // ---------------------------------------------------------------
        // Button / countdown
        // ---------------------------------------------------------------
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_testRunning) StartCountdown();
            else AdvanceTest();
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
        // Finish
        // ---------------------------------------------------------------
        private void FinishAllTests()
        {
            ResultDifficulty = _romService.EvaluateDifficulty();

            MessageBox.Show(
                $"ROM Test complete!\n\nYour recommended difficulty: {ResultDifficulty}\n\nWe'll start your session with {ResultDifficulty} poses.",
                "Assessment Complete", MessageBoxButton.OK, MessageBoxImage.Information);

            Cleanup();
            DialogResult = true;
            Close();
        }

        private void Cleanup()
        {
            _countdownTimer?.Stop();
            _kinectManager.ColorFrameReady -= OnColorFrameReady;
            _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
            _kinectManager.BodyStatusChanged -= OnBodyStatusChanged;
        }

        protected override void OnClosed(EventArgs e)
        {
            Cleanup();
            base.OnClosed(e);
        }
    }
}