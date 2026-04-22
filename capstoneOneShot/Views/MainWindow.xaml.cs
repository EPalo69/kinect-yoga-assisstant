using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Kinect;
using capstoneOneShot.Models;
using capstoneOneShot.Services;

namespace capstoneOneShot.Views
{
    public partial class MainWindow : Window
    {
        private KinectManager _kinectManager;

        private const double HoverRadius = 80;
        private const double HoldSeconds = 1.5;
        private const int FrameRate = 30;
        private bool _isFiring = false;

        private Grid _hoveredButton = null;
        private double _hoverProgress = 0;
        private DispatcherTimer _hoverTimer;

        // No more _buttonCenters — Grid handles layout
        private Dictionary<Grid, double> _buttonCircumferences = new Dictionary<Grid, double>();

        private const double AvatarOpacity = 0.28;
        private const double BoneThickness = 2.0;
        private const double JointDotRadius = 5.0;
        private static readonly Color BoneColor = Color.FromArgb(180, 200, 220, 255);
        private static readonly Color JointColor = Color.FromArgb(220, 255, 255, 255);
        private static readonly Color ActiveHandColor = Color.FromArgb(255, 77, 208, 225);

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Set circumferences based on fixed button size in XAML (150px)
            double circ = Math.PI * 150;
            foreach (var btn in new[] { Btn_StartSession, Btn_ROMTest,
                                         Btn_PoseLibrary, Btn_Settings, Btn_Exit })
                _buttonCircumferences[btn] = circ;

            SetupMouseAndKeyboard();
            StartKinect();
        }

        private void SetupMouseAndKeyboard()
        {
            foreach (var btn in new[] { Btn_StartSession, Btn_ROMTest,
                                         Btn_PoseLibrary, Btn_Settings, Btn_Exit })
            {
                var b = btn;
                b.MouseEnter += (s, e) => UpdateHover(b);
                b.MouseLeave += (s, e) => UpdateHover(null);
                b.MouseLeftButtonUp += (s, e) => { _hoverProgress = 0; FireButton(b); };
            }

            KeyDown += (s, e) =>
            {
                switch (e.Key)
                {
                    case System.Windows.Input.Key.D1:
                    case System.Windows.Input.Key.NumPad1: FireButton(Btn_StartSession); break;
                    case System.Windows.Input.Key.D2:
                    case System.Windows.Input.Key.NumPad2: FireButton(Btn_ROMTest); break;
                    case System.Windows.Input.Key.D3:
                    case System.Windows.Input.Key.NumPad3: FireButton(Btn_PoseLibrary); break;
                    case System.Windows.Input.Key.D4:
                    case System.Windows.Input.Key.NumPad4: FireButton(Btn_Settings); break;
                    case System.Windows.Input.Key.Escape: FireButton(Btn_Exit); break;
                }
            };
        }

        private void StartKinect()
        {
            _kinectManager = new KinectManager();
            bool connected = _kinectManager.Initialize();

            if (connected)
            {
                KinectStatusDot.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                KinectStatusLabel.Text = "Kinect Connected";
                KinectStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;
                _kinectManager.BodyStatusChanged += OnBodyStatusChanged;
            }
            else
            {
                KinectStatusLabel.Text = "Kinect Not Connected";
            }

            _hoverTimer = new DispatcherTimer();
            _hoverTimer.Interval = TimeSpan.FromSeconds(1.0 / FrameRate);
            _hoverTimer.Tick += OnHoverTick;
            _hoverTimer.Start();
        }

