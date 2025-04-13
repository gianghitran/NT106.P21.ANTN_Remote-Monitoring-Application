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

namespace RemoteMonitoringApplication.Client
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Client: Window
    {
        public Client()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void lblScreen_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Visible;
            CPU.Visibility = Visibility.Collapsed;
            GPU.Visibility = Visibility.Collapsed;
            RAM.Visibility = Visibility.Collapsed;
            Disk.Visibility = Visibility.Collapsed;
            Network.Visibility = Visibility.Collapsed;
        }

        private void lblCPU_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Collapsed;
            CPU.Visibility = Visibility.Visible;
            GPU.Visibility = Visibility.Collapsed;
            RAM.Visibility = Visibility.Collapsed;
            Disk.Visibility = Visibility.Collapsed;
            Network.Visibility = Visibility.Collapsed;
        }

        private void lblGPU_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Collapsed;
            CPU.Visibility = Visibility.Collapsed;
            GPU.Visibility = Visibility.Visible;
            RAM.Visibility = Visibility.Collapsed;
            Disk.Visibility = Visibility.Collapsed;
            Network.Visibility = Visibility.Collapsed;
        }

        private void lblRAM_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Collapsed;
            CPU.Visibility = Visibility.Collapsed;
            GPU.Visibility = Visibility.Collapsed;
            RAM.Visibility = Visibility.Visible;
            Disk.Visibility = Visibility.Collapsed;
            Network.Visibility = Visibility.Collapsed;
        }

        private void lblDisk_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Collapsed;
            CPU.Visibility = Visibility.Collapsed;
            GPU.Visibility = Visibility.Collapsed;
            RAM.Visibility = Visibility.Collapsed;
            Disk.Visibility = Visibility.Visible;
            Network.Visibility = Visibility.Collapsed;
        }

        private void lblNetwork_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Screen.Visibility = Visibility.Collapsed;
            CPU.Visibility = Visibility.Collapsed;
            GPU.Visibility = Visibility.Collapsed;
            RAM.Visibility = Visibility.Collapsed;
            Disk.Visibility = Visibility.Collapsed;
            Network.Visibility = Visibility.Visible;
        }

        private void btnHome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Home_1.Visibility = Visibility.Visible;
            Home_2.Visibility = Visibility.Visible;
            Remote.Visibility = Visibility.Collapsed;
            
        }

        private void btnRemote_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Home_1.Visibility = Visibility.Collapsed;
            Home_2.Visibility = Visibility.Collapsed;
            Remote.Visibility = Visibility.Visible;
        }

        private void btnHistory_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Home_1.Visibility = Visibility.Visible;
            Home_2.Visibility = Visibility.Visible;
            Remote.Visibility = Visibility.Collapsed;
        }
    }
}
