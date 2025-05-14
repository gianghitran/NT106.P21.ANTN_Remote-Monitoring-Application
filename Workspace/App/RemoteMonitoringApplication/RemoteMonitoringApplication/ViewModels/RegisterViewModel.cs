using RemoteMonitoringApplication.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace RemoteMonitoringApplication.ViewModels
{
    public class RegisterViewModel : INotifyPropertyChanged
    {
        private string _username;
        private string _email;
        private string _password;
        private string _confirmPassword;
        private string _errorMessage;
        private bool _isRegistering;

        private readonly AuthService _authService;

        public Action NavigateToLoginAction { get; set; }

        public RegisterViewModel(AuthService authService)
        {
            _authService = authService;
            _authService.RegisterSuccess += OnRegisterSuccess;
            _authService.RegisterFailed += OnRegisterFailed;
            RegisterCommand = new RelayCommand(async () => await RegisterAsync(), CanRegister);
        }

        public void Reset()
        {
            Username = string.Empty;
            Email = string.Empty;
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            ErrorMessage = string.Empty;
            IsRegistering = false;
        }

        public void Dispose()
        {
            _authService.RegisterSuccess -= OnRegisterSuccess;
            _authService.RegisterFailed -= OnRegisterFailed;
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
                ((RelayCommand)RegisterCommand).RaiseCanExecuteChanged();
            }
        }

        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
                ((RelayCommand)RegisterCommand).RaiseCanExecuteChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                ((RelayCommand)RegisterCommand).RaiseCanExecuteChanged();
            }
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                _confirmPassword = value;
                OnPropertyChanged();
                ((RelayCommand)RegisterCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsRegistering
        {
            get => _isRegistering;
            set
            {
                _isRegistering = value;
                OnPropertyChanged();
                ((RelayCommand)RegisterCommand).RaiseCanExecuteChanged();
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

        public ICommand RegisterCommand { get; }

        private async Task RegisterAsync()
        {
            IsRegistering = true;
            ErrorMessage = string.Empty;
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                IsRegistering = false;
                return;
            }
            try
            {
                await _authService.RegisterAsync(Username, Email, Password);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Registration request failed: {ex.Message}";
            }
            finally
            {
                IsRegistering = false;
            }
        }

        private void OnRegisterSuccess()
        {
            // Handle successful registration (e.g., navigate to login page)
            //Application.Current.Dispatcher.Invoke(() =>
            //{
                
            //    NavigateToLoginAction?.Invoke();
            //});
            IsRegistering = false;
            MessageBox.Show("Registration successful! Please log in.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnRegisterFailed(string status, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsRegistering = false;
                if (status == "error")
                {
                    MessageBox.Show(message, "Registration Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else if (status == "fail")
                {
                    MessageBox.Show(message, "Registration Failed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        }

        private bool CanRegister()
        {
            return !IsRegistering && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password) && Password == ConfirmPassword;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
}
