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
            var password = this.PasswordBox.Password;
            var confirmPassword = this.ConfirmPasswordBox.Password;

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

            // Get selected role from ComboBox
            if (RoleComboBox.SelectedItem is not ComboBoxItem selectedRoleItem || selectedRoleItem.Tag is not string role)
            {
                MessageBox.Show("Please select a role.", "Validation Error");
                return;
            }

            var registerData = new { username, password, role };
            var content = new StringContent(JsonSerializer.Serialize(registerData), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("/api/v1/auth/register", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    string message = $"Registration successful!\nUsername: {username}\nRole: {role}\n\nLogging you in automatically...";
                    MessageBox.Show(message, "Registration Success");
                    
                    // Auto login after successful registration using username
                    bool loginSuccess = await Services.UserSessionService.LoginAsync(username, password);
                    if (loginSuccess)
                    {
                        UsernameTextBox.Clear();
                        this.PasswordBox.Clear();
                        this.ConfirmPasswordBox.Clear();
                        RoleComboBox.SelectedIndex = 0; // Reset to Admin
                        
                        // Call OnLoginSuccess to setup navigation, user info, and show dashboard
                        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                        mainWindow.OnLoginSuccess();
                    }
                    else
                    {
                        MessageBox.Show("Registration successful but auto-login failed. Please login manually.", "Info");
                        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                        mainWindow.ShowLoginPage();
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Registration failed: {errorContent}", "Registration Error");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error");
            }
        }

        private void LoginLinkButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.ShowLoginPage();
        }
    }
}