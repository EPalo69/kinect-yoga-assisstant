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
using YAMAS.Models;
using YAMAS.Services;

namespace YAMAS.Views
{
    /// <summary>
    /// Interaction logic for SessionResultsView.xaml
    /// </summary>
    public partial class SessionResultsView : Window
    {
        private readonly KinectManager _kinectManager;
        private readonly PoseDefinition _pose;
        private double _averageAccuracy;
        private double _bestScore;
        private int _holdSeconds;
        private int _totalSessionSeconds;

        private PointerSelectionService _pointerService;

        public SessionResultsView(KinectManager kinectManager, PoseDefinition pose)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _pose = pose;
            
            // Set up pointer service for the new ellipse buttons
            _pointerService = new PointerSelectionService(CursorCanvas);
            const double circ = 628; // 200 * Pi
            _pointerService.RegisterButton(PracticeAgainButton, circ, () => Dispatcher.Invoke(() => PracticeAgain_Click(null, null)));
            _pointerService.RegisterButton(NewPoseButton, circ, () => Dispatcher.Invoke(() => NewPose_Click(null, null)));
            _pointerService.RegisterButton(MainMenuButton, circ, () => Dispatcher.Invoke(() => MainMenu_Click(null, null)));

            _kinectManager.SkeletonFrameReady += OnSkeletonFrameReady;
        }

        public void SetResults(double averageAccuracy, double bestScore, int holdSeconds, int totalSessionSeconds)
        {
            _averageAccuracy = averageAccuracy;
            _bestScore = bestScore;
            _holdSeconds = holdSeconds;
            _totalSessionSeconds = totalSessionSeconds;
            Loaded += (s, e) => 
            {
                PopulateUI();
                _pointerService.Start();
                _pointerService.BringCursorToFront();
                _pointerService.ResetPosition();
            };
        }

        private void OnSkeletonFrameReady(Microsoft.Kinect.Skeleton[] skeletons)
        {
            if (_pointerService == null) return;
            if (skeletons == null || skeletons.Length == 0) return;

            var skeleton = skeletons.FirstOrDefault(s => s.TrackingState == Microsoft.Kinect.SkeletonTrackingState.Tracked);
            if (skeleton == null)
            {
                Dispatcher.Invoke(() => _pointerService.ProcessHandPosition(new Point(), false));
                return;
            }

            var leftHand = skeleton.Joints[Microsoft.Kinect.JointType.HandLeft];
            var rightHand = skeleton.Joints[Microsoft.Kinect.JointType.HandRight];
            var activeHand = leftHand.Position.Y > rightHand.Position.Y ? leftHand : rightHand;
            bool tracked = activeHand.TrackingState == Microsoft.Kinect.JointTrackingState.Tracked;

            Dispatcher.Invoke(() =>
            {
                if (!tracked)
                {
                    _pointerService.ProcessHandPosition(new Point(), false);
                    return;
                }

                // Map to screen
                double screenW = SystemParameters.PrimaryScreenWidth;
                double screenH = SystemParameters.PrimaryScreenHeight;
                
                double x = (activeHand.Position.X + 1.0) / 2.0 * screenW;
                double y = (1.0 - (activeHand.Position.Y + 1.0) / 2.0) * screenH;

                _pointerService.ProcessHandPosition(new Point(x, y), true);
            });
        }

        private void PopulateUI()
        {
            PoseNameLabel.Text = _pose.Name;
            AccuracyLabel.Text = _averageAccuracy.ToString("F0") + "%";
            HoldTimeLabel.Text = _holdSeconds.ToString() + "s";
            BestScoreLabel.Text = _bestScore.ToString("F0") + "%";
            DifficultyLabel.Text = _pose.Difficulty.ToString();
            int minutes = _totalSessionSeconds / 60;
            int seconds = _totalSessionSeconds % 60;
            TotalTimeLabel.Text = $"{minutes.ToString("D2")}:{seconds.ToString("D2")}";

            // Result icon + feedback based on score
            if (_averageAccuracy >= 80)
            {
                ResultIcon.Text = "🏆";
                FeedbackMessage.Text = "Excellent form! You've mastered this pose.";
                AccuracyLabel.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(34, 197, 94));
            }
            else if (_averageAccuracy >= 60)
            {
                ResultIcon.Text = "👍";
                FeedbackMessage.Text = "Good effort! A little more practice and you'll nail it.";
                AccuracyLabel.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(245, 158, 11));
            }
            else
            {
                ResultIcon.Text = "💪";
                FeedbackMessage.Text = "Keep going! Consistency is the key to improvement.";
                AccuracyLabel.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(239, 68, 68));
            }
        }

        private void Cleanup()
        {
            _kinectManager.SkeletonFrameReady -= OnSkeletonFrameReady;
            _pointerService?.Stop();
            _pointerService?.ClearButtons();
            _pointerService = null;
        }

        private void PracticeAgain_Click(object sender, RoutedEventArgs e)
        {
            Cleanup();
            var session = new SessionView(_kinectManager, _pose);
            session.Show();
            Close();
        }

        private void NewPose_Click(object sender, RoutedEventArgs e)
        {
            Cleanup();
            var selection = new PoseSelectionView(_kinectManager);
            selection.Show();
            Close();
        }

        private void MainMenu_Click(object sender, RoutedEventArgs e)
        {
            Cleanup();
            Application.Current.MainWindow.Show();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            Cleanup();
            base.OnClosed(e);
        }
    }
}
