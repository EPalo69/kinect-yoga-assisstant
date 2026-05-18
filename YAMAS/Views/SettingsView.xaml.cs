using YAMAS.Services;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace YAMAS.Views
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
            HideSkeletonToggle.IsChecked = Properties.Settings.Default.HideSkeleton;
            ShowForceWinToggle.IsChecked  = Properties.Settings.Default.ShowForceWin;

            PopulateAudioDevices();

            TransitionHelper.ApplyFadeInTransition(this);

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

        private void PopulateAudioDevices()
        {
            // Populate Output Devices
            AudioOutputCombo.Items.Clear();
            AudioOutputCombo.Items.Add("System Default");
            for (int i = 0; i < NAudio.Wave.WaveOut.DeviceCount; i++)
            {
                var caps = NAudio.Wave.WaveOut.GetCapabilities(i);
                AudioOutputCombo.Items.Add(caps.ProductName);
            }

            // Select current or default
            string currentOut = Properties.Settings.Default.AudioOutputDevice;
            if (string.IsNullOrEmpty(currentOut) || !AudioOutputCombo.Items.Contains(currentOut))
                AudioOutputCombo.SelectedIndex = 0;
            else
                AudioOutputCombo.SelectedItem = currentOut;

            // Populate Input Devices
            AudioInputCombo.Items.Clear();
            AudioInputCombo.Items.Add("System Default");
            for (int i = 0; i < NAudio.Wave.WaveIn.DeviceCount; i++)
            {
                var caps = NAudio.Wave.WaveIn.GetCapabilities(i);
                AudioInputCombo.Items.Add(caps.ProductName);
            }

            // Select current or default
            string currentIn = Properties.Settings.Default.AudioInputDevice;
            if (string.IsNullOrEmpty(currentIn) || !AudioInputCombo.Items.Contains(currentIn))
                AudioInputCombo.SelectedIndex = 0;
            else
                AudioInputCombo.SelectedItem = currentIn;
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded) return; // Prevent saving while initializing

            if (KinectBypassToggle.IsChecked.HasValue)
                Properties.Settings.Default.KinectBypass = KinectBypassToggle.IsChecked.Value;
            
            if (HideSkeletonToggle.IsChecked.HasValue)
                Properties.Settings.Default.HideSkeleton = HideSkeletonToggle.IsChecked.Value;

            if (ShowForceWinToggle.IsChecked.HasValue)
                Properties.Settings.Default.ShowForceWin = ShowForceWinToggle.IsChecked.Value;

            Properties.Settings.Default.Save();
        }

        private void AudioOutputCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || AudioOutputCombo.SelectedItem == null) return;
            string selected = AudioOutputCombo.SelectedItem.ToString();
            Properties.Settings.Default.AudioOutputDevice = selected == "System Default" ? "" : selected;
            Properties.Settings.Default.Save();
        }

        private void AudioInputCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || AudioInputCombo.SelectedItem == null) return;
            string selected = AudioInputCombo.SelectedItem.ToString();
            Properties.Settings.Default.AudioInputDevice = selected == "System Default" ? "" : selected;
            Properties.Settings.Default.Save();
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
