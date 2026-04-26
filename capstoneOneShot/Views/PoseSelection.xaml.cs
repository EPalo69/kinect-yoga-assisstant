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
        private Button _activeFilter;

        public PoseSelectionView(KinectManager kinectManager)
        {
            InitializeComponent();
            _kinectManager = kinectManager;
            _allPoses = PoseLibrary.GetAllPoses();

            Loaded += (s, e) =>
            {
                SetActiveFilter(FilterAll);
                ShowPoses(_allPoses);
            };
        }

        // ── Filter buttons ───────────────────────────────────────────────
        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            SetActiveFilter(btn);

            var tag = btn.Tag?.ToString();
            var filtered = tag == "All"
                ? _allPoses
                : _allPoses.Where(p => p.Difficulty.ToString() == tag).ToList();

            ShowPoses(filtered);
        }

        private void SetActiveFilter(Button btn)
        {
            // Reset previous
            if (_activeFilter != null)
            {
                _activeFilter.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(17, 24, 39));
                _activeFilter.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(156, 163, 175));
            }

            _activeFilter = btn;
            btn.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(77, 208, 225));
            btn.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Colors.Black);
        }

        private void ShowPoses(List<PoseDefinition> poses)
        {
            PoseList.ItemsSource = poses;
        }

        // ── Pose card click ──────────────────────────────────────────────
        private void PoseCard_Click(object sender, MouseButtonEventArgs e)
        {
            var border = (Border)sender;
            var pose = (PoseDefinition)border.Tag;

            var detailView = new PoseDetailView(_kinectManager, pose);
            detailView.Show();
            Close();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var main = new MainWindow();
            main.Show();
            Close();
        }
    }
}
