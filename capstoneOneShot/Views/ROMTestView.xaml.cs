using capstoneOneShot.Models;
using capstoneOneShot.Services;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace capstoneOneShot.Views
{
    // ── ViewModel for each row in the joint angle HUD ──────────────────────
    public class JointAngleRowVM : INotifyPropertyChanged
    {
        private string _userAngle = "--°";
        private string _angleColor = "#4DD0E1";

        public string Label { get; set; }
        public string IdealAngle { get; set; }
        public string DotColor { get; set; } = "#4DD0E1";

        public string UserAngle
        {
            get => _userAngle;
            set { _userAngle = value; OnChanged(nameof(UserAngle)); }
        }

        public string AngleColor
        {
            get => _angleColor;
            set { _angleColor = value; OnChanged(nameof(AngleColor)); }
        }

        // Key used to look up this joint in the angle dictionary
        public string AngleKey { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Static ideal human ROM averages (degrees) ───────────────────────────
    // Sources: AAOS / clinical reference ranges
    internal static class IdealAngles
    {
        // Displayed angles are the *joint angle* as calculated by JointAngleCalculator
        // (angle at the joint between the two segments).
        // Shoulder raise (arm-overhead): ~170°  (fully raised = near 180)
        // Lateral raise:                 ~80-90°
        // Knee bend (squat):             ~50-90° at peak flex
        // Hip hinge (forward fold):      ~45-60° (angle at hip decreases)
        public static readonly Dictionary<string, (double Ideal, string Label)> Map =
            new Dictionary<string, (double, string)>
            {
                ["LeftShoulder"]  = (170, "L Shoulder"),
                ["RightShoulder"] = (170, "R Shoulder"),
                ["LeftElbow"]     = (170, "L Elbow"),
                ["RightElbow"]    = (170, "R Elbow"),
                ["LeftKnee"]      = (60,  "L Knee"),
                ["RightKnee"]     = (60,  "R Knee"),
                ["LeftHip"]       = (60,  "L Hip"),
                ["RightHip"]      = (60,  "R Hip"),
                ["LeftWrist"]     = (170, "L Wrist"),
                ["RightWrist"]    = (170, "R Wrist"),
            };
    }

    // ── Which joints are relevant for each test ──────────────────────────────
    internal static class TestJointSets
    {
        public static readonly Dictionary<string, string[]> ByTestName =
            new Dictionary<string, string[]>
            {
                ["Shoulder Raise"]   = new[] { "LeftShoulder", "RightShoulder", "LeftElbow", "RightElbow" },
                ["Knee Bend"]        = new[] { "LeftKnee", "RightKnee", "LeftHip", "RightHip" },
                ["Hip Hinge"]        = new[] { "LeftHip", "RightHip", "LeftKnee", "RightKnee" },
                ["Lateral Arm Raise"]= new[] { "LeftShoulder", "RightShoulder", "LeftElbow", "RightElbow" },
            };

        public static string[] GetFor(string testName)
        {
            if (ByTestName.TryGetValue(testName, out var keys)) return keys;
            // Fallback: show all
            return new[] { "LeftShoulder", "RightShoulder", "LeftKnee", "RightKnee", "LeftHip", "RightHip" };
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    public partial class ROMTestView : Window
    {
        private readonly ROMTestService  _romService;
        private readonly KinectManager   _kinectManager;

        private PauseGestureService      _pauseService;
        private PointerSelectionService  _pointerService;
        private bool                     _isPaused = false;

        private DispatcherTimer _countdownTimer;
        private int  _secondsRemaining;
        private bool _testRunning       = false;
        private bool _waitingForDetection = false;
        private BodyDetectionStatus _currentBodyStatus = BodyDetectionStatus.NotDetected;

        private const double JointRadius = 6;

        private ObservableCollection<JointAngleRowVM> _angleRows;
        private string[] _currentJointKeys = Array.Empty<string>();

        public DifficultyLevel ResultDifficulty { get; private set; } = DifficultyLevel.Beginner;

        // ── Angle dictionary from the last skeleton frame ────────────────────
        private Dictionary<string, double> _lastAngles = new Dictionary<string, double>();

        public ROMTestView(KinectManager kinectManager)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _romService    = new ROMTestService();

            _kinectManager.ColorFrameReady   += OnColorFrameReady;
            _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;
            _kinectManager.BodyStatusChanged  += OnBodyStatusChanged;

            // Set up pause gesture (same as SessionView)
            _pauseService = new PauseGestureService(_kinectManager);
            _pauseService.PauseDetected += OnPauseTriggered;
            _pauseService.Enable(PauseGestureCanvas);
            Panel.SetZIndex(PauseGestureCanvas, 9999);

            LoadCurrentTest();
        }

        // ── Load current test ────────────────────────────────────────────────
        private void LoadCurrentTest()
        {
            if (_romService.IsComplete) { FinishAllTests(); return; }

            var test = _romService.GetCurrentTest();

            TestNameLabel.Text    = test.Name;
            TestNumberBadge.Text  = $" #{_romService.CurrentTestIndex + 1}";
            InstructionLabel.Text = test.Instruction;
            InstructionSubLabel.Text = "Hold at your maximum range and keep still";
            ProgressLabel.Text    = $"Test {_romService.CurrentTestIndex + 1} of {_romService.Tests.Count}";

            UpdateProgressPips(_romService.CurrentTestIndex + 1);
            BestAngleReadout.Text = "--°";

            _testRunning       = false;
            _waitingForDetection = false;
            CountdownText.Text = "";
            CountdownSubLabel.Text = "";

            // Build the joint angle rows for this test
            BuildJointRows(test.Name);

            // Show "ready" overlay using the pause panel
            PauseIcon.Text             = "🏁";
            PauseTitleText.Text        = "READY TO START";
            PauseDescriptionText.Text  = $"Up next: {test.Name}\nReview the instructions and step into position.";
            ResumeButtonText.Text      = "Start Test";
            PauseOverlay.Visibility    = Visibility.Visible;
        }

        private void BuildJointRows(string testName)
        {
            _currentJointKeys = TestJointSets.GetFor(testName);
            _angleRows = new ObservableCollection<JointAngleRowVM>();

            foreach (var key in _currentJointKeys)
            {
                if (!IdealAngles.Map.TryGetValue(key, out var info)) continue;
                _angleRows.Add(new JointAngleRowVM
                {
                    Label      = info.Label,
                    AngleKey   = key,
                    IdealAngle = info.Ideal.ToString("F0") + "°",
                    DotColor   = "#4DD0E1",
                    UserAngle  = "--°",
                    AngleColor = "#4DD0E1"
                });
            }

            JointAngleRows.ItemsSource = _angleRows;
        }

        private void UpdateProgressPips(int activeIndex)
        {
            if (ProgressPips == null) return;
            ProgressPips.Items.Clear();
            for (int i = 1; i <= _romService.Tests.Count; i++)
            {
                ProgressPips.Items.Add(new TextBlock
                {
                    Text     = "●",
                    FontSize = 8,
                    Margin   = new Thickness(4, 0, 4, 0),
                    Foreground = i == activeIndex
                        ? new SolidColorBrush(Color.FromRgb(77, 208, 225))
                        : new SolidColorBrush(Color.FromRgb(107, 122, 141))
                });
            }
        }

        // ── Camera feed ──────────────────────────────────────────────────────
        private void OnColorFrameReady(System.Windows.Media.Imaging.BitmapSource bitmap)
            => Dispatcher.Invoke(() => CameraFeed.Source = bitmap);

        // ── Body detection ───────────────────────────────────────────────────
        private void OnBodyStatusChanged(BodyDetectionStatus status)
        {
            Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case BodyDetectionStatus.Detected:
                        NoBodyOverlay.Visibility    = Visibility.Collapsed;
                        PartialBodyBanner.Visibility = Visibility.Collapsed;
                        SetTrackingStatus("Fully Tracked", "#4CAF50");
                        break;
                    case BodyDetectionStatus.PartialDetect:
                        NoBodyOverlay.Visibility    = Visibility.Collapsed;
                        PartialBodyBanner.Visibility = Visibility.Visible;
                        SetTrackingStatus("Partial Tracking", "#F59E0B");
                        break;
                    case BodyDetectionStatus.NotDetected:
                        NoBodyOverlay.Visibility    = Visibility.Visible;
                        PartialBodyBanner.Visibility = Visibility.Collapsed;
                        SetTrackingStatus("Not Detected", "#EF4444");
                        ClearAngleReadouts();
                        break;
                }
                _currentBodyStatus = status;
                CheckAutoStart();
            });
        }

        private void CheckAutoStart()
        {
            if (_waitingForDetection &&
                _currentBodyStatus == BodyDetectionStatus.Detected &&
                !_testRunning)
            {
                _waitingForDetection = false;
                StartCountdown();
            }
        }

        private void SetTrackingStatus(string text, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            TrackingStatusLabel.Text       = text;
            TrackingStatusLabel.Foreground = new SolidColorBrush(color);
            TrackingDot.Fill               = new SolidColorBrush(color);
        }

        private void ClearAngleReadouts()
        {
            BestAngleReadout.Text = "--°";
            if (_angleRows == null) return;
            foreach (var row in _angleRows)
            {
                row.UserAngle  = "--°";
                row.AngleColor = "#4DD0E1";
            }
        }

        // ── Skeleton frame ───────────────────────────────────────────────────
        private void OnSkeletonFrameReady(Skeleton[] skeletons)
        {
            if (skeletons == null || skeletons.Length == 0) return;

            var skeleton = skeletons[0];
            var angles   = BuildAngleDictionary(skeleton);

            if (_testRunning && _secondsRemaining > 0)
                _romService.RecordFrame(angles);

            var test = _romService.GetCurrentTest();
            if (test == null) return;

            angles.TryGetValue(test.JointToMeasure, out double primaryAngle);

            // Also compute best for display
            double best = primaryAngle;
            if (_lastAngles.TryGetValue(test.JointToMeasure + "_best", out double prevBest))
                best = test.TrackMinimum
                    ? Math.Min(prevBest, primaryAngle)
                    : Math.Max(prevBest, primaryAngle);

            _lastAngles[test.JointToMeasure + "_best"] = best;
            _lastAngles = angles; // keep for HUD

            Dispatcher.Invoke(() =>
            {
                // Update joint rows
                UpdateJointRows(angles, test);

                // Update best readout (shown for the primary joint)
                BestAngleReadout.Text = best.ToString("F1") + "°";

                DrawSkeleton(skeleton);
            });
        }

        private void UpdateJointRows(Dictionary<string, double> angles, ROMTest test)
        {
            if (_angleRows == null) return;

            foreach (var row in _angleRows)
            {
                if (!angles.TryGetValue(row.AngleKey, out double angle))
                {
                    row.UserAngle  = "--°";
                    row.AngleColor = "#4DD0E1";
                    row.DotColor   = "#3D4A5A";
                    continue;
                }

                row.UserAngle = angle.ToString("F1") + "°";

                // Colour the row: compare to ideal
                if (IdealAngles.Map.TryGetValue(row.AngleKey, out var info))
                {
                    double diff   = Math.Abs(angle - info.Ideal);
                    bool isPrimary = row.AngleKey == test.JointToMeasure;

                    // Highlight primary joint brighter
                    row.DotColor = isPrimary ? "#4DD0E1" : "#3D4A5A";

                    // Colour-code closeness to ideal
                    if (diff <= 15)
                        row.AngleColor = "#4CAF50";   // green: close to ideal
                    else if (diff <= 35)
                        row.AngleColor = "#FFB300";   // amber: moderate
                    else
                        row.AngleColor = "#4DD0E1";   // cyan: still recording
                }
            }
        }

        // ── Skeleton drawing ─────────────────────────────────────────────────
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
                X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
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
                Width  = JointRadius * 2,
                Height = JointRadius * 2,
                Fill   = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(100, 181, 246)),
                StrokeThickness = 2
            };
            Canvas.SetLeft(c, p.X - JointRadius);
            Canvas.SetTop(c,  p.Y - JointRadius);
            SkeletonCanvas.Children.Add(c);
        }

        private Point MapToCanvas(SkeletonPoint pos)
        {
            double canvasW = SkeletonCanvas.ActualWidth  > 0 ? SkeletonCanvas.ActualWidth  : 640;
            double canvasH = SkeletonCanvas.ActualHeight > 0 ? SkeletonCanvas.ActualHeight : 480;

            const double sourceAspect = 640.0 / 480.0;
            double canvasAspect = canvasW / canvasH;

            double renderW, renderH, offsetX, offsetY;
            if (canvasAspect > sourceAspect)
            {
                renderH = canvasH; renderW = canvasH * sourceAspect;
                offsetX = (canvasW - renderW) / 2.0; offsetY = 0;
            }
            else
            {
                renderW = canvasW; renderH = canvasW / sourceAspect;
                offsetX = 0; offsetY = (canvasH - renderH) / 2.0;
            }

            return new Point(
                (pos.X + 1.0) / 2.0 * renderW + offsetX,
                (1.0 - (pos.Y + 1.0) / 2.0) * renderH + offsetY);
        }

        // ── Angle dictionary ─────────────────────────────────────────────────
        private Dictionary<string, double> BuildAngleDictionary(Skeleton skeleton)
        {
            var j = skeleton.Joints;
            return new Dictionary<string, double>
            {
                ["LeftShoulder"]  = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderCenter].Position, j[JointType.ShoulderLeft].Position,  j[JointType.ElbowLeft].Position),
                ["RightShoulder"] = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderCenter].Position, j[JointType.ShoulderRight].Position, j[JointType.ElbowRight].Position),
                ["LeftElbow"]     = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderLeft].Position,   j[JointType.ElbowLeft].Position,     j[JointType.WristLeft].Position),
                ["RightElbow"]    = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderRight].Position,  j[JointType.ElbowRight].Position,    j[JointType.WristRight].Position),
                ["LeftKnee"]      = JointAngleCalculator.CalculateAngle(j[JointType.HipLeft].Position,        j[JointType.KneeLeft].Position,      j[JointType.AnkleLeft].Position),
                ["RightKnee"]     = JointAngleCalculator.CalculateAngle(j[JointType.HipRight].Position,       j[JointType.KneeRight].Position,     j[JointType.AnkleRight].Position),
                ["LeftHip"]       = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderCenter].Position, j[JointType.HipLeft].Position,       j[JointType.KneeLeft].Position),
                ["RightHip"]      = JointAngleCalculator.CalculateAngle(j[JointType.ShoulderCenter].Position, j[JointType.HipRight].Position,      j[JointType.KneeRight].Position),
                ["LeftWrist"]     = JointAngleCalculator.CalculateAngle(j[JointType.ElbowLeft].Position,      j[JointType.WristLeft].Position,     j[JointType.HandLeft].Position),
                ["RightWrist"]    = JointAngleCalculator.CalculateAngle(j[JointType.ElbowRight].Position,     j[JointType.WristRight].Position,    j[JointType.HandRight].Position),
            };
        }

        // ── Countdown ────────────────────────────────────────────────────────
        private void StartCountdown()
        {
            _testRunning = true;
            var test = _romService.GetCurrentTest();
            _secondsRemaining = test.DurationSeconds;
            CountdownText.Text     = _secondsRemaining.ToString();
            CountdownSubLabel.Text = "SECONDS REMAINING";

            _countdownTimer          = new DispatcherTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick    += CountdownTick;
            _countdownTimer.Start();
        }

        private void CountdownTick(object sender, EventArgs e)
        {
            _secondsRemaining--;
            CountdownText.Text = _secondsRemaining > 0 ? _secondsRemaining.ToString() : "";
            CountdownSubLabel.Text = _secondsRemaining > 0 ? "SECONDS REMAINING" : "";

            if (_secondsRemaining <= 0)
            {
                _countdownTimer.Stop();
                AdvanceTest();
            }
        }

        private void AdvanceTest()
        {
            _romService.AdvanceToNextTest();
            // Reset best tracking for the new test
            _lastAngles.Clear();
            LoadCurrentTest();
        }

        // ── Pause gesture (identical pattern to SessionView) ─────────────────
        private void OnPauseTriggered(object sender, EventArgs e)
        {
            if (_isPaused)
            {
                ResumeTest();
            }
            else
            {
                _isPaused = true;
                _countdownTimer?.Stop();

                PauseIcon.Text            = "⏸";
                PauseTitleText.Text       = "PAUSED";
                PauseDescriptionText.Text = "Your test is paused. Take a moment before continuing.";
                ResumeButtonText.Text     = "Continue";
                PauseOverlay.Visibility   = Visibility.Visible;
                PauseOverlay.UpdateLayout();

                _pointerService = new PointerSelectionService(PauseOverlayCursorCanvas);

                const double circ = 471;
                _pointerService.RegisterButton(ResumeButton,     circ, () => Dispatcher.Invoke(ResumeTest));
                _pointerService.RegisterButton(RedoButton,       circ, () => Dispatcher.Invoke(RedoCurrentTest));
                _pointerService.RegisterButton(EndSessionButton, circ, () => Dispatcher.Invoke(() => EndSessionButton_Click(null, null)));

                _pointerService.BringCursorToFront();
                _pointerService.Start();
                _pointerService.ResetPosition();

                double cx = PauseOverlayCursorCanvas.ActualWidth  / 2;
                double cy = PauseOverlayCursorCanvas.ActualHeight / 2;
                _pointerService.ProcessHandPosition(new Point(cx, cy), true);

                _kinectManager.SkeletonFrameReady += OnPauseSkeletonFrame;
            }
        }

        private void OnPauseSkeletonFrame(Skeleton[] skeletons)
        {
            if (_pointerService == null) return;
            if (skeletons == null || skeletons.Length == 0) return;

            var skeleton = skeletons.FirstOrDefault(s => s.TrackingState == SkeletonTrackingState.Tracked);

            if (skeleton == null)
            {
                Dispatcher.Invoke(() => _pointerService.ProcessHandPosition(new Point(), false));
                return;
            }

            var hand    = skeleton.Joints[JointType.HandRight];
            bool tracked = hand.TrackingState == JointTrackingState.Tracked;

            Dispatcher.Invoke(() =>
            {
                if (!tracked) { _pointerService.ProcessHandPosition(new Point(), false); return; }
                Point cameraPoint  = MapToCanvas(hand.Position);
                Point overlayPoint = SkeletonCanvas.TransformToVisual(PauseOverlayCursorCanvas)
                                                   .Transform(cameraPoint);
                _pointerService.ProcessHandPosition(overlayPoint, true);
            });
        }

        private void ResumeTest()
        {
            _isPaused = false;
            PauseOverlay.Visibility = Visibility.Collapsed;

            _kinectManager.SkeletonFrameReady -= OnPauseSkeletonFrame;
            _pointerService?.Stop();
            _pointerService?.ClearButtons();
            _pointerService = null;

            // Re-start countdown only if test was already running
            if (_testRunning && _secondsRemaining > 0)
                _countdownTimer?.Start();
        }

        // ── Button handlers ──────────────────────────────────────────────────
        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            PauseOverlay.Visibility = Visibility.Collapsed;

            _kinectManager.SkeletonFrameReady -= OnPauseSkeletonFrame;
            _pointerService?.Stop();
            _pointerService?.ClearButtons();
            _pointerService = null;
            _isPaused = false;

            if (!_testRunning)
            {
                // "Start Test" path: wait for body detection then begin countdown
                _waitingForDetection = true;
                CheckAutoStart();
            }
            else
            {
                // "Continue" path: resume the countdown
                if (_secondsRemaining > 0)
                    _countdownTimer?.Start();
            }
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = false;
            _kinectManager.SkeletonFrameReady -= OnPauseSkeletonFrame;
            _pointerService?.Stop();
            _pointerService?.ClearButtons();
            _pointerService = null;

            RedoCurrentTest();
        }

        private void RedoCurrentTest()
        {
            _countdownTimer?.Stop();
            _romService.Reset();
            _lastAngles.Clear();
            LoadCurrentTest();
        }

        private void EndSessionButton_Click(object sender, RoutedEventArgs e)
        {
            Cleanup();
            Application.Current.MainWindow.Show();
            Close();
        }

        // ── Finish all tests ─────────────────────────────────────────────────
        private void FinishAllTests()
        {
            ResultDifficulty     = _romService.EvaluateDifficulty();
            UserSession.ROMProfile = _romService.BuildProfile(ResultDifficulty);

            MessageBox.Show(
                "Your ROM results have been recorded.\n\nWe will filter poses based on your flexibility.",
                "Assessment Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Cleanup();
            Application.Current.MainWindow.Show();
            Close();
        }

        private void Cleanup()
        {
            _countdownTimer?.Stop();
            _kinectManager.ColorFrameReady   -= OnColorFrameReady;
            _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
            _kinectManager.BodyStatusChanged  -= OnBodyStatusChanged;
            _kinectManager.SkeletonFrameReady -= OnPauseSkeletonFrame;

            if (_pauseService != null)
            {
                _pauseService.Disable();
                _pauseService.PauseDetected -= OnPauseTriggered;
            }

            _pointerService?.Stop();
            _pointerService?.ClearButtons();
            _pointerService = null;
        }

        protected override void OnClosed(EventArgs e)
        {
            Cleanup();
            base.OnClosed(e);
        }
    }
}