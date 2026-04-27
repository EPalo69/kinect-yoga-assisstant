using capstoneOneShot.Models;
using capstoneOneShot.Services;
//using HelixToolkit.Wpf;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace capstoneOneShot.Views
{
    public partial class MainWindow : Window
    {
        private KinectManager _kinectManager;
        //private AvatarService _avatarService;

        // ── Hover gesture ────────────────────────────────────────────────
        private const double HoverRadius = 80;
        private const double HoldSeconds = 1.5;
        private const int FrameRate = 30;
        private const double JointRadius = 6;
        private bool _isFiring = false;

        private Grid _hoveredButton = null;
        private double _hoverProgress = 0;
        private DispatcherTimer _hoverTimer;

        private Dictionary<Grid, double> _buttonCircumferences
            = new Dictionary<Grid, double>();

        private readonly Ellipse HandCursor = new Ellipse
        {
            Width = 28,
            Height = 28,
            Fill = new SolidColorBrush(Color.FromArgb(102, 77, 208, 225)),
            Stroke = new SolidColorBrush(Color.FromRgb(77, 208, 225)),
            StrokeThickness = 2,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // ── Startup ──────────────────────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            BuildMenuButtons();
            SetupMouseAndKeyboard();
            StartKinect();
        }

        // ── Button placement ─────────────────────────────────────────────
        private void BuildMenuButtons()
        {
            MenuCanvas.UpdateLayout();

            if (MenuCanvas.ActualWidth == 0 || MenuCanvas.ActualHeight == 0)
            {
                MenuCanvas.SizeChanged += (s, ev) =>
                {
                    MenuCanvas.SizeChanged -= null;
                    PlaceButtons();
                };
                return;
            }
            PlaceButtons();
        }

        private void PlaceButtons()
        {
            double cw = MenuCanvas.ActualWidth;
            double ch = MenuCanvas.ActualHeight;
            double cx = cw / 2;
            double cy = ch / 2;
            double size = 150;
            double circ = Math.PI * size;

            var positions = new Dictionary<Grid, Point>
            {
                { Btn_StartSession, new Point(cx,        cy - 300) }, // was cy - 120
                { Btn_ROMTest,      new Point(cx - 210,  cy - 200) }, // was cy - 20
                { Btn_PoseLibrary,  new Point(cx + 210,  cy - 200) }, // was cy - 20
                { Btn_Settings,     new Point(cx - 380,  cy - 120) }, // was cy + 60
                { Btn_Exit,         new Point(cx + 380,  cy - 120) }, // was cy + 60
            };

            foreach (var kvp in positions)
            {
                var btn = kvp.Key;
                var center = kvp.Value;

                btn.Width = size;
                btn.Height = size;

                if (btn.Parent is Panel oldParent)
                    oldParent.Children.Remove(btn);

                Canvas.SetLeft(btn, center.X - size / 2);
                Canvas.SetTop(btn, center.Y - size / 2);

                if (!MenuCanvas.Children.Contains(btn))
                    MenuCanvas.Children.Add(btn);

                _buttonCircumferences[btn] = circ;
            }

            // Hand cursor always on top
            if (MenuCanvas.Children.Contains(HandCursor))
                MenuCanvas.Children.Remove(HandCursor);
            MenuCanvas.Children.Add(HandCursor);
        }

        // ── Mouse + keyboard fallback ────────────────────────────────────
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

        // ── Kinect init ──────────────────────────────────────────────────
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

            //_avatarService = new AvatarService(AvatarViewport);
        }

        // ── Body status pill ─────────────────────────────────────────────
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
                        //_avatarService?.Clear();
                        break;
                }
            });
        }

        // ── Skeleton frame ───────────────────────────────────────────────
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
                DrawSkeleton(skeleton);

                if (activeHand.TrackingState == JointTrackingState.NotTracked)
                {
                    HandCursor.Visibility = Visibility.Collapsed;
                    UpdateHover(null);
                    return;
                }

                // Use MapToCanvas so cursor is in the exact same coordinate space as the skeleton
                var handPoint = MapToCanvas(activeHand.Position, MenuCanvas.ActualWidth, MenuCanvas.ActualHeight); double hx = handPoint.X;
                double hy = handPoint.Y;

                Canvas.SetLeft(HandCursor, hx - HandCursor.Width / 2);
                Canvas.SetTop(HandCursor, hy - HandCursor.Height / 2);
                HandCursor.Visibility = Visibility.Visible;

                // Hit-test buttons
                Grid nearest = null;
                foreach (var btn in new[] { Btn_StartSession, Btn_ROMTest,
                                     Btn_PoseLibrary, Btn_Settings, Btn_Exit })
                {
                    double bx = Canvas.GetLeft(btn) + btn.Width / 2;
                    double by = Canvas.GetTop(btn) + btn.Height / 2;
                    double dx = hx - bx;
                    double dy = hy - by;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist <= HoverRadius) { nearest = btn; break; }
                }

                UpdateHover(nearest);
            });
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

            var p1 = MapToCanvas(j1.Position, SkeletonCanvas.ActualWidth, SkeletonCanvas.ActualHeight);
            var p2 = MapToCanvas(j2.Position, SkeletonCanvas.ActualWidth, SkeletonCanvas.ActualHeight);
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
            var p = MapToCanvas(joint.Position, SkeletonCanvas.ActualWidth, SkeletonCanvas.ActualHeight);
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

        private Point MapToCanvas(SkeletonPoint pos, double canvasW, double canvasH)
        {
            const double sourceAspect = 640.0 / 480.0;
            double canvasAspect = canvasW / canvasH;
            double renderW, renderH, offsetX, offsetY;

            if (canvasAspect > sourceAspect)
            {
                renderH = canvasH;
                renderW = canvasH * sourceAspect;
                offsetX = (canvasW - renderW) / 2.0;
                offsetY = 0;
            }
            else
            {
                renderW = canvasW;
                renderH = canvasW / sourceAspect;
                offsetX = 0;
                offsetY = (canvasH - renderH) / 2.0;
            }

            double x = (pos.X + 1.0) / 2.0 * renderW + offsetX;
            double y = (1.0 - (pos.Y + 1.0) / 2.0) * renderH + offsetY;
            return new Point(x, y);
        }

        // ── Hover timer ──────────────────────────────────────────────────
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

        // ── Fire button ──────────────────────────────────────────────────
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
                    var selection = new PoseSelectionView(_kinectManager);
                    selection.Show();
                    Hide();
                    break;

                case "ROMTest":
                    var rom = new ROMTestView(_kinectManager);
                    bool? res = rom.ShowDialog();
                    if (res == true)
                    {
                        var sel = new PoseSelectionView(_kinectManager);
                        sel.Show();
                        Hide();
                    }
                    break;

                case "PoseLibrary":
                    var library = new PoseSelectionView(_kinectManager);
                    library.Show();
                    Hide();
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

        // ── Cleanup ──────────────────────────────────────────────────────
        protected override void OnClosed(EventArgs e)
        {
            _hoverTimer?.Stop();
            _kinectManager?.Shutdown();
            base.OnClosed(e);
        }
    }
}