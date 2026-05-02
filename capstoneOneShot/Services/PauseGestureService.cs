using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Kinect;

namespace capstoneOneShot.Services
{
    /// <summary>
    /// Detects a crossed-arms pause gesture and renders a visual hold indicator.
    /// Designed to run during ROM tests and yoga sessions where PointerSelectionService is disabled.
    ///
    /// Usage:
    ///   1. Call Enable(overlayCanvas) when entering yoga/ROM view
    ///   2. Call Disable() when leaving
    ///   3. Subscribe to PauseDetected to handle the pause action
    ///   4. Optionally subscribe to HoldProgressChanged for custom UI
    /// </summary>
    public class PauseGestureService
    {
        // ── Tuning constants ──────────────────────────────────────────────────
        private const float HoldSeconds = 1.5f;   // seconds to hold the pose
        private const int FrameRate = 30;     // expected Kinect body frame rate
        private const int CooldownFrames = 45;     // ~1.5s cooldown after firing (prevents re-trigger)
        private const float MinWristCrossX = 0.04f;  // how far past midline each wrist must travel (meters)
        private const float MinElbowFlexY = 0.02f;  // wrist must be at least this far above elbow (meters)

        // ── Overlay visual constants ──────────────────────────────────────────
        private const double RingSize = 120;    // outer diameter of the progress ring
        private const double RingThickness = 8;
        private const double RingCircumference = Math.PI * (RingSize - RingThickness); // ≈ 347

        // ── State ─────────────────────────────────────────────────────────────
        private bool _isEnabled = false;
        private int _holdFrames = 0;
        private int _cooldownCount = 0;
        private float _lastProgress = 0f;

        // ── Kinect / source of skeletons ────────────────────────────────────────
        private readonly KinectManager _kinectManager;

        // ── Overlay UI ────────────────────────────────────────────────────────
        private Canvas _overlayCanvas;
        private Grid _overlayRoot;     // the entire overlay container
        private Ellipse _bgCircle;        // dark background disc
        private Ellipse _progressArc;     // stroke-dasharray progress ring
        private Ellipse _glowRing;        // outer glow that pulses on completion
        private TextBlock _labelText;       // "Hold to pause" / "Pausing…"
        private TextBlock _iconText;        // crossed-arms icon character
        private bool _overlayVisible = false;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired when the user has held the crossed-arms pose long enough.</summary>
        public event EventHandler PauseDetected;

        /// <summary>Progress from 0.0 to 1.0 while arms are crossed. Resets to 0 when arms drop.</summary>
        public event EventHandler<float> HoldProgressChanged;

