using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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

        public PoseDetailView(KinectManager kinectManager, PoseDefinition pose)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _pose = pose;
            Loaded += (s, e) =>
            {
                PopulateUI();

                _pointerService = new PointerSelectionService(MenuCanvas);
                _pointerService.Start();

                _pointerService.RegisterButton(Btn_Back, Math.PI * 100, () => BackButton_Click(null, null));
                _pointerService.RegisterButton(Btn_Begin, Math.PI * 150, () => BeginButton_Click(null, null));

                // Add mouse events for hover
                Btn_Back.MouseEnter += (s2, ev) => _pointerService.SetHover(Btn_Back);
                Btn_Back.MouseLeave += (s2, ev) => _pointerService.ClearHover();
                Btn_Begin.MouseEnter += (s2, ev) => _pointerService.SetHover(Btn_Begin);
                Btn_Begin.MouseLeave += (s2, ev) => _pointerService.ClearHover();

                _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;
                _pointerService.BringCursorToFront();
            };
        }

        private void PopulateUI()
        {
            // Load the image based on PoseDefinition.ImageFileName
            if (!string.IsNullOrEmpty(_pose.ImageFileName))
            {
                try
                {
                    string packUri = "pack://application:,,,/Assets/Poses/" + _pose.ImageFileName;
                    PoseImage.Source = new BitmapImage(new Uri(packUri, UriKind.Absolute));
                }
                catch { }
            }

            HoldTimeLeftLabel.Text = $"{_pose.HoldSeconds}s hold";

            RulesList.Items.Clear(); // Clear XAML defaults
            RulesList.ItemsSource = _pose.Rules;
        }

        private void BeginButton_Click(object sender, MouseButtonEventArgs e)
        {
            // Pass a single-pose session
            var session = new SessionView(_kinectManager, _pose);
            session.Show();
            Close();
        }

        private void BackButton_Click(object sender, MouseButtonEventArgs e)
        {
            var selection = new PoseSelectionView(_kinectManager);
            selection.Show();
            Close();
        }

        // ── Kinect logic ─────────────────────────────────────────────────
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

        protected override void OnClosed(EventArgs e)
        {
            _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
            _pointerService?.Stop();
            base.OnClosed(e);
        }
    }
}
