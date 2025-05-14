using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using RemoteMonitoringApplication.Services;
using RemoteMonitoringApplication.Views;
using static RemoteMonitoringApplication.Services.AuthService;


namespace RemoteMonitoringApplication.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private string _username;
        private string _password;
        private string _errorMessage;
        private bool _isLoggingIn;

        private readonly AuthService _authService;

        public Action NavigateToClientAction { get; set; }

        public LoginViewModel(AuthService authService)
        {
            _authService = authService;
            LoginCommand = new RelayCommand(async () => await LoginAsync(), CanLogin);
        }

        public void Reset()
        {
            Username = string.Empty;
            Password = string.Empty;
            ErrorMessage = string.Empty;
            IsLoggingIn = false;
            ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            _authService.LoginSuccess -= OnLoginSuccess;
            _authService.LoginFailed -= OnLoginFailed;
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set
            {
                _isLoggingIn = value;
                OnPropertyChanged();
                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
            }
        }

        public ICommand LoginCommand { get; }

        private async Task LoginAsync()
        {
            IsLoggingIn = true;
            ErrorMessage = string.Empty;

            _authService.LoginSuccess -= OnLoginSuccess;
            _authService.LoginFailed -= OnLoginFailed;
            _authService.LoginSuccess += OnLoginSuccess;
            _authService.LoginFailed += OnLoginFailed;

            try
            {
                await _authService.LoginAsync(Username, Password);
                // Chờ phản hồi từ WebSocket rồi gọi event LoginSucceeded/LoginFailed
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login request failed: {ex.Message}";
            }
            finally
            {
                IsLoggingIn = false;
  
            }
        }

        private void OnLoginSuccess(LoginMessage res)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsLoggingIn = false;
                
                var session = SessionManager.Instance;
                session.id = res.id;
                session.username = res.username;
                session.email = res.email;
                session.role = res.role;
                // Add token to session manager

                NavigateToClientAction?.Invoke(); // Fixed: Changed EndInvoke to Invoke

            });
        }

        private void OnLoginFailed(string status, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsLoggingIn = false;
                if (status == "error")
                {
                    ErrorMessage = message;
                    MessageBox.Show(message, "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else if (status == "fail")
                {
                    ErrorMessage = message;
                    MessageBox.Show(message, "Login Failed", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
        });
        }

        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && !IsLoggingIn;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public async void Execute(object parameter)
        {
            await _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
