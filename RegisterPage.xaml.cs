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
            _httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:8000") };
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var password = this.PasswordBox.Password;
            var confirmPassword = this.ConfirmPasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                MessageBox.Show("Vui lòng điền đầy đủ các trường.", "Lỗi xác thực");
                return;
            }

            if (username.Length < 3)
            {
                MessageBox.Show("Tên đăng nhập phải có ít nhất 3 ký tự.", "Lỗi xác thực");
                return;
            }

            if (password.Length < 6)
            {
                MessageBox.Show("Mật khẩu phải có ít nhất 6 ký tự.", "Lỗi xác thực");
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Mật khẩu không khớp.", "Lỗi xác thực");
                return;
            }

            // Get selected role from ComboBox
            if (RoleComboBox.SelectedItem is not ComboBoxItem selectedRoleItem || selectedRoleItem.Tag is not string role)
            {
                MessageBox.Show("Vui lòng chọn vai trò.", "Lỗi xác thực");
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
                    
                    string message = $"Đăng ký thành công!\nTên đăng nhập: {username}\nVai trò: {role}\n\nĐang tự động đăng nhập...";
                    MessageBox.Show(message, "Thành công");
                    
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
                        MessageBox.Show("Đăng ký thành công nhưng tự động đăng nhập thất bại. Vui lòng đăng nhập thủ công.", "Thông báo");
                        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                        mainWindow.ShowLoginPage();
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Đăng ký thất bại: {errorContent}", "Lỗi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Đã xảy ra lỗi: {ex.Message}", "Lỗi");
            }
        }

        private void LoginLinkButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.ShowLoginPage();
        }
    }
}