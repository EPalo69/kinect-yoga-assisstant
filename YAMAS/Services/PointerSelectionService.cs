using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace YAMAS.Services
{
    public class PointerSelectionService
    {
        private const double HoverRadius = 80;
        private const double HoldSeconds = 1.5;
        private const int FrameRate = 30;

        // ── Fix 1: EMA smoothing ──────────────────────────────────────────────
        // Lower alpha = smoother but more lag. 0.2–0.3 is a good starting range.
        private const double SmoothingAlpha = 0.25;
        private Point _smoothedPosition = new Point(0, 0);
        private bool _hasFirstPosition = false;

        // ── Fix 2: Hysteresis ────────────────────────────────────────────────
        // Candidate must be hovered for N consecutive frames before becoming active.
        private const int AcquireFrames = 3;
        private const int LossFrames = 10;
        private Grid _candidateButton = null;
        private int _candidateFrames = 0;
        private int _lossFramesCount = 0;

        // ── Fix 3: Grace period on hand loss ─────────────────────────────────
        private const int GraceFrames = 9; // ~300ms at 30fps
        private int _graceCountdown = 0;

        private bool _isFiring = false;
        private bool _isMouseHovering = false;
        private Grid _hoveredButton = null;
        private double _hoverProgress = 0;
        private DispatcherTimer _hoverTimer;
        private DateTime _lastTickTime;
        private Canvas _canvas;
        private Dictionary<Grid, (double Circumference, Action FireAction)> _buttons 
            = new Dictionary<Grid, (double, Action)>();

        public Ellipse HandCursor { get; private set; }

        public PointerSelectionService(Canvas canvas)
        {
            _canvas = canvas;

            HandCursor = new Ellipse
            {
                Width = 28,
                Height = 28,
                Fill = new SolidColorBrush(Color.FromArgb(102, 77, 208, 225)),
                Stroke = new SolidColorBrush(Color.FromRgb(77, 208, 225)),
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };

            // Clear any lingering pointer balls left on the canvas from previous instantiations
            _canvas.Children.Clear();
            _canvas.Children.Add(HandCursor);

            _hoverTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.0 / FrameRate)
            };
            _hoverTimer.Tick += OnHoverTick;
        }

        public void BringCursorToFront()
        {
            if (_canvas.Children.Contains(HandCursor))
                _canvas.Children.Remove(HandCursor);
            _canvas.Children.Add(HandCursor);
        }

        public void Start()
        {
            if (!_canvas.Children.Contains(HandCursor))
                _canvas.Children.Add(HandCursor);
            _lastTickTime = DateTime.Now;
            _hoverTimer.Start();
        }

        public void Stop()
        {
            _hoverTimer.Stop();
            if (_canvas != null && _canvas.Children.Contains(HandCursor))
            {
                _canvas.Children.Remove(HandCursor);
            }
        }

        public void RegisterButton(Grid btn, double circumference, Action onFire)
        {
            _buttons[btn] = (circumference, onFire);
        }

        public void UnregisterButton(Grid btn)
        {
            if (_buttons.ContainsKey(btn))
            {
                _buttons.Remove(btn);
            }
        }

        public void ClearButtons()
        {
            _buttons.Clear();
        }

        public void ProcessHandPosition(Point handPoint, bool isTracked)
        {
            if (_isMouseHovering) return; // Prevent Kinect from interrupting mouse

            if (!isTracked)
            {
                // ── Fix 3: Don't snap the cursor away immediately ─────────────
                _graceCountdown--;
                if (_graceCountdown <= 0)
                {
                    HandCursor.Visibility = Visibility.Collapsed;
                    UpdateHover(null);
                    _candidateButton = null;
                    _candidateFrames = 0;
                }
                return;
            }

            _graceCountdown = GraceFrames; // reset grace on every good frame

            // ── Fix 1: Apply EMA to raw Kinect position ───────────────────────
            if (!_hasFirstPosition)
            {
                _smoothedPosition = handPoint;
                _hasFirstPosition = true;
            }
            else
            {
                _smoothedPosition = new Point(
                    SmoothingAlpha * handPoint.X + (1 - SmoothingAlpha) * _smoothedPosition.X,
                    SmoothingAlpha * handPoint.Y + (1 - SmoothingAlpha) * _smoothedPosition.Y
                );
            }

            double hx = _smoothedPosition.X;
            double hy = _smoothedPosition.Y;

            Canvas.SetLeft(HandCursor, hx - HandCursor.Width / 2);
            Canvas.SetTop(HandCursor, hy - HandCursor.Height / 2);
            HandCursor.Visibility = Visibility.Visible;

            // Hit-test against registered buttons
            Grid nearest = null;
            foreach (var btn in _buttons.Keys)
            {
                if (!btn.IsLoaded || !btn.IsVisible) continue;
                try
                {
                    Point center = new Point(btn.ActualWidth / 2, btn.ActualHeight / 2);
                    Point mapped = btn.TransformToVisual(_canvas).Transform(center);
                    double dx = hx - mapped.X;
                    double dy = hy - mapped.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    double dynamicRadius = Math.Max(HoverRadius,
                        Math.Max(btn.ActualWidth, btn.ActualHeight) / 2.0);

                    if (dist <= dynamicRadius) { nearest = btn; break; }
                }
                catch { continue; }
            }

            // ── Fix 2: Hysteresis — require N frames before switching hover ───
            if (nearest == _hoveredButton)
            {
                // Already on the active button, no candidate needed
                _candidateButton = null;
                _candidateFrames = 0;
                _lossFramesCount = 0;
            }
            else if (nearest == null)
            {
                // Empty space
                _candidateButton = null;
                _candidateFrames = 0;
                if (_hoveredButton != null)
                {
                    _lossFramesCount++;
                    if (_lossFramesCount >= LossFrames)
                    {
                        UpdateHover(null);
                        _lossFramesCount = 0;
                    }
                }
            }
            else
            {
                _lossFramesCount = 0;
                if (nearest == _candidateButton)
                {
                    _candidateFrames++;
                    if (_candidateFrames >= AcquireFrames)
                    {
                        UpdateHover(nearest);
                        _candidateButton = null;
                        _candidateFrames = 0;
                    }
                }
                else
                {
                    // New candidate — reset the counter
                    _candidateButton = nearest;
                    _candidateFrames = 1;
                }
            }
        }

        public void ResetPosition()
        {
            _hasFirstPosition = false;
            _smoothedPosition = new Point(0, 0);
            _candidateButton = null;
            _candidateFrames = 0;
            _graceCountdown = 0;
        }

        private void OnHoverTick(object sender, EventArgs e)
        {
            double dt = (DateTime.Now - _lastTickTime).TotalSeconds;
            _lastTickTime = DateTime.Now;
            if (dt > 0.1) dt = 0.1; // Cap dt to prevent massive jumps

            if (_hoveredButton == null)
            {
                return; // Decay logic is handled by UpdateHover resetting to 0 instantly for now.
            }

            _hoverProgress += dt / HoldSeconds;
            if (_hoverProgress >= 1.0)
            {
                _hoverProgress = 1.0;
                UpdateProgressArc(_hoveredButton, 1.0);
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
                    double circ = _buttons.ContainsKey(btn) ? _buttons[btn].Circumference : 471;
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

            // Keep it visually full and glowing during the action
            SetGlowOpacity(btn, 0.4);
            UpdateProgressArc(btn, 1.0);
            
            _hoveredButton = null;
            _hoverProgress = 0;

            if (_buttons.TryGetValue(btn, out var data))
            {
                data.FireAction?.Invoke();
            }

            // Clean up visual state asynchronously
            Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(300);
                UpdateProgressArc(btn, 0);
                SetGlowOpacity(btn, 0);
                _isFiring = false;
            });
        }

        public void ManualFire(Grid btn)
        {
            FireButton(btn);
        }

        public void SetHover(Grid btn)
        {
            _isMouseHovering = true;
            UpdateHover(btn);
        }
        
        public void ClearHover()
        {
            _isMouseHovering = false;
            UpdateHover(null);
            _hoverProgress = 0;
        }
    }
}
