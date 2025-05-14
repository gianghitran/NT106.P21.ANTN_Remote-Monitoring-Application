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
    /// Interaction logic for Register.xaml
    /// </summary>
    public partial class Register : Window
    {
        private readonly AuthService _auth;
        private readonly ViewModels.RegisterViewModel _viewModel;
        public Register(AuthService authService)
        {
            InitializeComponent();
            _auth = authService;
            _viewModel = new ViewModels.RegisterViewModel(_auth);
            _viewModel.NavigateToLoginAction = () =>
            {
                if (Application.Current.Windows.OfType<Login>().Any())
                {
                    var loginWindow = Application.Current.Windows.OfType<Login>().First();
                    loginWindow.Show();
                    (loginWindow.DataContext as LoginViewModel)?.Reset();
                }
                else
                {
                    Login loginWindow = new Login(_auth);
                    loginWindow.Show();
                    loginWindow.Activate();
                    (loginWindow?.DataContext as LoginViewModel)?.Reset();
                }
                (this.DataContext as RegisterViewModel)?.Reset();
                this.ClearPassword();
                this.Hide();
            };
            this.DataContext = _viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _viewModel.Dispose();

        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void btnRegister_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void navLogin_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            if (Application.Current.Windows.OfType<Login>().Any())
            {
                var loginWindow = Application.Current.Windows.OfType<Login>().First();
                loginWindow.Show();
                (loginWindow.DataContext as LoginViewModel)?.Reset();
            }
            else
            {
                Login loginWindow = new Login(_auth);
                loginWindow.Show();
                loginWindow.Activate();
                (loginWindow.DataContext as LoginViewModel)?.Reset();
            }
            this.ClearPassword();
            this.Hide();
        }

        private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.RegisterViewModel viewModel)
            {
                viewModel.Password = txtPassword.Password;
            }
        }

        private void txtConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.RegisterViewModel viewModel)
            {
                viewModel.ConfirmPassword = txtConfirmPassword.Password;
            }
        }

        public void ClearPassword()
        {
            txtPassword.Clear();
            txtConfirmPassword.Clear();
        }
    }
}
