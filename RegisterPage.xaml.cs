using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace EmployeeManagement
{
    public partial class RegisterPage : UserControl
    {
        private readonly HttpClient _httpClient;

        public RegisterPage()
        {
            InitializeComponent();
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:8000") };
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                MessageBox.Show("Please fill all fields.", "Validation Error");
                return;
            }

            if (username.Length < 3)
            {
                MessageBox.Show("Username must be at least 3 characters.", "Validation Error");
                return;
            }

            if (password.Length < 6)
            {
                MessageBox.Show("Password must be at least 6 characters.", "Validation Error");
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Passwords do not match.", "Validation Error");
                return;
            }

            var registerData = new { username, password };
            var content = new StringContent(JsonSerializer.Serialize(registerData), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("/api/v1/auth/register", content);
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Registration successful! Please login with your account.");
                    UsernameTextBox.Clear();
                    PasswordBox.Clear();
                    ConfirmPasswordBox.Clear();
                    
                    // Navigate to Login page
                    MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                    mainWindow.ShowLoginPage();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Registration failed: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private void LoginLinkButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.ShowLoginPage();
        }
    }
}