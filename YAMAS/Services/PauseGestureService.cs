using Microsoft.Kinect;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace YAMAS.Services
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
        private TextBlock _labelText;       // "Hold to pause" / "Pausing…"
        private TextBlock _iconText;        // crossed-arms icon character
        private bool _overlayVisible = false;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired when the user has held the crossed-arms pose long enough.</summary>
        public event EventHandler PauseDetected;

        /// <summary>Progress from 0.0 to 1.0 while arms are crossed. Resets to 0 when arms drop.</summary>
        public event EventHandler<float> HoldProgressChanged;

        public event EventHandler OverlayClicked;

        // ─────────────────────────────────────────────────────────────────────
        public PauseGestureService(KinectManager kinectManager)
        {
            _kinectManager = kinectManager ?? throw new ArgumentNullException(nameof(kinectManager));
            // Subscribe to the existing KinectManager skeleton event (removed gesture detection)
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
            // Removed gesture detection logic
        }

        private void ProcessBody(Skeleton skeleton)
        {
            // Removed gesture detection logic
        }

        private bool IsBothHandsAboveHead(Skeleton skeleton)
        {
            return false;
        }

        // ── Fire pause ────────────────────────────────────────────────────────
        public void SimulateClick()
        {
            FirePause();
        }

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
            _overlayRoot = new Grid
            {
                Width = 165, // 110 * 1.5
                Height = 165,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Opacity = 0.75,
                IsHitTestVisible = true
            };

            var bg = new Border
            {
                Width = 165,
                Height = 165,
                Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x08, 0x0C, 0x14)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x4D, 0xD0, 0xE1)),
                BorderThickness = new Thickness(1.5), // 1 * 1.5
                CornerRadius = new CornerRadius(24), // 16 * 1.5
                Padding = new Thickness(12) // 8 * 1.5
            };

            bg.Cursor = Cursors.Hand;
            bg.MouseLeftButtonUp += (s, e) => FirePause();

            bg.MouseEnter += (s, e) =>
            {
                _overlayRoot.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150)));
            };
            bg.MouseLeave += (s, e) =>
            {
                _overlayRoot.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0.75, TimeSpan.FromMilliseconds(150)));
            };

            _overlayRoot.IsHitTestVisible = true;

            var innerGrid = new Grid();
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var image = new Image
            {
                Source = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Assets/Icons/pause-gesture.png")),
                Width = 60, // 40 * 1.5
                Height = 60,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(image, 0);

            var textStack = new StackPanel
            {
                Margin = new Thickness(0, 9, 0, 0), // 6 * 1.5
                VerticalAlignment = VerticalAlignment.Center
            };

            _iconText = new TextBlock
            {
                Text = "⏸",
                FontSize = 21, // 14 * 1.5
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4D, 0xD0, 0xE1)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3) // 2 * 1.5
            };

            var gestureLabel = new TextBlock
            {
                Text = "CLICK OR SAY",
                FontSize = 12, // 9 * 1.5
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4D, 0xD0, 0xE1)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 1.5) // 1 * 1.5
            };

            _labelText = new TextBlock
            {
                Text = "'PAUSE'",
                FontSize = 15, // 10 * 1.5
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xE8, 0xED, 0xF2)),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            textStack.Children.Add(_iconText);
            textStack.Children.Add(gestureLabel);
            textStack.Children.Add(_labelText);
            Grid.SetRow(textStack, 1);

            innerGrid.Children.Add(image);
            innerGrid.Children.Add(textStack);
            bg.Child = innerGrid;

            _overlayRoot.Children.Add(bg);

            PositionOverlay();
            _overlayCanvas.SizeChanged += (s, e) => PositionOverlay();
            _overlayCanvas.Children.Add(_overlayRoot);
            Panel.SetZIndex(_overlayRoot, 9999);

            _overlayRoot.Opacity = 0.75;
            _overlayVisible = true;
        }

        private void PositionOverlay()
        {
            if (_overlayCanvas == null || _overlayRoot == null) return;

            // Top-right with a small margin
            Canvas.SetRight(_overlayRoot, 16);
            Canvas.SetTop(_overlayRoot, 16);

            // Canvas.SetRight doesn't work directly — calculate from width
            double left = _overlayCanvas.ActualWidth - _overlayRoot.Width - 16;
            Canvas.SetLeft(_overlayRoot, Math.Max(0, left));
            Canvas.SetTop(_overlayRoot, 16);
        }

        private void RemoveOverlay()
        {
            if (_overlayCanvas != null && _overlayRoot != null)
            {
                _overlayCanvas.Children.Remove(_overlayRoot);
            }
            _overlayRoot = null;
            _labelText = null;
            _iconText = null;
            _overlayVisible = false;
        }

        // ── Overlay state updates ─────────────────────────────────────────────

        private void SetOverlayVisible(bool visible)
        {
            if (_overlayRoot == null) return;

            if (visible)
                _labelText.Text = "to pause";
            else
                _labelText.Text = "to pause";
        }

        private void UpdateOverlayProgress(float progress)
        {
            // Shift label text as user nears completion
            if (_labelText != null)
            {
                _labelText.Text = progress >= 0.75f ? "Pausing…" : "Hold to pause";
            }
        }

        private void PlayCompletionAnimation()
        {
            if (_overlayRoot == null) return;

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
        }
    }
}