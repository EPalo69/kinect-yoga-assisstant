using capstoneOneShot.Models;
using capstoneOneShot.Services;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace capstoneOneShot.Views
{
    public partial class SessionView : Window
    {
        private readonly KinectManager _kinectManager;
        private readonly PoseEvaluator _evaluator;
        private readonly List<PoseDefinition> _poses;

        private int _currentPoseIndex = 0;
        private int _holdSeconds = 0;
        private DispatcherTimer _holdTimer;

        private const double JointRadius = 6;

        private List<double> _frameScores = new List<double>();
        private double _bestScore = 0;
        private PoseDefinition _currentPose => _poses[_currentPoseIndex];


        public SessionView(KinectManager kinectManager, DifficultyLevel difficulty)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _evaluator = new PoseEvaluator();
            _poses = PoseLibrary.GetPosesByDifficulty(difficulty);

            SetupUI(difficulty.ToString());
            SetupKinect();
            LoadPose(0);
            StartHoldTimer();
        }

        public SessionView(KinectManager kinectManager, PoseDefinition pose)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _evaluator = new PoseEvaluator();
            _poses = new List<PoseDefinition> { pose };

            SetupUI(pose.Difficulty.ToString());
            SetupKinect();
            LoadPose(0);
            StartHoldTimer();
        }

        private void SetupUI(string difficultyText)
        {
            DifficultyLabel.Text = difficultyText;
        }

        private void SetupKinect()
        {
            _kinectManager.ColorFrameReady += OnColorFrameReady;
            _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;
            _kinectManager.BodyStatusChanged += OnBodyStatusChanged;
        }
        private void UnhookKinect()
        {
            _kinectManager.ColorFrameReady -= OnColorFrameReady;
            _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
            _kinectManager.BodyStatusChanged -= OnBodyStatusChanged;
        }

        private void LoadPose(int index)
        {
            if (index >= _poses.Count) { EndSession(); return; }

            _evaluator.ResetHistory();

            var pose = _poses[index];
            CurrentPoseLabel.Text = pose.Name;
            PoseDescriptionLabel.Text = pose.Description;
            PoseCountLabel.Text = $"Pose {index + 1} of {_poses.Count}";
            ScoreLabel.Text = "0%";
            HoldTimerLabel.Text = "0s";
            FeedbackList.ItemsSource = null;
            AllGoodLabel.Visibility = Visibility.Collapsed;
            _holdSeconds = 0;

            LoadPoseImage(pose.ImageFileName);
        }

        /// <summary>
        /// Loads the reference image for the current pose from Assets/Poses/.
        /// The filename comes directly from <see cref="PoseDefinition.ImageFileName"/> — no name derivation.
        /// </summary>
        private void LoadPoseImage(string imageFileName)
        {
            if (!string.IsNullOrEmpty(imageFileName))
            {
                var path = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Assets", "Poses", imageFileName);

                if (System.IO.File.Exists(path))
                {
                    PoseReferenceImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(path));
                    PoseReferenceImage.Visibility = Visibility.Visible;
                    PoseImagePlaceholder.Visibility = Visibility.Collapsed;
                    return;
                }
            }

            // Fall through: no filename set or file not found — show placeholder
            PoseReferenceImage.Visibility = Visibility.Collapsed;
            PoseImagePlaceholder.Visibility = Visibility.Visible;
        }

        private void StartHoldTimer()
        {
            _holdTimer = new DispatcherTimer();
            _holdTimer.Interval = TimeSpan.FromSeconds(1);
            _holdTimer.Tick += (s, e) => { _holdSeconds++; HoldTimerLabel.Text = $"{_holdSeconds}s"; };
            _holdTimer.Start();
        }

        private void OnColorFrameReady(System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            Dispatcher.Invoke(() => CameraFeed.Source = bitmap);
        }

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
                        ScoreLabel.Text = "0%";
                        FeedbackList.ItemsSource = null;
                        AllGoodLabel.Visibility = Visibility.Collapsed;
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

        private void OnSkeletonFrameReady(Skeleton[] skeletons)
        {
            if (skeletons == null || skeletons.Length == 0) return;

            var skeleton = skeletons[0];
            var angles = BuildAngleDictionary(skeleton);
            var result = _evaluator.Evaluate(_currentPose, angles);

            // Track scores for results screen
            _frameScores.Add(result.Score);
            if (result.Score > _bestScore) _bestScore = result.Score;

            Dispatcher.Invoke(() =>
            {
                UpdateScoreDisplay(result);
                UpdateAngleReadouts(angles);
                DrawSkeleton(skeleton);
            });
        }

        private void UpdateScoreDisplay(EvaluationResult result)
        {
            ScoreLabel.Text = result.ToString() + "%";
            ScoreLabel.Foreground = result.IsPoseCorrect
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                : new SolidColorBrush(Color.FromRgb(255, 107, 107));

            if (result.Feedback.Count == 0)
            {
                FeedbackList.ItemsSource = null;
                AllGoodLabel.Visibility = Visibility.Visible;
            }
            else
            {
                FeedbackList.ItemsSource = result.Feedback;
                AllGoodLabel.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateAngleReadouts(Dictionary<string, double> angles)
        {
            if (angles.TryGetValue("LeftKnee", out double lk)) LeftKneeAngle.Text = lk.ToString("F0") + "%";
            if (angles.TryGetValue("RightKnee", out double rk)) RightKneeAngle.Text = rk.ToString("F0") + "%";
            if (angles.TryGetValue("LeftShoulder", out double ls)) LeftShoulderAngle.Text = ls.ToString("F0") + "%";
            if (angles.TryGetValue("RightShoulder", out double rs)) RightShoulderAngle.Text = rs.ToString("F0") + "%";
        }

        private Dictionary<string, double> BuildAngleDictionary(Skeleton skeleton)
        {
            var j = skeleton.Joints;
            return new Dictionary<string, double>
            {
                ["LeftShoulder"] = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderCenter].Position, j[JointType.ShoulderLeft].Position, j[JointType.ElbowLeft].Position),
                ["RightShoulder"] = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderCenter].Position, j[JointType.ShoulderRight].Position, j[JointType.ElbowRight].Position),
                ["LeftElbow"] = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderLeft].Position, j[JointType.ElbowLeft].Position, j[JointType.WristLeft].Position),
                ["RightElbow"] = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderRight].Position, j[JointType.ElbowRight].Position, j[JointType.WristRight].Position),
                ["LeftKnee"] = JointAngleCalculator.CalculateAngle(j[JointType.HipLeft].Position, j[JointType.KneeLeft].Position, j[JointType.AnkleLeft].Position),
                ["RightKnee"] = JointAngleCalculator.CalculateAngle(j[JointType.HipRight].Position, j[JointType.KneeRight].Position, j[JointType.AnkleRight].Position),
                ["LeftHip"] = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderCenter].Position, j[JointType.HipLeft].Position, j[JointType.KneeLeft].Position),
                ["RightHip"] = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderCenter].Position, j[JointType.HipRight].Position, j[JointType.KneeRight].Position),
            };
        }

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
            foreach (var (s, e) in bones) DrawBone(skeleton, s, e);
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

        private void NextPoseButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPoseIndex++;
            _holdSeconds = 0;
            LoadPose(_currentPoseIndex);
        }

        private void EndSessionButton_Click(object sender, RoutedEventArgs e) => EndSession();

        private void EndSession()
        {
            _holdTimer?.Stop();
            UnhookKinect();
            OpenResultsScreen();
            Close();
        }

        private void OpenResultsScreen()
        {
            double avg = _frameScores.Count > 0
                          ? _frameScores.Average()
                          : 0;

            var results = new SessionResultsView(_kinectManager, _currentPose);
            results.SetResults(avg, _bestScore, _holdSeconds);
            results.Show();
        }

        protected override void OnClosed(EventArgs e)
        {
            _holdTimer?.Stop();
            UnhookKinect();
            base.OnClosed(e);
        }
    }
}