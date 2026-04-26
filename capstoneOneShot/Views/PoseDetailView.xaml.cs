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

        public PoseDetailView(KinectManager kinectManager, PoseDefinition pose)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _pose = pose;
            Loaded += (s, e) => PopulateUI();
        }

        private void PopulateUI()
        {
            PoseNameBig.Text = _pose.Name;
            PoseNameLabel.Text = _pose.Name;
            DifficultyLabel.Text = _pose.Difficulty.ToString();
            DescriptionLabel.Text = _pose.Description;
            HoldTimeLabel.Text = $"{_pose.HoldSeconds}s";
            JointCountLabel.Text = _pose.Rules.Count.ToString();
            RulesList.ItemsSource = _pose.Rules;

            // Color difficulty badge
            if (_pose.Difficulty == DifficultyLevel.Beginner)
                DifficultyBadge.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(6, 78, 59));
            else if (_pose.Difficulty == DifficultyLevel.Intermediate)
                DifficultyBadge.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(92, 52, 10));
            else if (_pose.Difficulty == DifficultyLevel.Advanced)
                DifficultyBadge.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(76, 5, 25));
            else
                DifficultyBadge.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(31, 41, 55));
        }

        private void BeginButton_Click(object sender, RoutedEventArgs e)
        {
            // Pass a single-pose session
            var poses = new System.Collections.Generic.List<PoseDefinition> { _pose };
            var session = new SessionView(_kinectManager, _pose);
            session.Show();
            Close();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var selection = new PoseSelectionView(_kinectManager);
            selection.Show();
            Close();
        }
    }
}
