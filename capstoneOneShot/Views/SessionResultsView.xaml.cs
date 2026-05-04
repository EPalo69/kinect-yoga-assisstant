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

        public SessionResultsView(KinectManager kinectManager, PoseDefinition pose)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _pose = pose;
        }

        public void SetResults(double averageAccuracy, double bestScore, int holdSeconds, int totalSessionSeconds)
        {
            _averageAccuracy = averageAccuracy;
            _bestScore = bestScore;
            _holdSeconds = holdSeconds;
            _totalSessionSeconds = totalSessionSeconds; // ★
            Loaded += (s, e) => PopulateUI();
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

        private void PracticeAgain_Click(object sender, RoutedEventArgs e)
        {
            var session = new SessionView(_kinectManager, _pose);
            session.Show();
            Close();
        }

        private void NewPose_Click(object sender, RoutedEventArgs e)
        {
            var selection = new PoseSelectionView(_kinectManager);
            selection.Show();
            Close();
        }

        private void MainMenu_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.Show();
            Close();
        }
    }
}
