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

            // Use ROM-filtered poses if available, otherwise all poses
            _allPoses = UserSession.HasCompletedROM
                ? PoseLibrary.GetPosesForUser(UserSession.ROMProfile)
                : PoseLibrary.GetAllPoses();

            Loaded += (s, e) =>
            {
                _pointerService = new PointerSelectionService(MenuCanvas);
                _pointerService.Start();

                // Register static buttons
                _pointerService.RegisterButton(Btn_Back, Math.PI * 100, () => BackButton_Click(null, null));

                _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;

                ShowPoses(_allPoses);
                ShowROMBanner();
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
            double circ = Math.PI * 300; // 300 is the width/height
            _pointerService.RegisterButton(grid, circ, () => SimulatePoseClick(grid));
            grid.MouseEnter += (s, ev) => _pointerService.SetHover(grid);
            grid.MouseLeave += (s, ev) => _pointerService.ClearHover();
        }

        private void PoseCard_Unloaded(object sender, RoutedEventArgs e)
        {
            var grid = (Grid)sender;
            _pointerService.UnregisterButton(grid);
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
            SimulatePoseClick((Grid)sender);
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
            _pointerService?.Stop();
            base.OnClosed(e);
        }
    }
}