        // ─────────────────────────────────────────────────────────────────────
        public PauseGestureService(KinectManager kinectManager)
        {
            _kinectManager = kinectManager ?? throw new ArgumentNullException(nameof(kinectManager));
            // Subscribe to the existing KinectManager skeleton event
            _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Start listening for the pause gesture and mount the overlay onto the given canvas.
        /// Call this when the yoga or ROM view loads.
        /// </summary>
        public void Enable(Canvas overlayCanvas)
        {
            _overlayCanvas = overlayCanvas ?? throw new ArgumentNullException(nameof(overlayCanvas));
            _isEnabled = true;
            _holdFrames = 0;
            _cooldownCount = 0;
            _lastProgress = 0f;

            BuildOverlay();
        }

        /// <summary>
        /// Stop gesture detection and remove the overlay from the canvas.
        /// Call this when leaving the yoga or ROM view.
        /// </summary>
        public void Disable()
        {
            _isEnabled = false;
            _holdFrames = 0;
            _cooldownCount = 0;

            RemoveOverlay();
            _overlayCanvas = null;
        }

        // ── Body frame processing ─────────────────────────────────────────────

        // Handler wired to KinectManager.SkeletonFrameReady
        private void OnSkeletonFrameReady(Skeleton[] skeletons)
        {
            if (!_isEnabled) return;
            if (skeletons == null || skeletons.Length == 0) { Application.Current?.Dispatcher.InvokeAsync(() => ProcessBody(null)); return; }

            var skeleton = skeletons.FirstOrDefault(s => s.TrackingState == SkeletonTrackingState.Tracked);
            Application.Current?.Dispatcher.InvokeAsync(() => ProcessBody(skeleton));
        }

        private void ProcessBody(Skeleton skeleton)
        {
            // ── Cooldown: block re-trigger after firing ───────────────────────
            if (_cooldownCount > 0)
            {
                _cooldownCount--;
                // Keep overlay hidden during cooldown
                SetOverlayVisible(false);
                return;
            }

            if (skeleton == null)
            {
                // Lost tracking — bleed progress down quickly
                if (_holdFrames > 0) _holdFrames = Math.Max(0, _holdFrames - 2);
                ReportProgress(0f);
                SetOverlayVisible(false);
                return;
            }

            bool crossed = IsCrossedArms(skeleton);

            if (crossed)
            {
                _holdFrames++;
                float progress = Math.Min(1f, _holdFrames / (HoldSeconds * FrameRate));
                ReportProgress(progress);
                UpdateOverlayProgress(progress);
                SetOverlayVisible(true);

                if (_holdFrames >= (int)(HoldSeconds * FrameRate))
                {
                    FirePause();
                }
            }
            else
            {
                // Bleed out smoothly rather than snapping to zero
                if (_holdFrames > 0)
                {
                    _holdFrames = Math.Max(0, _holdFrames - 2);
                    float progress = _holdFrames / (HoldSeconds * FrameRate);
                    ReportProgress(progress);
                    UpdateOverlayProgress(progress);

                    if (_holdFrames == 0)
                        SetOverlayVisible(false);
                }
                else
                {
                    SetOverlayVisible(false);
                }
            }
        }

        // ── Crossed-arms detection ────────────────────────────────────────────

        private bool IsCrossedArms(Skeleton skeleton)
        {
            var joints = skeleton.Joints;

            // Require all key joints to be at least inferred
            JointType[] required =
            {
                JointType.WristRight, JointType.WristLeft,
                JointType.ElbowRight, JointType.ElbowLeft,
                JointType.ShoulderCenter
            };

            if (required.Any(j => joints[j].TrackingState == JointTrackingState.NotTracked))
                return false;

            // Use the shoulder centre as an approximate midline for Kinect v1 skeletons
            float midX = joints[JointType.ShoulderCenter].Position.X;

            float rightWristX = joints[JointType.WristRight].Position.X;
            float leftWristX = joints[JointType.WristLeft].Position.X;

            float rightWristY = joints[JointType.WristRight].Position.Y;
            float leftWristY = joints[JointType.WristLeft].Position.Y;
            float rightElbowY = joints[JointType.ElbowRight].Position.Y;
            float leftElbowY = joints[JointType.ElbowLeft].Position.Y;

            // Each wrist must have crossed the midline by MinWristCrossX meters
            bool rightCrossed = (midX - rightWristX) >= MinWristCrossX;
            bool leftCrossed = (leftWristX - midX) >= MinWristCrossX;

            // Wrists must be raised above their own elbows (avoids resting arms at sides)
            bool wristsRaised = (rightWristY - rightElbowY) >= MinElbowFlexY
                             && (leftWristY - leftElbowY) >= MinElbowFlexY;

            return rightCrossed && leftCrossed && wristsRaised;
        }

        // ── Fire pause ────────────────────────────────────────────────────────

        private void FirePause()
        {
            _holdFrames = 0;
            _cooldownCount = CooldownFrames;

            // Flash the ring green on completion
            PlayCompletionAnimation();

            PauseDetected?.Invoke(this, EventArgs.Empty);
        }

        private void ReportProgress(float progress)
        {
            if (Math.Abs(progress - _lastProgress) < 0.01f) return; // skip trivial updates
            _lastProgress = progress;
            HoldProgressChanged?.Invoke(this, progress);
        }

        // ── Overlay construction ──────────────────────────────────────────────

        private void BuildOverlay()
        {
            // Root container — centred at bottom-centre of the canvas
            _overlayRoot = new Grid
            {
                Width = RingSize + 60,
                Height = RingSize + 50,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Opacity = 0,
                IsHitTestVisible = false
            };

            // Outer glow ring (animates on completion)
            _glowRing = new Ellipse
            {
                Width = RingSize + 20,
                Height = RingSize + 20,
                Fill = Brushes.Transparent,
                Stroke = new SolidColorBrush(Color.FromArgb(60, 77, 208, 225)),
                StrokeThickness = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -10, 0, 0),
                Opacity = 0
            };

            // Background disc
            _bgCircle = new Ellipse
            {
                Width = RingSize,
                Height = RingSize,
                Fill = new SolidColorBrush(Color.FromArgb(180, 15, 20, 30)),
                Stroke = new SolidColorBrush(Color.FromArgb(80, 77, 208, 225)),
                StrokeThickness = 1.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };

            // Progress arc — uses StrokeDashArray trick (same as PointerSelectionService)
            double arcDiameter = RingSize - RingThickness;
            _progressArc = new Ellipse
            {
                Width = arcDiameter,
                Height = arcDiameter,
                Fill = Brushes.Transparent,
                Stroke = new SolidColorBrush(Color.FromRgb(77, 208, 225)),
                StrokeThickness = RingThickness,
                StrokeDashArray = new DoubleCollection { 0, RingCircumference },
                // Rotate so arc starts from the top (12 o'clock)
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-90),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, RingThickness / 2, 0, 0)
            };

