using System;
using System.Windows;
using System.Windows.Controls;
using EmployeeManagement.Services;

namespace EmployeeManagement
{
    public partial class LoginPage : UserControl
    {
        public LoginPage()
        {
            InitializeComponent();
            
            // Set focus on email field
            Loaded += (s, e) => EmailTextBox.Focus();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var identifier = EmailTextBox.Text.Trim();
            var password = PasswordBox.Password;

            // Validation
            if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter both email/username and password.", "Validation Error");
                return;
            }

            // Show loading state
            LoginButton.IsEnabled = false;
            LoginButton.Content = "Logging in...";
            LoadingIndicator.Visibility = Visibility.Visible;

            try
            {
                // Use UserSessionService for authentication
                bool loginSuccess = await UserSessionService.LoginAsync(identifier, password);

                if (loginSuccess)
                {
                    // Login successful
                    MessageBox.Show($"Welcome {UserSessionService.CurrentUser?.username}!", "Login Successful");
                    
                    // Get MainWindow and call OnLoginSuccess
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    mainWindow?.OnLoginSuccess();
                }
                else
                {
                    // Login failed (error message already shown in UserSessionService)
                    ResetForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Login Error");
                ResetForm();
            }
            finally
            {
                // Reset loading state
                LoginButton.IsEnabled = true;
                LoginButton.Content = "Login";
                LoadingIndicator.Visibility = Visibility.Hidden;
            }
        }
        private void RegisterLink_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to register page - for admin/manager only
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.ShowRegisterPage();
        }

        private void EmailTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                PasswordBox.Focus();
            }
        }

        private void PasswordBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                LoginButton_Click(sender, e);
            }
        }

        private void ResetForm()
        {
            // Clear password field for security
            PasswordBox.Clear();
            EmailTextBox.Focus();
        }

        #region Demo Login Buttons (for testing different roles)
        private async void AdminDemoButton_Click(object sender, RoutedEventArgs e)
        {
            await DemoLogin("admin@company.com", "admin123");
        }

        private async void ManagerDemoButton_Click(object sender, RoutedEventArgs e)
        {
            await DemoLogin("manager@company.com", "manager123");
        }

        private async void EmployeeDemoButton_Click(object sender, RoutedEventArgs e)
        {
            await DemoLogin("employee@company.com", "employee123");
        }

        private async System.Threading.Tasks.Task DemoLogin(string identifier, string password)
        {
            EmailTextBox.Text = identifier;
            PasswordBox.Password = password;
            
            // Auto-trigger login
            await System.Threading.Tasks.Task.Delay(100); // Small delay for visual feedback
            LoginButton_Click(this, new RoutedEventArgs());
        }
        #endregion
    }
}