        private void OnBodyStatusChanged(BodyDetectionStatus status)
        {
            Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case BodyDetectionStatus.Detected:
                        BodyStatusDot.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        BodyStatusLabel.Text = "Body Detected";
                        BodyStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        break;
                    case BodyDetectionStatus.PartialDetect:
                        BodyStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        BodyStatusLabel.Text = "Partial Body";
                        BodyStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        break;
                    case BodyDetectionStatus.NotDetected:
                        BodyStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        BodyStatusLabel.Text = "No Body Detected";
                        BodyStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
                        AvatarCanvas.Children.Clear();
                        break;
                }
            });
        }

        private void OnSkeletonFrameReady(Skeleton[] skeletons)
        {
            if (skeletons == null || skeletons.Length == 0) return;
            var skeleton = skeletons[0];

            var leftHand = skeleton.Joints[JointType.HandLeft];
            var rightHand = skeleton.Joints[JointType.HandRight];
            Joint activeHand = leftHand.Position.Y > rightHand.Position.Y
                               ? leftHand : rightHand;

            Dispatcher.Invoke(() =>
            {
                double cw = ActualWidth;
                double ch = ActualHeight;

                DrawAvatar(skeleton, cw, ch, activeHand.JointType);

                if (activeHand.TrackingState == JointTrackingState.NotTracked)
                {
                    HandCursor.Visibility = Visibility.Collapsed;
                    UpdateHover(null);
                    return;
                }

                // Map hand to MenuGrid coordinate space
                double hx = (activeHand.Position.X + 1.0) / 2.0 * MenuGrid.ActualWidth;
                double hy = (1.0 - (activeHand.Position.Y + 1.0) / 2.0) * MenuGrid.ActualHeight;

                // Move hand cursor using Margin (Grid child, not Canvas)
                var gridPt = this.TranslatePoint(new Point(
                    (activeHand.Position.X + 1.0) / 2.0 * cw,
                    (1.0 - (activeHand.Position.Y + 1.0) / 2.0) * ch), MenuGrid);

                HandCursor.Margin = new Thickness(
                    gridPt.X - HandCursor.Width / 2,
                    gridPt.Y - HandCursor.Height / 2,
                    0, 0);
                HandCursor.Visibility = Visibility.Visible;

                // Hit-test using TranslatePoint — no _buttonCenters needed
                Grid nearest = null;
                foreach (var btn in new[] { Btn_StartSession, Btn_ROMTest,
                                             Btn_PoseLibrary, Btn_Settings, Btn_Exit })
                {
                    var center = btn.TranslatePoint(
                        new Point(btn.ActualWidth / 2, btn.ActualHeight / 2), MenuGrid);
                    double dx = hx - center.X;
                    double dy = hy - center.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist <= HoverRadius) { nearest = btn; break; }
                }

                UpdateHover(nearest);
            });
        }

        private void DrawAvatar(Skeleton skeleton, double canvasW, double canvasH,
                                JointType activeHandType)
        {
            AvatarCanvas.Children.Clear();

            var bones = new[]
            {
                (JointType.Head,           JointType.ShoulderCenter),
                (JointType.ShoulderCenter, JointType.ShoulderLeft),
                (JointType.ShoulderCenter, JointType.ShoulderRight),
                (JointType.ShoulderLeft,   JointType.ElbowLeft),
                (JointType.ElbowLeft,      JointType.WristLeft),
                (JointType.WristLeft,      JointType.HandLeft),
                (JointType.ShoulderRight,  JointType.ElbowRight),
                (JointType.ElbowRight,     JointType.WristRight),
                (JointType.WristRight,     JointType.HandRight),
                (JointType.ShoulderCenter, JointType.Spine),
                (JointType.Spine,          JointType.HipCenter),
                (JointType.HipCenter,      JointType.HipLeft),
                (JointType.HipCenter,      JointType.HipRight),
                (JointType.HipLeft,        JointType.KneeLeft),
                (JointType.KneeLeft,       JointType.AnkleLeft),
                (JointType.HipRight,       JointType.KneeRight),
                (JointType.KneeRight,      JointType.AnkleRight),
            };

            foreach (var (a, b) in bones)
                DrawAvatarBone(skeleton, a, b, canvasW, canvasH);

            // Head circle
            var headJoint = skeleton.Joints[JointType.Head];
            if (headJoint.TrackingState != JointTrackingState.NotTracked)
            {
                var hp = MapToWindow(headJoint.Position, canvasW, canvasH);
                var head = new Ellipse
                {
                    Width = 28,
                    Height = 28,
                    Stroke = new SolidColorBrush(JointColor),
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(60, 200, 220, 255)),
                    Opacity = AvatarOpacity * 2
                };
                Canvas.SetLeft(head, hp.X - 14);
                Canvas.SetTop(head, hp.Y - 14);
                AvatarCanvas.Children.Add(head);
            }

            // Joint dots
            foreach (var jt in JointAngleCalculator.AnalysisJoints)
            {
                var joint = skeleton.Joints[jt];
                if (joint.TrackingState == JointTrackingState.NotTracked) continue;

                var pt = MapToWindow(joint.Position, canvasW, canvasH);
                bool isActiveHand = (jt == activeHandType);

                if (isActiveHand)
                {
                    var ring = new Ellipse
                    {
                        Width = 32,
                        Height = 32,
                        Stroke = new SolidColorBrush(ActiveHandColor),
                        StrokeThickness = 1.5,
                        Fill = Brushes.Transparent,
                        Opacity = 0.5
                    };
                    Canvas.SetLeft(ring, pt.X - 16);
                    Canvas.SetTop(ring, pt.Y - 16);
                    AvatarCanvas.Children.Add(ring);
                }

                var dot = new Ellipse
                {
                    Width = isActiveHand ? 18 : JointDotRadius * 2,
                    Height = isActiveHand ? 18 : JointDotRadius * 2,
                    Fill = new SolidColorBrush(isActiveHand ? ActiveHandColor : JointColor),
                    Opacity = isActiveHand ? 0.9 : AvatarOpacity * 1.5
                };
                Canvas.SetLeft(dot, pt.X - dot.Width / 2);
                Canvas.SetTop(dot, pt.Y - dot.Height / 2);
                AvatarCanvas.Children.Add(dot);
            }
        }

        private void DrawAvatarBone(Skeleton skeleton, JointType a, JointType b,
                                    double cw, double ch)
        {
            var j1 = skeleton.Joints[a];
            var j2 = skeleton.Joints[b];
            if (j1.TrackingState == JointTrackingState.NotTracked ||
                j2.TrackingState == JointTrackingState.NotTracked) return;

            var p1 = MapToWindow(j1.Position, cw, ch);
            var p2 = MapToWindow(j2.Position, cw, ch);

            AvatarCanvas.Children.Add(new Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = new SolidColorBrush(BoneColor),
                StrokeThickness = BoneThickness,
                Opacity = AvatarOpacity
            });
        }

        private Point MapToWindow(SkeletonPoint pos, double w, double h)
        {
            return new Point(
                (pos.X + 1.0) / 2.0 * w,
                (1.0 - (pos.Y + 1.0) / 2.0) * h);
        }

        private void OnHoverTick(object sender, EventArgs e)
        {
            if (_hoveredButton == null)
            {
                if (_hoverProgress > 0)
                {
                    _hoverProgress = Math.Max(0, _hoverProgress - (1.0 / FrameRate) * 2);
                    UpdateProgressArc(_hoveredButton, _hoverProgress);
                }
                return;
            }

            _hoverProgress += (1.0 / FrameRate) / HoldSeconds;
            if (_hoverProgress >= 1.0)
            {
                _hoverProgress = 0;
                FireButton(_hoveredButton);
                return;
            }
            UpdateProgressArc(_hoveredButton, _hoverProgress);
        }

        private void UpdateHover(Grid newButton)
        {
            if (newButton == _hoveredButton) return;

            if (_hoveredButton != null)
            {
                SetGlowOpacity(_hoveredButton, 0);
                UpdateProgressArc(_hoveredButton, 0);
                _hoverProgress = 0;
            }

            _hoveredButton = newButton;
            if (_hoveredButton != null)
                SetGlowOpacity(_hoveredButton, 0.15);
        }

        private void UpdateProgressArc(Grid btn, double progress)
        {
            if (btn == null) return;
            foreach (var child in btn.Children)
            {
                if (child is Ellipse arc && arc.Name.StartsWith("Progress_"))
                {
                    double circ = _buttonCircumferences.ContainsKey(btn)
                                    ? _buttonCircumferences[btn] : 471;
                    double filled = circ * progress;
                    arc.StrokeDashArray = new DoubleCollection { filled, circ - filled };
                    arc.InvalidateVisual();
                    break;
                }
            }
        }

        private void SetGlowOpacity(Grid btn, double opacity)
        {
            foreach (var child in btn.Children)
            {
                if (child is Ellipse e && e.Name.StartsWith("Glow_"))
                {
                    e.BeginAnimation(Ellipse.OpacityProperty,
                        new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(150)));
                    break;
                }
            }
        }

        private void FireButton(Grid btn)
        {
            if (_isFiring) return;
            _isFiring = true;

            SetGlowOpacity(btn, 0);
            UpdateProgressArc(btn, 0);
            _hoveredButton = null;
            _hoverProgress = 0;

            switch (btn.Tag?.ToString())
            {
                case "StartSession":
                    var session = new SessionView(_kinectManager, DifficultyLevel.Beginner);
                    session.Show(); Hide();
                    break;
                case "ROMTest":
                    var rom = new ROMTestView(_kinectManager);
                    bool? res = rom.ShowDialog();
                    if (res == true)
                    {
                        var sess = new SessionView(_kinectManager, rom.ResultDifficulty);
                        sess.Show(); Hide();
                    }
                    break;
                case "PoseLibrary":
                    MessageBox.Show("Pose Library coming soon!", "Not Yet Implemented",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case "Settings":
                    MessageBox.Show("Settings coming soon!", "Not Yet Implemented",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case "Exit":
                    _kinectManager?.Shutdown();
                    Application.Current.Shutdown();
                    break;
            }

            _isFiring = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            _hoverTimer?.Stop();
            _kinectManager?.Shutdown();
            base.OnClosed(e);
        }
    }
}