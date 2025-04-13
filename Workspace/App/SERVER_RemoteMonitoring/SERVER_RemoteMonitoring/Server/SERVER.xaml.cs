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

namespace SERVER_RemoteMonitoring.Server
{
    /// <summary>
    /// Interaction logic for SERVER.xaml
    /// </summary>
    public partial class SERVER : Window
    {
        public SERVER()
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


        private void HomeClick_Click(object sender, MouseButtonEventArgs e)
        {
            Home.Visibility = Visibility.Visible;

            UserControl_Table.Visibility = Visibility.Collapsed;
        }
        private void UserControl_Click(object sender, MouseButtonEventArgs e)
        {
            UserControl_Table.Visibility = Visibility.Visible;
            Home.Visibility = Visibility.Collapsed;
        }
        


    }
}
