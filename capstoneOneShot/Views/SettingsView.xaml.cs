using capstoneOneShot.Services;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace capstoneOneShot.Views
{
    public partial class SettingsView : Window
    {
        private KinectManager _kinectManager;
        private PointerSelectionService _pointerService;

        public SettingsView(KinectManager kinectManager)
        {
            InitializeComponent();
            _kinectManager = kinectManager;

            // Load settings
            KinectBypassToggle.IsChecked = Properties.Settings.Default.KinectBypass;

            // Opacity fade-in
            this.Opacity = 0;
            this.Loaded += (s, e) =>
            {
                this.BeginAnimation(Window.OpacityProperty,
                    new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(400)));
            };

            SetupPointer();
            SetupMouseAndKeyboard();
        }

        private void SetupPointer()
        {
            _pointerService = new PointerSelectionService(MenuCanvas);
            
            double circ = Math.PI * 120; // width is 120
            _pointerService.RegisterButton(Btn_ReturnMenu, circ, () => Dispatcher.Invoke(() => Btn_ReturnMenu_Click(null, null)));

            _pointerService.Start();

            if (_kinectManager != null)
                _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;
        }

        private void SetupMouseAndKeyboard()
        {
            Btn_ReturnMenu.MouseEnter += (s, e) => _pointerService.SetHover(Btn_ReturnMenu);
            Btn_ReturnMenu.MouseLeave += (s, e) => _pointerService.ClearHover();

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Btn_ReturnMenu_Click(null, null);
                }
            };
        }

        private void OnSkeletonFrameReady(Microsoft.Kinect.Skeleton[] skeletons)
        {
            if (skeletons == null || skeletons.Length == 0) return;
            var skeleton = skeletons[0];
            var leftHand = skeleton.Joints[Microsoft.Kinect.JointType.HandLeft];
            var rightHand = skeleton.Joints[Microsoft.Kinect.JointType.HandRight];
            var activeHand = leftHand.Position.Y > rightHand.Position.Y ? leftHand : rightHand;

            Dispatcher.Invoke(() =>
            {
                if (activeHand.TrackingState == Microsoft.Kinect.JointTrackingState.NotTracked)
                {
                    _pointerService.ProcessHandPosition(new Point(0, 0), false);
                    return;
                }

                var handPoint = MapToCanvas(activeHand.Position, MenuCanvas.ActualWidth, MenuCanvas.ActualHeight);
                _pointerService.ProcessHandPosition(handPoint, true);
            });
        }

        private Point MapToCanvas(Microsoft.Kinect.SkeletonPoint pos, double canvasW, double canvasH)
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

        private void KinectBypassToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (KinectBypassToggle.IsChecked.HasValue)
            {
                Properties.Settings.Default.KinectBypass = KinectBypassToggle.IsChecked.Value;
                Properties.Settings.Default.Save();
            }
        }

        private void Btn_ReturnMenu_Click(object sender, RoutedEventArgs e)
        {
            TransitionHelper.FadeOutAndHide(this, () =>
            {
                Application.Current.MainWindow.Show();
                this.Close();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_kinectManager != null)
                _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
            _pointerService?.Stop();
            base.OnClosed(e);
        }
    }
}
