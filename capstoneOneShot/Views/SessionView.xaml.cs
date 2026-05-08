using capstoneOneShot.Models;
using capstoneOneShot.Services;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private int _totalSessionSeconds = 0;
        private DispatcherTimer _sessionTimer;
        private DispatcherTimer _holdTimer;
        private bool _currentFrameCorrect = false;

        private PauseGestureService _pauseService;
        private bool _isPaused = false;
        private PointerSelectionService _pointerService;
        private BodyDetectionStatus _currentBodyStatus = BodyDetectionStatus.NotDetected;
        private DispatcherTimer _bodyLostTimer;
        private DispatcherTimer _kinectCheckTimer;

        private const double JointRadius = 6;

        private List<double> _frameScores = new List<double>();
        private double _bestScore = 0;
        private PoseDefinition _currentPose => _currentPoseIndex < _poses.Count ? _poses[_currentPoseIndex] : null;
        private PoseDefinition _lastCompletedPose;
        private readonly TextToSpeechService _tts;
        private int _currentInstructionStep = 0;
        private bool _instructionStepSpeaking = false;

        private bool _poseEverCorrect = false;
        private string _activeInstruction = null;

        private ObservableCollection<InstructionItem> _instructionItems;
        private ObservableCollection<InstructionItem> _correctionItems;

        // ★ Map JointType to the string names used in rules
        private static readonly Dictionary<JointType, string[]> JointTypeToRuleNames
            = new Dictionary<JointType, string[]>
            {
                { JointType.ElbowLeft,    new[] { "LeftElbow"    } },
                { JointType.ElbowRight,   new[] { "RightElbow"   } },
                { JointType.ShoulderLeft, new[] { "LeftShoulder" } },
                { JointType.ShoulderRight,new[] { "RightShoulder"} },
                { JointType.KneeLeft,     new[] { "LeftKnee"     } },
                { JointType.KneeRight,    new[] { "RightKnee"    } },
                { JointType.HipLeft,      new[] { "LeftHip"      } },
                { JointType.HipRight,     new[] { "RightHip"     } },
                { JointType.WristLeft,    new[] { "LeftWrist"     } },
                { JointType.WristRight,   new[] { "RightWrist"   } },
            };

        public SessionView(KinectManager kinectManager, PoseDefinition pose)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _evaluator = new PoseEvaluator();
            _poses = new List<PoseDefinition> { pose };
            _tts = new TextToSpeechService();

            SetupUI(pose.Difficulty.ToString());
            SetupKinect();
            LoadPose(0);
            StartHoldTimer();
            StartSessionTimer();
        }

        private void SetupUI(string difficultyText)
        {
            // Info removed from UI
        }

        private void SetupKinect()
        {
            _kinectManager.ColorFrameReady   += OnColorFrameReady;
            _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;
            _kinectManager.BodyStatusChanged  += OnBodyStatusChanged;
            _kinectManager.VoiceCommandHeard  += OnVoiceCommandHeard;

            _pauseService = new PauseGestureService(_kinectManager);
            _pauseService.PauseDetected += OnPauseTriggered;
            _pauseService.Enable(PauseGestureCanvas);
            Panel.SetZIndex(PauseGestureCanvas, 9999);

            // Initialize status pills
            InitStatusPills();

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
        }
        private void UnhookKinect()
        {
            _kinectManager.ColorFrameReady -= OnColorFrameReady;
            _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
            _kinectManager.BodyStatusChanged -= OnBodyStatusChanged;
            _kinectManager.VoiceCommandHeard -= OnVoiceCommandHeard;

            if (_pauseService != null)
            {
                _pauseService.Disable();
                _pauseService.PauseDetected -= OnPauseTriggered;
            }

            _bodyLostTimer?.Stop();
            _kinectCheckTimer?.Stop();

            _pointerService?.Stop();
            _pointerService?.ClearButtons();
            _pointerService = null;
        }

        private void LoadPose(int index)
        {
            if (index >= _poses.Count) { EndSession(); return; }

            _evaluator.ResetHistory();

            // Reset TTS
            _tts.Reset();
            _poseEverCorrect = false;
            _activeInstruction = null;
            _currentInstructionStep = 0;
            _currentFrameCorrect = false;

            var pose = _poses[index];
            CurrentPoseLabel.Text = pose.Name;
            //PoseDescriptionLabel.Text = pose.Description;
            //ScoreLabel.Text = "0%";
            HoldTimerLabel.Text = "0s";
            FeedbackList.ItemsSource = null;
            AllGoodLabel.Visibility = Visibility.Collapsed;
            _holdSeconds = 0;

            // Build instruction items from pose rules
            BuildInstructionPanel(pose);

            LoadPoseImage(pose.ImageFileName);
        }

        private void BuildInstructionPanel(PoseDefinition pose)
        {
            _instructionItems = new ObservableCollection<InstructionItem>(
                pose.Instructions.Select((text, index) => new InstructionItem
                {
                    JointName = $"step_{index}",
                    Text = text,
                    Status = InstructionItemStatus.Pending
                })
            );

            _correctionItems = new ObservableCollection<InstructionItem>();

            InstructionsList.ItemsSource = _instructionItems;
            CorrectionsList.ItemsSource = _correctionItems;

            CorrectionsSection.Visibility = Visibility.Collapsed;
            GuidanceDivider.Visibility = Visibility.Collapsed;
            AllDoneBanner.Visibility = Visibility.Collapsed;
            CorrectionBanner.Visibility = Visibility.Collapsed;

            if (_instructionItems.Count > 0)
            {
                SetActiveInstruction(_instructionItems[0].JointName);
                CurrentInstructionLabel.Text = _instructionItems[0].Text;
            }
            else
            {
                CurrentInstructionLabel.Text = "";
            }
        }

        private void SetActiveInstruction(string jointName)
        {
            _activeInstruction = jointName;

            foreach (var item in _instructionItems)
            {
                if (item.JointName == jointName &&
                    item.Status != InstructionItemStatus.Completed)
                {
                    item.Status = InstructionItemStatus.Active;
                    // Update the HUD overlay to show only the current instruction
                    CurrentInstructionLabel.Text = item.Text;
                }
            }
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

            PoseReferenceImage.Visibility = Visibility.Collapsed;
            PoseImagePlaceholder.Visibility = Visibility.Visible;
        }

        private void StartHoldTimer()
        {
            _holdTimer = new DispatcherTimer();
            _holdTimer.Interval = TimeSpan.FromSeconds(1);
            _holdTimer.Tick += HoldTimer_Tick;
            _holdTimer.Start();
        }

        private void StartSessionTimer()
        {
            _sessionTimer = new DispatcherTimer();
            _sessionTimer.Interval = TimeSpan.FromSeconds(1);
            _sessionTimer.Tick += SessionTimer_Tick;
            _sessionTimer.Start();
        }

        private void HoldTimer_Tick(object sender, EventArgs e)
        {
            if (_currentFrameCorrect)
            {
                _holdSeconds++;
                HoldTimerLabel.Text = $"{_holdSeconds}s";

                int targetHold = _currentPose?.HoldSeconds > 0 ? _currentPose.HoldSeconds : 10;
                if (_holdSeconds >= targetHold)
                {
                    _holdTimer.Stop();
                    AdvancePose();
                }
            }
        }

        private void SessionTimer_Tick(object sender, EventArgs e)
        {
            _totalSessionSeconds++;
            int minutes = _totalSessionSeconds / 60;
            int seconds = _totalSessionSeconds % 60;
            SessionTimerLabel.Text = $"{minutes.ToString("D2")}:{seconds.ToString("D2")}";
        }

        private void AdvancePose()
        {
            _lastCompletedPose = _currentPose;
            _currentPoseIndex++;
            _holdSeconds = 0;
            LoadPose(_currentPoseIndex);
            
            if (_currentPoseIndex < _poses.Count)
            {
                _holdTimer?.Start();
            }
        }

        private void OnColorFrameReady(System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            Dispatcher.Invoke(() => CameraFeed.Source = bitmap);
        }

        private void OnBodyStatusChanged(BodyDetectionStatus status)
        {
            _currentBodyStatus = status;
            Dispatcher.Invoke(() =>
            {
                if (status == BodyDetectionStatus.NotDetected)
                    _bodyLostTimer?.Start();
                else
                    _bodyLostTimer?.Stop();

                switch (status)
                {
                    case BodyDetectionStatus.Detected:
                        NoBodyOverlay.Visibility     = Visibility.Collapsed;
                        PartialBodyBanner.Visibility = Visibility.Collapsed;
                        PillBodyDot.Fill   = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        PillBodyLabel.Text = "Body Detected";
                        PillBodyLabel.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        break;
                    case BodyDetectionStatus.PartialDetect:
                        NoBodyOverlay.Visibility     = Visibility.Collapsed;
                        PartialBodyBanner.Visibility = Visibility.Visible;
                        PillBodyDot.Fill   = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        PillBodyLabel.Text = "Partial Body";
                        PillBodyLabel.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        break;
                    case BodyDetectionStatus.NotDetected:
                        NoBodyOverlay.Visibility     = Visibility.Visible;
                        PartialBodyBanner.Visibility = Visibility.Collapsed;
                        FeedbackList.ItemsSource     = null;
                        AllGoodLabel.Visibility      = Visibility.Collapsed;
                        PillBodyDot.Fill   = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        PillBodyLabel.Text = "No Body Detected";
                        PillBodyLabel.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
                        break;
                }
            });
        }

        // ── Status pill helpers ────────────────────────────────────────────────────
        private void InitStatusPills()
        {
            bool kinectOk = _kinectManager.IsConnected;
            PillKinectDot.Fill   = new SolidColorBrush(kinectOk ? Color.FromRgb(34,197,94) : Color.FromRgb(239,68,68));
            PillKinectLabel.Text = kinectOk ? "Kinect Connected" : "Kinect Not Connected";
            PillKinectLabel.Foreground = new SolidColorBrush(kinectOk ? Color.FromRgb(34,197,94) : Color.FromRgb(156,163,175));

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

        private void OnSkeletonFrameReady(Skeleton[] skeletons)
        {
            if (skeletons == null || skeletons.Length == 0) return;
            if (_currentPose == null) return;

            var skeleton = skeletons[0];
            var angles = BuildAngleDictionary(skeleton);
            var result = _evaluator.Evaluate(_currentPose, angles);

            // Track scores for results screen
            _frameScores.Add(result.Score);
            if (result.Score > _bestScore) _bestScore = result.Score;
            _currentFrameCorrect = result.IsPoseCorrect;

            Dispatcher.Invoke(() =>
            {
                UpdateScoreDisplay(result);
                DrawSkeleton(skeleton, result);
                UpdateTTS(result);
            });
        }

        private void UpdateTTS(EvaluationResult result)
        {
            if (_isPaused) return;
            if (_currentPose == null) return;

            var currentFeedback = result.Feedback ?? new List<string>();
            var failingFeedback = new HashSet<string>(currentFeedback);

            if (!_poseEverCorrect)
            {
                // ════════════════════════════════════════════════════
                // PHASE 1 — Walk through Instructions sequentially
                // Each step advances only after TTS finishes speaking it.
                // Corrections from Rules override if a joint fails.
                // ════════════════════════════════════════════════════

                // ★ Correction override: if any joint is failing, speak
                //   the correction and pause instruction progression
                if (currentFeedback.Count > 0)
                {
                    // Mirror failing joints as corrections in the UI
                    foreach (var item in _instructionItems)
                        item.Status = item.Status == InstructionItemStatus.Completed
                            ? InstructionItemStatus.Completed
                            : item.Status; // keep current

                    // Show correction in HUD overlay
                    CorrectionBanner.Visibility = Visibility.Visible;
                    CorrectionLabel.Text = currentFeedback[0];

                    _tts.Speak(string.Empty, currentFeedback[0]);
                    return;
                }

                // Hide correction when no failures
                CorrectionBanner.Visibility = Visibility.Collapsed;

                // ★ No joint failures — advance through instruction steps
                if (_currentInstructionStep < _instructionItems.Count)
                {
                    var activeItem = _instructionItems[_currentInstructionStep];

                    if (activeItem.Status != InstructionItemStatus.Completed)
                    {
                        activeItem.Status = InstructionItemStatus.Active;
                        CurrentInstructionLabel.Text = activeItem.Text;
                        _tts.Speak(activeItem.Text, string.Empty);
                    }

                    // ★ Advance when TTS queue is empty (step was spoken)
                    if (!_tts.IsSpeaking && activeItem.Status == InstructionItemStatus.Active)
                    {
                        activeItem.Status = InstructionItemStatus.Completed;
                        _currentInstructionStep++;

                        if (_currentInstructionStep < _instructionItems.Count)
                            SetActiveInstruction(_instructionItems[_currentInstructionStep].JointName);
                    }
                }
                else
                {
                    // ★ All steps spoken — enter Phase 2
                    _poseEverCorrect = true;
                    _activeInstruction = null;

                    foreach (var item in _instructionItems)
                        item.Status = InstructionItemStatus.Completed;

                    CorrectionsSection.Visibility = Visibility.Collapsed;
                    GuidanceDivider.Visibility = Visibility.Collapsed;
                    CorrectionBanner.Visibility = Visibility.Collapsed;
                    CurrentInstructionLabel.Text = "Great! Now hold the pose.";
                    AllDoneBanner.Visibility = Visibility.Visible;

                    _tts.Reset();
                    _tts.Speak("Great! Now hold the pose.", string.Empty);
                }
            }
            else
            {
                // ════════════════════════════════════════════════════
                // PHASE 2 — Corrections only, instructions stay green
                // ════════════════════════════════════════════════════

                _correctionItems.Clear();

                if (currentFeedback.Count == 0)
                {
                    CorrectionsSection.Visibility = Visibility.Collapsed;
                    GuidanceDivider.Visibility = Visibility.Collapsed;
                    CorrectionBanner.Visibility = Visibility.Collapsed;
                    CurrentInstructionLabel.Text = "Hold the pose steady.";
                    AllDoneBanner.Visibility = Visibility.Visible;

                    foreach (var item in _instructionItems)
                        item.Status = InstructionItemStatus.Completed;

                    _tts.Speak("Good, keep holding.", string.Empty);
                    return;
                }

                foreach (var feedback in currentFeedback)
                {
                    _correctionItems.Add(new InstructionItem
                    {
                        Text = feedback,
                        Status = InstructionItemStatus.Regressed
                    });

                    var matching = _instructionItems.FirstOrDefault(i => i.Text == feedback);
                    if (matching != null)
                        matching.Status = InstructionItemStatus.Regressed;
                }

                foreach (var item in _instructionItems.Where(i => !failingFeedback.Contains(i.Text)))
                    item.Status = InstructionItemStatus.Completed;

                // Show correction in HUD overlay
                CorrectionBanner.Visibility = Visibility.Visible;
                CorrectionLabel.Text = currentFeedback[0];
                CurrentInstructionLabel.Text = "Correction needed";
                AllDoneBanner.Visibility = Visibility.Collapsed;

                _tts.Speak(string.Empty, currentFeedback[0]);
            }
        }

        private void UpdateScoreDisplay(EvaluationResult result)
        {
            //ScoreLabel.Text = result.ToString() + "%";
            //ScoreLabel.Foreground = result.IsPoseCorrect
            //    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
            //    : new SolidColorBrush(Color.FromRgb(255, 107, 107));

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
            // UI elements removed
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
                ["LeftWrist"] = JointAngleCalculator.CalculateAngle(j[JointType.ElbowLeft].Position, j[JointType.WristLeft].Position, j[JointType.HandLeft].Position),
                ["RightWrist"] = JointAngleCalculator.CalculateAngle(j[JointType.ElbowRight].Position, j[JointType.WristRight].Position, j[JointType.HandRight].Position),
            };
        }

        private void DrawSkeleton(Skeleton skeleton, EvaluationResult result)
        {
            SkeletonCanvas.Children.Clear();

            // ★ Build a fast lookup of failing joint names
            var failingJoints = new HashSet<string>(
                result.JointDeviations
                      .Where(j => !j.IsPassing)
                      .Select(j => j.JointName)
            );

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

            foreach (var (s, e) in bones) DrawBone(skeleton, s, e, failingJoints);
            foreach (var jt in JointAngleCalculator.AnalysisJoints)
                DrawJoint(skeleton.Joints[jt], failingJoints);
        }

        private bool IsJointFailing(JointType jt, HashSet<string> failingJoints)
        {
            if (!JointTypeToRuleNames.ContainsKey(jt)) return false;
            return JointTypeToRuleNames[jt].Any(name => failingJoints.Contains(name));
        }


        private void DrawBone(Skeleton skeleton, JointType a, JointType b,
                              HashSet<string> failingJoints)
        {
            var j1 = skeleton.Joints[a];
            var j2 = skeleton.Joints[b];

            if (j1.TrackingState == JointTrackingState.NotTracked ||
                j2.TrackingState == JointTrackingState.NotTracked) return;

            bool failing = IsJointFailing(a, failingJoints) ||
                           IsJointFailing(b, failingJoints);

            var p1 = MapToCanvas(j1.Position);
            var p2 = MapToCanvas(j2.Position);

            SkeletonCanvas.Children.Add(new Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = failing
                                    ? new SolidColorBrush(Color.FromArgb(200, 255, 107, 107)) // ★ red
                                    : new SolidColorBrush(Color.FromArgb(200, 100, 181, 246)),
                StrokeThickness = 3
            });
        }

        private void DrawJoint(Joint joint, HashSet<string> failingJoints)
        {
            if (joint.TrackingState == JointTrackingState.NotTracked) return;

            bool failing = IsJointFailing(joint.JointType, failingJoints);

            var p = MapToCanvas(joint.Position);
            var c = new Ellipse
            {
                Width = JointRadius * 2,
                Height = JointRadius * 2,
                Fill = failing
                                    ? new SolidColorBrush(Color.FromRgb(255, 107, 107)) // ★ red
                                    : Brushes.White,
                Stroke = failing
                                    ? new SolidColorBrush(Color.FromRgb(255, 60, 60))   // ★ red ring
                                    : new SolidColorBrush(Color.FromRgb(100, 181, 246)),
                StrokeThickness = 2
            };

            Canvas.SetLeft(c, p.X - JointRadius);
            Canvas.SetTop(c, p.Y - JointRadius);
            SkeletonCanvas.Children.Add(c);
        }

        private Point MapToCanvas(SkeletonPoint pos)
        {
            double canvasW = SkeletonCanvas.ActualWidth > 0 ? SkeletonCanvas.ActualWidth : 640;
            double canvasH = SkeletonCanvas.ActualHeight > 0 ? SkeletonCanvas.ActualHeight : 480;

            const double sourceAspect = 640.0 / 480.0; // Kinect color stream native aspect
            double canvasAspect = canvasW / canvasH;

            double renderW, renderH, offsetX, offsetY;

            if (canvasAspect > sourceAspect)
            {
                // Canvas is wider — pillarboxed (black bars on left/right)
                renderH = canvasH;
                renderW = canvasH * sourceAspect;
                offsetX = (canvasW - renderW) / 2.0;
                offsetY = 0;
            }
            else
            {
                // Canvas is taller — letterboxed (black bars on top/bottom)
                renderW = canvasW;
                renderH = canvasW / sourceAspect;
                offsetX = 0;
                offsetY = (canvasH - renderH) / 2.0;
            }

            double x = (pos.X + 1.0) / 2.0 * renderW + offsetX;
            double y = (1.0 - (pos.Y + 1.0) / 2.0) * renderH + offsetY;

            return new Point(x, y);
        }

        // ── Pause and Navigation Logic ────────────────────────────────────

        private void OnVoiceCommandHeard(string command)
        {
            if (command == "pause" && !_isPaused)
            {
                OnPauseTriggered(this, EventArgs.Empty);
            }
            else if (command == "resume" && _isPaused)
            {
                ResumeSession();
            }
        }

        private void OnPauseTriggered(object sender, EventArgs e)
        {
            if (_isPaused)
            {
                ResumeSession();
            }
            else
            {
                PauseIcon.Text = "⏸";
                ResumeButtonText.Text = "Continue";
                TriggerPause("PAUSED", "Your session is paused.", true);
            }
        }

        private void TriggerPause(string title, string description, bool canResume)
        {
            _isPaused = true;
            _holdTimer?.Stop();
            _sessionTimer?.Stop();
            _tts.Reset();

            PauseTitleText.Text = title;
            PauseDescriptionText.Text = description;

            ResumeButton.Visibility = canResume ? Visibility.Visible : Visibility.Collapsed;
            SelectionButton.Visibility = canResume ? Visibility.Visible : Visibility.Collapsed;
            PauseGestureHint.Visibility = canResume ? Visibility.Visible : Visibility.Collapsed;

            PauseOverlay.Visibility = Visibility.Visible;
            PauseOverlay.UpdateLayout();

            if (_pointerService != null)
            {
                _pointerService.Stop();
                _pointerService.ClearButtons();
            }

            // ★ Initialize pointer service on the pause cursor canvas
            _pointerService = new PointerSelectionService(PauseOverlayCursorCanvas);

            // ★ Register the pause buttons — circumference = π * diameter = π * 150 ≈ 471
            const double circ = 471;
            if (canResume)
            {
                _pointerService.RegisterButton(ResumeButton, circ, () => Dispatcher.Invoke(ResumeSession));
                _pointerService.RegisterButton(SelectionButton, circ, () => Dispatcher.Invoke(() => SelectionButton_Click(null, null)));
            }
            _pointerService.RegisterButton(EndSessionButton, circ, () => Dispatcher.Invoke(() => EndSessionButton_Click(null, null)));

            _pointerService.BringCursorToFront();
            _pointerService.Start();
            _pointerService.ResetPosition();

            double cx = PauseOverlayCursorCanvas.ActualWidth / 2;
            double cy = PauseOverlayCursorCanvas.ActualHeight / 2;
            _pointerService.ProcessHandPosition(new Point(cx, cy), true);

            // ★ Feed hand position to pointer service via skeleton frames
            _kinectManager.SkeletonFrameReady -= OnPauseSkeletonFrame;
            _kinectManager.SkeletonFrameReady += OnPauseSkeletonFrame;
        }

        private void OnPauseSkeletonFrame(Skeleton[] skeletons)
        {
            if (_pointerService == null) return;
            if (skeletons == null || skeletons.Length == 0) return;

            var skeleton = skeletons.FirstOrDefault(s =>
                s.TrackingState == SkeletonTrackingState.Tracked);

            if (skeleton == null)
            {
                Dispatcher.Invoke(() => _pointerService.ProcessHandPosition(new Point(), false));
                return;
            }

            // Use right hand — swap to WristRight if HandRight is unreliable
            var hand = skeleton.Joints[JointType.HandRight];
            bool tracked = hand.TrackingState == JointTrackingState.Tracked;

            Dispatcher.Invoke(() =>
            {
                if (!tracked) { _pointerService.ProcessHandPosition(new Point(), false); return; }

                // ★ Get position in camera canvas space
                Point cameraPoint = MapToCanvas(hand.Position);

                // ★ Remap from SkeletonCanvas space to PauseOverlayCursorCanvas space
                Point overlayPoint = SkeletonCanvas.TransformToVisual(PauseOverlayCursorCanvas)
                                                   .Transform(cameraPoint);

                _pointerService.ProcessHandPosition(overlayPoint, true);
            });
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            ResumeSession();
        }

        private void ResumeSession()
        {
            _isPaused = false;
            PauseOverlay.Visibility = Visibility.Collapsed;

            _kinectManager.SkeletonFrameReady -= OnPauseSkeletonFrame;
            _pointerService?.Stop();
            _pointerService?.ClearButtons();
            _pointerService = null;
            _sessionTimer?.Start();

            if (_currentBodyStatus == BodyDetectionStatus.Detected)
                _holdTimer?.Start();
        }

        private void SelectionButton_Click(object sender, RoutedEventArgs e)
        {
            var sel = new PoseSelectionView(_kinectManager);
            sel.Show();
            UnhookKinect();
            Close();
        }
        private void PauseButton_Click(object sender, MouseButtonEventArgs e)
        {
            OnPauseTriggered(this, EventArgs.Empty);
        }

        private void EndSessionButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.Show();
            UnhookKinect();
            Close();
        }

        private void EndSession()
        {
            _holdTimer?.Stop();
            _sessionTimer?.Stop();
            _tts.Reset();
            UnhookKinect();
            OpenResultsScreen();
            Close();
        }

        private void OpenResultsScreen()
        {
            double avg = _frameScores.Count > 0 ? _frameScores.Average() : 0;
            int lastValidIndex = Math.Min(_currentPoseIndex, _poses.Count - 1);
            var poseToReport = _poses[lastValidIndex];

            var results = new SessionResultsView(_kinectManager, _lastCompletedPose ?? _poses[0]);
            results.SetResults(avg, _bestScore, _holdSeconds, _totalSessionSeconds);
            results.Show();
        }

        protected override void OnClosed(EventArgs e)
        {
            _holdTimer?.Stop();
            _sessionTimer?.Stop();
            UnhookKinect();
            base.OnClosed(e);
        }



    }
}