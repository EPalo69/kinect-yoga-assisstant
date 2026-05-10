using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using capstoneOneShot.Models;
using capstoneOneShot.Services;

namespace capstoneOneShot.Views
{
    /// <summary>
    /// Interaction logic for PoseDetailView.xaml
    /// </summary>
    public partial class PoseDetailView : Window
    {
        private readonly KinectManager _kinectManager;
        private readonly PoseDefinition _pose;
        private PointerSelectionService _pointerService;

        // ── Auto-proceed / hover-boost timer ─────────────────────────────────
        private DispatcherTimer _proceedTimer;
        private double _proceedProgress  = 0.0;
        private bool   _hoveredByMouse   = false;
        private bool   _hoveredByKinect  = false;
        private bool   _proceedFired     = false;

        /// <summary>Seconds to auto-fill the arc with no interaction.</summary>
        private const double AUTO_DURATION   = 120.0;
        /// <summary>Seconds to fill the arc when the hand/mouse hovers (matches PointerSelectionService).</summary>
        private const double HOVER_DURATION  = 3;
        /// <summary>π × diameter (150px button).</summary>
        private const double CIRCUMFERENCE   = 471.0;
        /// <summary>Hit-test radius for Kinect hand against the Proceed button.</summary>
        private const double KINECT_RADIUS   = 80.0;
        private const int    FPS             = 30;

        // Small helper for numbered instruction items
        private class InstructionItem
        {
            public string Number { get; set; }
            public string Text   { get; set; }
        }

        private readonly bool _isLibraryMode;

        public PoseDetailView(KinectManager kinectManager, PoseDefinition pose, bool isLibraryMode = false)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _pose          = pose;
            _isLibraryMode = isLibraryMode;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PopulateUI();

            // ── Pointer service — cursor + Kinect tracking only ───────────────
            // We do NOT register Btn_Proceed with the service; we manage its arc
            // and fire logic ourselves so we can blend auto + hover rates.
            _pointerService = new PointerSelectionService(MenuCanvas);
            _pointerService.Start();

