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

        public PoseSelectionView(KinectManager kinectManager)
        {
            InitializeComponent();
            _kinectManager = kinectManager;

            // Use ROM-filtered poses if available, otherwise all poses
            _allPoses = UserSession.HasCompletedROM
                ? PoseLibrary.GetPosesForUser(UserSession.ROMProfile)
                : PoseLibrary.GetAllPoses();

            Loaded += (s, e) =>
            {
                SetActiveFilter(FilterAll);
                ShowPoses(_allPoses);
                ShowROMBanner();
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

        // ── Filter buttons ───────────────────────────────────────────────
        private void Filter_Click(object sender, MouseButtonEventArgs e)
        {
            var btn = (Grid)sender;
            SetActiveFilter(btn);

            var tag = btn.Tag?.ToString();
            var filtered = tag == "All"
                ? _allPoses
                : _allPoses.Where(p => p.Difficulty.ToString() == tag).ToList();

            ShowPoses(filtered);
        }

        private void SetActiveFilter(Grid btn)
        {
            // Reset previous
            if (_activeFilter != null)
            {
                var bg = _activeFilter.Children.OfType<Ellipse>().FirstOrDefault(x => x.Name.StartsWith("Bg_"));
                var txt = _activeFilter.Children.OfType<TextBlock>().FirstOrDefault(x => x.Name.StartsWith("Txt_"));
                if (bg != null) bg.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(17, 24, 39));
                if (txt != null) txt.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));
            }

            _activeFilter = btn;
            var bgNew = btn.Children.OfType<Ellipse>().FirstOrDefault(x => x.Name.StartsWith("Bg_"));
            var txtNew = btn.Children.OfType<TextBlock>().FirstOrDefault(x => x.Name.StartsWith("Txt_"));
            if (bgNew != null) bgNew.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(77, 208, 225));
            if (txtNew != null) txtNew.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
        }

        private void ShowPoses(List<PoseDefinition> poses)
        {
            PoseList.ItemsSource = poses;
        }

        // ── Pose card click ──────────────────────────────────────────────
        private void PoseCard_Click(object sender, MouseButtonEventArgs e)
        {
            var grid = (Grid)sender;
            var pose = (PoseDefinition)grid.Tag;

            var detailView = new PoseDetailView(_kinectManager, pose);
            detailView.Show();
            Close();
        }

        private void BackButton_Click(object sender, MouseButtonEventArgs e)
        {
            var main = new MainWindow();
            main.Show();
            Close();
        }
    }
}