            // Crossed-arms icon
            _iconText = new TextBlock
            {
                Text = "🤞",   // swap for a custom glyph/icon if preferred
                FontSize = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, RingSize * 0.22, 0, 0),
                Foreground = new SolidColorBrush(Color.FromArgb(200, 200, 220, 230))
            };

            // Label text
            _labelText = new TextBlock
            {
                Text = "Hold to pause",
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 160, 210, 220)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 4)
            };

            _overlayRoot.Children.Add(_glowRing);
            _overlayRoot.Children.Add(_bgCircle);
            _overlayRoot.Children.Add(_progressArc);
            _overlayRoot.Children.Add(_iconText);
            _overlayRoot.Children.Add(_labelText);

            // Position at bottom-centre of parent canvas
            // We'll update this whenever the canvas size changes
            PositionOverlay();
            _overlayCanvas.SizeChanged += (s, e) => PositionOverlay();
            _overlayCanvas.Children.Add(_overlayRoot);
        }

        private void PositionOverlay()
        {
            if (_overlayCanvas == null || _overlayRoot == null) return;

            double cx = _overlayCanvas.ActualWidth / 2 - (_overlayRoot.Width / 2);
            double cy = _overlayCanvas.ActualHeight - _overlayRoot.Height - 40;

            Canvas.SetLeft(_overlayRoot, cx);
            Canvas.SetTop(_overlayRoot, Math.Max(0, cy));
        }

        private void RemoveOverlay()
        {
            if (_overlayCanvas != null && _overlayRoot != null)
            {
                _overlayCanvas.Children.Remove(_overlayRoot);
            }
            _overlayRoot = null;
            _progressArc = null;
            _bgCircle = null;
            _glowRing = null;
            _labelText = null;
            _iconText = null;
            _overlayVisible = false;
        }

        // ── Overlay state updates ─────────────────────────────────────────────

        private void SetOverlayVisible(bool visible)
        {
            if (_overlayRoot == null || visible == _overlayVisible) return;
            _overlayVisible = visible;

            double targetOpacity = visible ? 1.0 : 0.0;
            _overlayRoot.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(targetOpacity, TimeSpan.FromMilliseconds(visible ? 200 : 400)));

            if (visible)
                _labelText.Text = "Hold to pause";
        }

        private void UpdateOverlayProgress(float progress)
        {
            if (_progressArc == null) return;

            double filled = RingCircumference * progress;
            double remaining = RingCircumference - filled;

            _progressArc.StrokeDashArray = new DoubleCollection { filled, remaining };
            _progressArc.InvalidateVisual();

            // Shift label text as user nears completion
            if (_labelText != null)
            {
                _labelText.Text = progress >= 0.75f ? "Pausing…" : "Hold to pause";
            }

            // Pulse the arc colour from cyan → white as progress increases
            byte r = (byte)(77 + (178 * progress));
            byte g = (byte)(208 - (8 * progress));
            byte b = (byte)(225 - (10 * progress));
            _progressArc.Stroke = new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private void PlayCompletionAnimation()
        {
            if (_overlayRoot == null) return;

            // Flash arc to solid white
            _progressArc?.BeginAnimation(Shape.StrokeProperty, null);
            if (_progressArc != null)
                _progressArc.Stroke = Brushes.White;

            // Glow ring pulses out
            if (_glowRing != null)
            {
                _glowRing.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimationUsingKeyFrames
                    {
                        KeyFrames =
                        {
                            new EasingDoubleKeyFrame(0.9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))),
                            new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500)))
                        }
                    });

                _glowRing.BeginAnimation(FrameworkElement.WidthProperty,
                    new DoubleAnimation(RingSize + 20, RingSize + 50,
                        TimeSpan.FromMilliseconds(500)));
                _glowRing.BeginAnimation(FrameworkElement.HeightProperty,
                    new DoubleAnimation(RingSize + 20, RingSize + 50,
                        TimeSpan.FromMilliseconds(500)));
            }

            // Fade out the whole overlay after the flash
            _overlayRoot.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(600))
                {
                    BeginTime = TimeSpan.FromMilliseconds(150)
                });

            _overlayVisible = false;
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            Disable();
            if (_kinectManager != null)
                _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
        }
    }
}