            if (_isLibraryMode)
            {
                Btn_Proceed.Visibility = Visibility.Collapsed;
                Btn_Back.Visibility = Visibility.Visible;
                FooterHint.Visibility = Visibility.Collapsed;

                _pointerService.RegisterButton(Btn_Back, Math.PI * 150, () => BackButton_Click(null, null));
                Btn_Back.MouseEnter += (s, ev) => _pointerService.SetHover(Btn_Back);
                Btn_Back.MouseLeave += (s, ev) => _pointerService.ClearHover();
                Btn_Back.MouseLeftButtonUp += (s, ev) => { _pointerService.ClearHover(); BackButton_Click(null, null); };
            }
            else
            {
                // Mouse hover wires up the hover-boost flag
                Btn_Proceed.MouseEnter += (s, ev) => { _hoveredByMouse = true;  SetGlow(true); };
                Btn_Proceed.MouseLeave += (s, ev) => { _hoveredByMouse = false; SetGlow(false); };

                // Direct click still works immediately
                Btn_Proceed.MouseLeftButtonUp += (s, ev) => FireProceed();

                // ── Start the auto-proceed countdown timer ────────────────────────
                _proceedTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.0 / FPS)
                };
                _proceedTimer.Tick += OnProceedTick;
                _proceedTimer.Start();
            }

            _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;
            _kinectManager.BodyStatusChanged  += OnBodyStatusChanged;
            _pointerService.BringCursorToFront();

            InitStatusPills();
        }

        // ── Arc countdown tick ───────────────────────────────────────────────
        private void OnProceedTick(object sender, EventArgs e)
        {
            if (_proceedFired) return;

            bool hovered = _hoveredByMouse || _hoveredByKinect;

            double rate = hovered
                ? (1.0 / FPS) / HOVER_DURATION
                : (1.0 / FPS) / AUTO_DURATION;

            _proceedProgress = Math.Min(1.0, _proceedProgress + rate);

            // Drive the arc
            double filled = CIRCUMFERENCE * _proceedProgress;
            Progress_Proceed.StrokeDashArray =
                new DoubleCollection(new[] { filled, CIRCUMFERENCE - filled });

            // Drive the glow opacity proportionally (max 0.55)
            SetGlowOpacity(_proceedProgress * 0.55);

            if (_proceedProgress >= 1.0)
                FireProceed();
        }

        private void FireProceed()
        {
            if (_proceedFired) return;
            _proceedFired = true;
            _proceedTimer?.Stop();
            ProceedButton_Click(null, null);
        }

        // ── Glow helpers ────────────────────────────────────────────────────
        private void SetGlow(bool on)
        {
            // When hovering, snap glow higher; on leave it falls back to arc-driven value
            if (!on)
                SetGlowOpacity(_proceedProgress * 0.55);
            else
                SetGlowOpacity(0.55);
        }

        private void SetGlowOpacity(double opacity)
        {
            Glow_Proceed.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(150)));
        }

        // ── Populate all UI elements from PoseDefinition ─────────────────────
        private void PopulateUI()
        {
            // Pose image
            if (!string.IsNullOrEmpty(_pose.ImageFileName))
            {
                try
                {
                    string uri = "pack://application:,,,/Assets/Poses/" + _pose.ImageFileName;
                    PoseImage.Source = new BitmapImage(new Uri(uri, UriKind.Absolute));
                }
                catch { /* image not critical — silently skip */ }
            }

            // Hold-time badge
            int mins = _pose.HoldSeconds / 60;
            int secs = _pose.HoldSeconds % 60;
            HoldTimeLabel.Text = mins > 0
                ? $"Hold for {mins.ToString()}m {secs.ToString("D2")}s"
                : $"Hold for {secs.ToString()}s";

            // Pose name
            PoseNameLabel.Text = _pose.Name ?? "Pose";

            // Difficulty badge colour
            switch (_pose.Difficulty)
            {
                case DifficultyLevel.Beginner:
                    DifficultyBadge.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                    DifficultyLabel.Text       = "Beginner";
                    DifficultyLabel.Foreground  = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                    break;
                case DifficultyLevel.Intermediate:
                    DifficultyBadge.Background = new SolidColorBrush(Color.FromRgb(92, 59, 6));
                    DifficultyLabel.Text       = "Intermediate";
                    DifficultyLabel.Foreground  = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                    break;
                case DifficultyLevel.Advanced:
                    DifficultyBadge.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
                    DifficultyLabel.Text       = "Advanced";
                    DifficultyLabel.Foreground  = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                    break;
            }

            // Brief info (tagline)
            BriefInfoLabel.Text = _pose.BriefInfo ?? _pose.Description ?? string.Empty;

            // Full description (only shown if different from BriefInfo)
            bool hasDistinctDesc = !string.IsNullOrWhiteSpace(_pose.Description)
                                   && _pose.Description != _pose.BriefInfo;
            DescriptionLabel.Text = hasDistinctDesc ? _pose.Description : string.Empty;
            if (string.IsNullOrWhiteSpace(DescriptionLabel.Text))
                DescriptionLabel.Visibility = Visibility.Collapsed;

            // Numbered step list from Instructions
            var items = new List<InstructionItem>();
            var src = (_pose.Instructions != null && _pose.Instructions.Count > 0)
                      ? _pose.Instructions : null;

            if (src != null)
            {
                for (int i = 0; i < src.Count; i++)
                    items.Add(new InstructionItem
                    {
                        Number = (i + 1).ToString(),
                        Text   = src[i]
                    });
            }

            InstructionsList.ItemsSource = items;
        }

        // ── Navigation ───────────────────────────────────────────────────────
        private void ProceedButton_Click(object sender, MouseButtonEventArgs e)
        {
            var session = new SessionView(_kinectManager, _pose);
            session.Show();
            Close();
        }

        private void BackButton_Click(object sender, MouseButtonEventArgs e)
        {
            var library = new PoseSelectionView(_kinectManager, true);
            library.Show();
            Close();
        }

        // ── Kinect skeleton handler ──────────────────────────────────────────
        private void OnSkeletonFrameReady(Microsoft.Kinect.Skeleton[] skeletons)
        {
            if (skeletons == null || skeletons.Length == 0) return;
            var skeleton  = skeletons[0];
            var leftHand  = skeleton.Joints[Microsoft.Kinect.JointType.HandLeft];
            var rightHand = skeleton.Joints[Microsoft.Kinect.JointType.HandRight];
            var active    = leftHand.Position.Y > rightHand.Position.Y ? leftHand : rightHand;

            Dispatcher.Invoke(() =>
            {
                bool tracked = active.TrackingState != Microsoft.Kinect.JointTrackingState.NotTracked;
                var pt = tracked
                    ? MapToCanvas(active.Position, MenuCanvas.ActualWidth, MenuCanvas.ActualHeight)
                    : new Point(0, 0);

                _pointerService.ProcessHandPosition(pt, tracked);

                // Manual hit-test against Proceed button for hover-boost
                bool overProceed = false;
                if (!_isLibraryMode && tracked && Btn_Proceed.IsLoaded && Btn_Proceed.IsVisible)
                {
                    try
                    {
                        Point center = new Point(Btn_Proceed.ActualWidth / 2.0,
                                                 Btn_Proceed.ActualHeight / 2.0);
                        Point mapped = Btn_Proceed.TransformToVisual(MenuCanvas).Transform(center);
                        double dx   = pt.X - mapped.X;
                        double dy   = pt.Y - mapped.Y;
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        double radius = Math.Max(KINECT_RADIUS,
                            Math.Max(Btn_Proceed.ActualWidth, Btn_Proceed.ActualHeight) / 2.0);
                        overProceed = dist <= radius;
                    }
                    catch { }
                }

                bool wasHovered = _hoveredByKinect;
                _hoveredByKinect = overProceed;

                // Trigger glow transition only on state change
                if (!_isLibraryMode && _hoveredByKinect != wasHovered)
                    SetGlow(_hoveredByMouse || _hoveredByKinect);
            });
        }

        private Point MapToCanvas(Microsoft.Kinect.SkeletonPoint pos, double w, double h)
        {
            const double srcAspect = 640.0 / 480.0;
            double canvasAspect = w / h;
            double rW, rH, oX, oY;

            if (canvasAspect > srcAspect)
            {
                rH = h; rW = h * srcAspect;
                oX = (w - rW) / 2.0; oY = 0;
            }
            else
            {
                rW = w; rH = w / srcAspect;
                oX = 0; oY = (h - rH) / 2.0;
            }

            double x = (pos.X + 1.0) / 2.0 * rW + oX;
            double y = (1.0 - (pos.Y + 1.0) / 2.0) * rH + oY;
            return new Point(x, y);
        }

        protected override void OnClosed(EventArgs e)
        {
            _proceedTimer?.Stop();
            _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
            _kinectManager.BodyStatusChanged  -= OnBodyStatusChanged;
            _pointerService?.Stop();
            base.OnClosed(e);
        }

        // ── Status pill helpers ─────────────────────────────────────────────────
        private void InitStatusPills()
        {
            bool kinectOk = _kinectManager.IsConnected;
            PillKinectDot.Fill   = new SolidColorBrush(kinectOk ? Color.FromRgb(34,197,94) : Color.FromRgb(239,68,68));
            PillKinectLabel.Text = kinectOk ? "Kinect Connected" : "Kinect Not Connected";
            PillKinectLabel.Foreground = new SolidColorBrush(kinectOk ? Color.FromRgb(34,197,94) : Color.FromRgb(156,163,175));
            UpdateROMPill();
        }

        private void OnBodyStatusChanged(BodyDetectionStatus status)
        {
            Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case BodyDetectionStatus.Detected:
                        PillBodyDot.Fill   = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        PillBodyLabel.Text = "Body Detected";
                        PillBodyLabel.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        break;
                    case BodyDetectionStatus.PartialDetect:
                        PillBodyDot.Fill   = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        PillBodyLabel.Text = "Partial Body";
                        PillBodyLabel.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        break;
                    case BodyDetectionStatus.NotDetected:
                        PillBodyDot.Fill   = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        PillBodyLabel.Text = "No Body Detected";
                        PillBodyLabel.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
                        break;
                }
            });
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
