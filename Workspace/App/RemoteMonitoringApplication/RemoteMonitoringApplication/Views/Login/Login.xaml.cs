using RemoteMonitoringApplication.Services;
using RemoteMonitoringApplication.ViewModels;
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

namespace RemoteMonitoringApplication.Views
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        private readonly AuthService _auth;
        public Login(AuthService auth)
        {
            InitializeComponent();
            _auth = auth;
            var viewModel = new ViewModels.LoginViewModel(_auth);
            viewModel.NavigateToClientAction = () =>
            {
                Client clientWindow = new Client();
                clientWindow.Show();
                clientWindow.Activate();
                (this.DataContext as ViewModels.LoginViewModel)?.Reset();
                this.ClearPassword();
                this.Hide();
            };
            
            this.DataContext = viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (DataContext is ViewModels.LoginViewModel viewModel)
            {
                viewModel.Dispose();
            }
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

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {

        }

        private void navRegister_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Application.Current.Windows.OfType<Register>().Any())
            {
                var registerWindow = Application.Current.Windows.OfType<Register>().First();
                registerWindow.Show();
                (registerWindow.DataContext as RegisterViewModel)?.Reset();
            }
            else
            {
                Register registerWindow = new Register(_auth);
                registerWindow.Show();
                registerWindow.Activate();
                (registerWindow.DataContext as RegisterViewModel)?.Reset();
            }
            this.ClearPassword();
            this.Hide();
        }

        private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LoginViewModel viewModel)
            {
                viewModel.Password = txtPassword.Password;
            }
        }

        public void ClearPassword()
        {
            txtPassword.Clear();
        }
    }
}
