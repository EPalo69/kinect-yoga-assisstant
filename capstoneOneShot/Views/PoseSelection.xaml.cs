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
    /// Interaction logic for PoseSelection.xaml
    /// </summary>
    public partial class PoseSelectionView : Window
    {
        private readonly KinectManager _kinectManager;
        private readonly List<PoseDefinition> _allPoses;
        private Grid _activeFilter;
        private PointerSelectionService _pointerService;

        public PoseSelectionView(KinectManager kinectManager)
        {
            InitializeComponent();
            TransitionHelper.ApplyFadeInTransition(this);
            _kinectManager = kinectManager;

            _allPoses = PoseLibrary.GetAllPoses();

            if (UserSession.HasCompletedROM)
            {
                var profile = UserSession.ROMProfile;
                foreach (var pose in _allPoses)
                {
                    if (!profile.CanPerformPose(pose))
                    {
                        pose.IsSelectable = false;
                        pose.WarningMessage = "Unavailable: Your ROM test indicates you do not have the required mobility for this pose.";
                    }
                }
            }

            Loaded += (s, e) =>
            {
                _pointerService = new PointerSelectionService(MenuCanvas);
                _pointerService.Start();

                // Register static buttons
                _pointerService.RegisterButton(Btn_Back, Math.PI * 100, () => BackButton_Click(null, null));

                _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;
                _kinectManager.BodyStatusChanged  += OnBodyStatusChanged;

                ShowPoses(_allPoses);
                ShowROMBanner();
                InitStatusPills();
                _pointerService.BringCursorToFront();
            };
        }

        private void ShowROMBanner()
        {
            if (UserSession.HasCompletedROM)
            {
                var profile = UserSession.ROMProfile;
                ROMBannerLabel.Text = "Showing poses matched to your flexibility  •  "
                                          + profile.AssignedDifficulty.ToString()
                                          + " level";
                ROMBanner.Visibility = Visibility.Visible;
            }
            else
            {
                ROMBanner.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowPoses(List<PoseDefinition> poses)
        {
            PoseList.ItemsSource = poses;
        }

        // ── Pose card click ──────────────────────────────────────────────
        private void PoseCard_Loaded(object sender, RoutedEventArgs e)
        {
            var grid = (Grid)sender;
            var pose = (PoseDefinition)grid.Tag;

            if (!pose.IsSelectable) return;

            double circ = Math.PI * 300; // 300 is the width/height
            _pointerService.RegisterButton(grid, circ, () => SimulatePoseClick(grid));
            grid.MouseEnter += (s, ev) => _pointerService.SetHover(grid);
            grid.MouseLeave += (s, ev) => _pointerService.ClearHover();
        }

        private void PoseCard_Unloaded(object sender, RoutedEventArgs e)
        {
            var grid = (Grid)sender;
            var pose = (PoseDefinition)grid.Tag;
            if (pose.IsSelectable)
            {
                _pointerService.UnregisterButton(grid);
            }
        }

        private void SimulatePoseClick(Grid grid)
        {
            var pose = (PoseDefinition)grid.Tag;
            TransitionHelper.FadeOutAndClose(this, () => {
                var detailView = new PoseDetailView(_kinectManager, pose);
                detailView.Show();
            });
        }

        private void PoseCard_Click(object sender, MouseButtonEventArgs e)
        {
            var grid = (Grid)sender;
            var pose = (PoseDefinition)grid.Tag;
            if (pose.IsSelectable)
            {
                SimulatePoseClick(grid);
            }
        }

        private void BackButton_Click(object sender, MouseButtonEventArgs e)
        {
            TransitionHelper.FadeOutAndClose(this, () => {
                Application.Current.MainWindow.Show();
            });
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
            _kinectManager.BodyStatusChanged  -= OnBodyStatusChanged;
            _pointerService?.Stop();
            base.OnClosed(e);
        }

        // ── Status pills ──────────────────────────────────────────────────
        private void InitStatusPills()
        {
            // Kinect pill — check whether hardware was actually found and started
            bool kinectOk = _kinectManager.IsConnected;
            PillKinectDot.Fill   = new SolidColorBrush(kinectOk ? Color.FromRgb(34,197,94) : Color.FromRgb(239,68,68));
            PillKinectLabel.Text = kinectOk ? "Kinect Connected" : "Kinect Not Connected";
            PillKinectLabel.Foreground = new SolidColorBrush(kinectOk ? Color.FromRgb(34,197,94) : Color.FromRgb(156,163,175));

            // ROM pill
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
