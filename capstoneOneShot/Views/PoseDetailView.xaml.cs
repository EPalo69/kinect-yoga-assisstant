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
            var poses = new System.Collections.Generic.List<PoseDefinition> { _pose };
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
    }
}
