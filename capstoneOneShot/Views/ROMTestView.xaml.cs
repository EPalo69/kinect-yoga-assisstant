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
    // Note: BalanceHipDrop is in Kinect Y-units (not degrees); ideal ≈ 0 drop.
    internal static class IdealAngles
    {
        public static readonly Dictionary<string, (double Ideal, string Label)> Map =
            new Dictionary<string, (double, string)>
            {
                // ── Test 1: Overhead Star Reach ──────────────────────────────
                ["OverheadShoulder"] = (170, "Shoulder (L)"),
                ["OverheadElbow"]    = (170, "Elbow (L)"),
                ["OverheadWrist"]    = (170, "Wrist (L)"),
                ["SpineDeviation"]   = (0,   "Spine Align"),

                // ── Test 2: Lateral Arm Raise ────────────────────────────────
                ["LateralShoulder"]  = (90,  "Shoulder (L)"),
                ["LateralShoulderR"] = (90,  "Shoulder (R)"),
                ["LateralElbow"]     = (170, "Elbow (L)"),
                ["LateralElbowR"]    = (170, "Elbow (R)"),

                // ── Test 3: Wide Squat ───────────────────────────────────────
                ["SquatKnee"]        = (60,  "Knee (L)"),
                ["SquatKneeR"]       = (60,  "Knee (R)"),
                ["SquatHip"]         = (70,  "Hip (L)"),
                ["SquatHipR"]        = (70,  "Hip (R)"),

                // ── Test 4: Single-Leg Balance ───────────────────────────────
                ["BalanceHipDrop"]   = (0,   "Hip Drop"),
                ["BalanceKnee"]      = (170, "Stand Knee"),
                ["BalanceAnkle"]     = (90,  "Ankle Stab."),
            };
    }

    // ── Which joints are relevant for each test ──────────────────────────────
    internal static class TestJointSets
    {
        public static readonly Dictionary<string, string[]> ByTestName =
            new Dictionary<string, string[]>
            {
                ["Overhead Star Reach"] = new[] { "OverheadShoulder", "OverheadElbow",   "OverheadWrist"  },
                ["Lateral Arm Raise"]   = new[] { "LateralShoulder",  "LateralShoulderR", "LateralElbow", "LateralElbowR" },
                ["Wide Squat"]          = new[] { "SquatKnee",        "SquatKneeR",      "SquatHip",     "SquatHipR"     },
                ["Single-Leg Balance"]  = new[] { "BalanceHipDrop",   "BalanceKnee",     "BalanceAnkle"  },
            };

        public static string[] GetFor(string testName)
        {
            if (ByTestName.TryGetValue(testName, out var keys)) return keys;
            return new[] { "OverheadShoulder", "LateralShoulder", "SquatKnee", "BalanceHipDrop" };
        }
    }

    // ── ViewModel for each result card ────────────────────────────────────────
    public class ResultCardVM
    {
        public string TestName   { get; set; }
        public string Metric     { get; set; }
        public string Value      { get; set; }
        public string Rating     { get; set; }
        public string ValueColor { get; set; } = "#4DD0E1";
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
        private DispatcherTimer _bodyLostTimer;
        private DispatcherTimer _kinectCheckTimer;

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
            _kinectManager.VoiceCommandHeard  += OnVoiceCommandHeard;

            // Set up pause gesture (same as SessionView)
            _pauseService = new PauseGestureService(_kinectManager);
            _pauseService.PauseDetected += OnPauseTriggered;
            _pauseService.Enable(PauseGestureCanvas);
            Panel.SetZIndex(PauseGestureCanvas, 9999);

            _bodyLostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _bodyLostTimer.Tick += (s, e) =>
            {
                _bodyLostTimer.Stop();
                if (_currentBodyStatus == BodyDetectionStatus.NotDetected)
                {
                    PauseIcon.Text = "⚠";
                    TriggerPause("USER LOST", "We lost track of you. Step back into frame to resume.", true);
                }
            };

            _kinectCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _kinectCheckTimer.Tick += (s, e) =>
            {
                if (!_kinectManager.IsConnected)
                {
                    _kinectCheckTimer.Stop();
                    PauseIcon.Text = "⚠";
                    TriggerPause("KINECT DISCONNECTED", "Your Kinect sensor has been disconnected. Return to Main Menu.", false);
                }
            };
            _kinectCheckTimer.Start();

            LoadCurrentTest();
            Loaded += (s, e) => InitStatusPills();
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

            // Reset time bar to full visually before test starts
            Dispatcher.InvokeAsync(() =>
            {
                TimeBarFill.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, null);
                TimeBarFill.Width = (TimeBarFill.Parent as System.Windows.FrameworkElement)?.ActualWidth ?? ActualWidth;
            });

            // Build the joint angle rows for this test
            BuildJointRows(test.Name);

            // Show "ready" overlay using the pause panel
            PauseIcon.Text             = "🏁";
            ResumeButtonText.Text      = "Start Test";
            TriggerPause("READY TO START", $"Up next: {test.Name}\nReview the instructions and step into position.", true);
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
                if (status == BodyDetectionStatus.NotDetected)
                    _bodyLostTimer?.Start();
                else
                    _bodyLostTimer?.Stop();

                switch (status)
                {
                    case BodyDetectionStatus.Detected:
                        NoBodyOverlay.Visibility    = Visibility.Collapsed;
                        PartialBodyBanner.Visibility = Visibility.Collapsed;
                        SetTrackingStatus("Fully Tracked", "#4CAF50");
                        PillBodyDot.Fill   = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        PillBodyLabel.Text = "Body Detected";
                        PillBodyLabel.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        break;
                    case BodyDetectionStatus.PartialDetect:
                        NoBodyOverlay.Visibility    = Visibility.Collapsed;
                        PartialBodyBanner.Visibility = Visibility.Visible;
                        SetTrackingStatus("Partial Tracking", "#F59E0B");
                        PillBodyDot.Fill   = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        PillBodyLabel.Text = "Partial Body";
                        PillBodyLabel.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        break;
                    case BodyDetectionStatus.NotDetected:
                        NoBodyOverlay.Visibility    = Visibility.Visible;
                        PartialBodyBanner.Visibility = Visibility.Collapsed;
                        SetTrackingStatus("Not Detected", "#EF4444");
                        ClearAngleReadouts();
                        PillBodyDot.Fill   = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        PillBodyLabel.Text = "No Body Detected";
                        PillBodyLabel.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
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

            // Compute best for display — persist across frames via _bestByKey
            string bestKey = test.JointToMeasure + "_best";
            double best;
            if (_lastAngles.TryGetValue(bestKey, out double prevBest))
                best = test.TrackMinimum
                    ? Math.Min(prevBest, primaryAngle)
                    : Math.Max(prevBest, primaryAngle);
            else
                best = primaryAngle;

            // Store the best into the new frame dict so it persists across frames
            _lastAngles = angles;
            _lastAngles[bestKey] = best;

            bool isBalanceTest = test.JointToMeasure == "BalanceHipDrop";

            Dispatcher.Invoke(() =>
            {
                // Update joint rows
                UpdateJointRows(angles, test);

                // Update best readout — units depend on test type
                BestAngleReadout.Text = isBalanceTest
                    ? best.ToString("F3") + " m"
                    : best.ToString("F1") + "°";

                DrawSkeleton(skeleton);
            });
        }

        private void UpdateJointRows(Dictionary<string, double> angles, ROMTest test)
        {
            if (_angleRows == null) return;

            bool isBalanceTest = test.JointToMeasure == "BalanceHipDrop";

            foreach (var row in _angleRows)
            {
                if (!angles.TryGetValue(row.AngleKey, out double angle))
                {
                    row.UserAngle  = "--";
                    row.AngleColor = "#4DD0E1";
                    row.DotColor   = "#3D4A5A";
                    continue;
                }

                bool isPrimary = row.AngleKey == test.JointToMeasure;
                row.DotColor = isPrimary ? "#4DD0E1" : "#3D4A5A";

                // ── BalanceHipDrop: display as Y-units, not degrees ──
                if (row.AngleKey == "BalanceHipDrop")
                {
                    row.UserAngle = angle.ToString("F3") + " m";
                    if (angle <= 0.025)      row.AngleColor = "#4CAF50";  // green: stable
                    else if (angle <= 0.05)  row.AngleColor = "#FFB300";  // amber: mild sway
                    else                     row.AngleColor = "#EF4444";  // red: significant drop
                    continue;
                }

                // ── Standard joint angle ──
                row.UserAngle = angle.ToString("F1") + "°";

                if (IdealAngles.Map.TryGetValue(row.AngleKey, out var info))
                {
                    double diff = Math.Abs(angle - info.Ideal);
                    if (diff <= 15)       row.AngleColor = "#4CAF50";  // close to ideal
                    else if (diff <= 35)  row.AngleColor = "#FFB300";  // moderate
                    else                  row.AngleColor = "#4DD0E1";  // still recording
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
        // Builds a unified key→value map covering all 4 ROM tests.
        // BalanceHipDrop is the |Y| difference between hips (not a true angle)
        // stored under the same key so RecordFrame can track it.
        private Dictionary<string, double> BuildAngleDictionary(Skeleton skeleton)
        {
            var j = skeleton.Joints;

            // Shoulder-center → shoulder → elbow (flexion / abduction)
            double lShoulderAngle = JointAngleCalculator.CalculateAngle(
                j[JointType.ShoulderCenter].Position, j[JointType.ShoulderLeft].Position,  j[JointType.ElbowLeft].Position);
            double rShoulderAngle = JointAngleCalculator.CalculateAngle(
                j[JointType.ShoulderCenter].Position, j[JointType.ShoulderRight].Position, j[JointType.ElbowRight].Position);

            // Elbow angles
            double lElbowAngle = JointAngleCalculator.CalculateAngle(
                j[JointType.ShoulderLeft].Position,  j[JointType.ElbowLeft].Position,  j[JointType.WristLeft].Position);
            double rElbowAngle = JointAngleCalculator.CalculateAngle(
                j[JointType.ShoulderRight].Position, j[JointType.ElbowRight].Position, j[JointType.WristRight].Position);

            // Wrist angles
            double lWristAngle = JointAngleCalculator.CalculateAngle(
                j[JointType.ElbowLeft].Position,  j[JointType.WristLeft].Position,  j[JointType.HandLeft].Position);

            // Knee angles
            double lKneeAngle = JointAngleCalculator.CalculateAngle(
                j[JointType.HipLeft].Position,  j[JointType.KneeLeft].Position,  j[JointType.AnkleLeft].Position);
            double rKneeAngle = JointAngleCalculator.CalculateAngle(
                j[JointType.HipRight].Position, j[JointType.KneeRight].Position, j[JointType.AnkleRight].Position);

            // Hip angles (trunk → hip → knee)
            double lHipAngle = JointAngleCalculator.CalculateAngle(
                j[JointType.ShoulderCenter].Position, j[JointType.HipLeft].Position,  j[JointType.KneeLeft].Position);
            double rHipAngle = JointAngleCalculator.CalculateAngle(
                j[JointType.ShoulderCenter].Position, j[JointType.HipRight].Position, j[JointType.KneeRight].Position);

            // Ankle stability proxy (HipCenter → Knee → Ankle)
            double rAnkleAngle = JointAngleCalculator.CalculateAngle(
                j[JointType.KneeRight].Position, j[JointType.AnkleRight].Position, j[JointType.FootRight].Position);

            // Balance: hip drop = |Y difference between hips| (Kinect Y units)
            double hipDrop = Math.Abs(
                j[JointType.HipLeft].Position.Y - j[JointType.HipRight].Position.Y);

            return new Dictionary<string, double>
            {
                // ── Overhead Star Reach keys ──
                ["OverheadShoulder"] = lShoulderAngle,
                ["OverheadElbow"]    = lElbowAngle,
                ["OverheadWrist"]    = lWristAngle,

                // ── Lateral Arm Raise keys ──
                ["LateralShoulder"]  = lShoulderAngle,
                ["LateralShoulderR"] = rShoulderAngle,
                ["LateralElbow"]     = lElbowAngle,
                ["LateralElbowR"]    = rElbowAngle,

                // ── Wide Squat keys ──
                ["SquatKnee"]        = lKneeAngle,
                ["SquatKneeR"]       = rKneeAngle,
                ["SquatHip"]         = lHipAngle,
                ["SquatHipR"]        = rHipAngle,

                // ── Single-Leg Balance keys ──
                ["BalanceHipDrop"]   = hipDrop,
                ["BalanceKnee"]      = rKneeAngle,
                ["BalanceAnkle"]     = rAnkleAngle,
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

            // Animate the time bar: full width → 0 over the test duration
            Dispatcher.InvokeAsync(() =>
            {
                double trackW = (TimeBarFill.Parent as System.Windows.FrameworkElement)?.ActualWidth ?? ActualWidth;
                // Cancel any prior animation then set start value
                TimeBarFill.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, null);
                TimeBarFill.Width = trackW;

                var anim = new System.Windows.Media.Animation.DoubleAnimation(
                    trackW, 0,
                    new Duration(TimeSpan.FromSeconds(test.DurationSeconds)));
                TimeBarFill.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, anim);
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            _countdownTimer          = new DispatcherTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick    += CountdownTick;
            _countdownTimer.Start();
        }

        private void CountdownTick(object sender, EventArgs e)
        {
            _secondsRemaining--;
            CountdownText.Text     = _secondsRemaining > 0 ? _secondsRemaining.ToString() : "";
            CountdownSubLabel.Text = _secondsRemaining > 0 ? "SECONDS REMAINING" : "";

            if (_secondsRemaining <= 0)
            {
                _countdownTimer.Stop();
                // Snap bar to zero cleanly
                TimeBarFill.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, null);
                TimeBarFill.Width = 0;
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
        private void OnVoiceCommandHeard(string command)
        {
            if (command == "pause" && !_isPaused)
            {
                OnPauseTriggered(this, EventArgs.Empty);
            }
            else if (command == "resume" && _isPaused)
            {
                ResumeButton_Click(this, null);
            }
        }

        private void OnPauseTriggered(object sender, EventArgs e)
        {
            if (_isPaused)
            {
                ResumeTest();
            }
            else
            {
                PauseIcon.Text            = "⏸";
                ResumeButtonText.Text     = "Continue";
                TriggerPause("PAUSED", "Your test is paused. Take a moment before continuing.", true);
            }
        }

        private void TriggerPause(string title, string description, bool canResume)
        {
            _isPaused = true;
            _countdownTimer?.Stop();

            if (_testRunning)
            {
                // Freeze the time bar at its exact current width
                Dispatcher.Invoke(() =>
                {
                    double currentW = TimeBarFill.ActualWidth;
                    TimeBarFill.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, null);
                    TimeBarFill.Width = currentW;
                });
            }

            PauseTitleText.Text       = title;
            PauseDescriptionText.Text = description;

            ResumeButton.Visibility = canResume ? Visibility.Visible : Visibility.Collapsed;
            RedoButton.Visibility = canResume ? Visibility.Visible : Visibility.Collapsed;
            PauseGestureHint.Visibility = canResume ? Visibility.Visible : Visibility.Collapsed;

            PauseOverlay.Visibility   = Visibility.Visible;
            PauseOverlay.UpdateLayout();

            if (_pointerService != null)
            {
                _pointerService.Stop();
                _pointerService.ClearButtons();
            }

            _pointerService = new PointerSelectionService(PauseOverlayCursorCanvas);

            const double circ = 471;
            if (canResume)
            {
                _pointerService.RegisterButton(ResumeButton,     circ, () => Dispatcher.Invoke(ResumeTest));
                _pointerService.RegisterButton(RedoButton,       circ, () => Dispatcher.Invoke(RedoCurrentTest));
            }
            _pointerService.RegisterButton(EndSessionButton, circ, () => Dispatcher.Invoke(() => EndSessionButton_Click(null, null)));

            _pointerService.BringCursorToFront();
            _pointerService.Start();
            _pointerService.ResetPosition();

            double cx = PauseOverlayCursorCanvas.ActualWidth  / 2;
            double cy = PauseOverlayCursorCanvas.ActualHeight / 2;
            _pointerService.ProcessHandPosition(new Point(cx, cy), true);

            _kinectManager.SkeletonFrameReady -= OnPauseSkeletonFrame;
            _kinectManager.SkeletonFrameReady += OnPauseSkeletonFrame;
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
            {
                _countdownTimer?.Start();

                // Resume time bar animation
                Dispatcher.InvokeAsync(() =>
                {
                    double currentW = TimeBarFill.ActualWidth;
                    var anim = new System.Windows.Media.Animation.DoubleAnimation(
                        currentW, 0,
                        new Duration(TimeSpan.FromSeconds(_secondsRemaining)));
                    TimeBarFill.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, anim);
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
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
            ResultDifficulty       = _romService.EvaluateDifficulty();
            UserSession.ROMProfile = _romService.BuildProfile(ResultDifficulty);

            // Stop all active timers
            _countdownTimer?.Stop();
            _bodyLostTimer?.Stop();
            _kinectCheckTimer?.Stop();

            // Dismiss pause overlay if open
            PauseOverlay.Visibility = Visibility.Collapsed;

            // Reset time bar
            TimeBarFill.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, null);
            TimeBarFill.Width = 0;

            // Build result cards
            var cards = BuildResultCards();
            ResultCards.ItemsSource = cards;

            // Difficulty badge colour
            string diffColor;
            switch (ResultDifficulty)
            {
                case DifficultyLevel.Advanced:     diffColor = "#22C55E"; break;
                case DifficultyLevel.Intermediate: diffColor = "#F59E0B"; break;
                default:                           diffColor = "#4DD0E1"; break;
            }
            DifficultyLabel.Text       = ResultDifficulty.ToString();
            DifficultyLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(diffColor));
            DifficultyBadge.Background = new SolidColorBrush(
                Color.FromArgb(40,
                    ((Color)ColorConverter.ConvertFromString(diffColor)).R,
                    ((Color)ColorConverter.ConvertFromString(diffColor)).G,
                    ((Color)ColorConverter.ConvertFromString(diffColor)).B));
            DifficultyBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(diffColor));
            DifficultyBadge.BorderThickness = new Thickness(1);

            // Update ROM pill
            UpdateROMPill();

            // Wire Return button to pointer service
            if (_pointerService == null)
                _pointerService = new PointerSelectionService(PauseOverlayCursorCanvas);

            ResultsOverlay.Visibility = Visibility.Visible;
        }

        private List<ResultCardVM> BuildResultCards()
        {
            var profile = UserSession.ROMProfile;
            var cards   = new List<ResultCardVM>();

            // Test 1: Overhead Star Reach
            double overhead = profile.ShoulderFlexion;
            cards.Add(new ResultCardVM
            {
                TestName   = "Overhead Star Reach",
                Metric     = "Shoulder flexion (max angle)",
                Value      = overhead.ToString("F1") + "°",
                Rating     = overhead >= 155 ? "Excellent" : overhead >= 120 ? "Good" : "Needs Work",
                ValueColor = overhead >= 155 ? "#22C55E"  : overhead >= 120 ? "#F59E0B" : "#EF4444"
            });

            // Test 2: Lateral Arm Raise
            double lateral = profile.LateralShoulder;
            cards.Add(new ResultCardVM
            {
                TestName   = "Lateral Arm Raise",
                Metric     = "Shoulder abduction (max angle)",
                Value      = lateral.ToString("F1") + "°",
                Rating     = lateral >= 80 ? "Excellent" : lateral >= 55 ? "Good" : "Needs Work",
                ValueColor = lateral >= 80 ? "#22C55E"  : lateral >= 55 ? "#F59E0B" : "#EF4444"
            });

            // Test 3: Wide Squat
            double squat = profile.KneeFlexion;
            cards.Add(new ResultCardVM
            {
                TestName   = "Wide Squat",
                Metric     = "Knee flexion (min angle = deeper)",
                Value      = squat.ToString("F1") + "°",
                Rating     = squat <= 70 ? "Excellent" : squat <= 105 ? "Good" : "Needs Work",
                ValueColor = squat <= 70 ? "#22C55E"  : squat <= 105 ? "#F59E0B" : "#EF4444"
            });

            // Test 4: Single-Leg Balance
            double hipDrop = profile.HipFlexion;   // repurposed field
            cards.Add(new ResultCardVM
            {
                TestName   = "Single-Leg Balance",
                Metric     = "Hip drop (lower = more stable)",
                Value      = hipDrop.ToString("F3") + " m",
                Rating     = hipDrop <= 0.025 ? "Excellent" : hipDrop <= 0.05 ? "Good" : "Needs Work",
                ValueColor = hipDrop <= 0.025 ? "#22C55E"  : hipDrop <= 0.05 ? "#F59E0B" : "#EF4444"
            });

            return cards;
        }

        private void ReturnToMenu_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Cleanup();
            Application.Current.MainWindow.Show();
            Close();
        }

        private void Cleanup()
        {
            _countdownTimer?.Stop();
            _bodyLostTimer?.Stop();
            _kinectCheckTimer?.Stop();
            _kinectManager.ColorFrameReady   -= OnColorFrameReady;
            _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
            _kinectManager.BodyStatusChanged  -= OnBodyStatusChanged;
            _kinectManager.SkeletonFrameReady -= OnPauseSkeletonFrame;
            _kinectManager.VoiceCommandHeard  -= OnVoiceCommandHeard;

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

        // ── Status pill helpers ───────────────────────────────────────────────
        private void InitStatusPills()
        {
            bool kinectOk = _kinectManager.IsConnected;
            PillKinectDot.Fill   = new SolidColorBrush(kinectOk ? Color.FromRgb(34,197,94) : Color.FromRgb(239,68,68));
            PillKinectLabel.Text = kinectOk ? "Kinect Connected" : "Kinect Not Connected";
            PillKinectLabel.Foreground = new SolidColorBrush(kinectOk ? Color.FromRgb(34,197,94) : Color.FromRgb(156,163,175));

            // ROM starts as "Not Loaded" on this screen — it gets set to loaded
            // only after this test finishes and the profile is saved.
            UpdateROMPill();
        }

        private void UpdateROMPill()
        {
            if (UserSession.HasCompletedROM)
            {
                PillROMDot.Fill   = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                PillROMLabel.Text = "ROM Loaded";
                PillROMLabel.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
            else
            {
                PillROMDot.Fill   = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                PillROMLabel.Text = "ROM Not Loaded";
                PillROMLabel.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
            }
        }
    }
}