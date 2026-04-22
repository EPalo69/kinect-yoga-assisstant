using capstoneOneShot.Services;
using capstoneOneShot.Views;
using Microsoft.Kinect;
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
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace capstoneOneShot
{
    public partial class MainWindow : Window
    {
        private KinectManager _kinectManager;

        public MainWindow()
        {
            InitializeComponent();
            _kinectManager = new KinectManager();

            bool connected = _kinectManager.Initialize();

            if (!connected)
            {
                MessageBox.Show("Kinect not found. Please plug it in and restart.",
                                "Sensor Not Found",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }

            // Sensor is connected — launch the ROM test
            var romView = new ROMTestView(_kinectManager);
            bool? result = romView.ShowDialog();

            if (result == true)
            {
                // ROM test completed — open the session with assigned difficulty
                var sessionView = new SessionView(_kinectManager, romView.ResultDifficulty);
                sessionView.Show();
                this.Hide();
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _kinectManager?.Shutdown();
            base.OnClosed(e);
        }
    }
}
