using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EmployeeManagement.Services;

namespace EmployeeManagement
{
    public partial class UsersPage : Page
    {
        private readonly string _backendUrl = "http://127.0.0.1:8000";
        private List<UserDto> _allUsers = new List<UserDto>();

        public UsersPage()
        {
            InitializeComponent();
            CheckPermissionsAndSetupUI();
            LoadUsers();
        }

        #region Data Models
        public class UserDto
        {
            public int id { get; set; }
            public string username { get; set; } = "";
            public string role { get; set; } = "";
            public bool is_active { get; set; }
            public int? employee_id { get; set; }
            public string? employee_name { get; set; }
            public string? employee_code { get; set; }
            public string? employee_email { get; set; }
            public DateTime? last_login { get; set; }
            public DateTime? created_at { get; set; }
            public int RowNumber { get; set; }
        }

        public class UserListResponse
        {
            public int total { get; set; }
            public int page { get; set; }
            public int page_size { get; set; }
            public List<UserDto> users { get; set; } = new List<UserDto>();
        }
        #endregion

        private void CheckPermissionsAndSetupUI()
        {
            if (!UserSessionService.IsAuthenticated)
            {
                MessageBox.Show("You need to be logged in to view users.", "Authentication Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Only Admin can access Users page
            if (!UserSessionService.IsAdmin)
            {
                MessageBox.Show("Only administrators can access user management.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowDashboard();
                return;
            }
        }

        #region Data Loading
        private async void LoadUsers(string? roleFilter = null)
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                
                string url = $"{_backendUrl}/api/v1/users/";
                if (!string.IsNullOrEmpty(roleFilter))
                {
                    url += $"?role={roleFilter}";
                }

                var response = await httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<UserListResponse>(jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (result != null)
                    {
                        _allUsers = result.users;
                        ApplySearchFilter();
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    MessageBox.Show("You don't have permission to view users.", "Access Denied",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to load users: {error}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to server: {ex.Message}", "Connection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplySearchFilter()
        {
            var searchTerm = SearchTextBox.Text?.Trim().ToLower() ?? "";
            
            var filtered = _allUsers.Where(u =>
                string.IsNullOrEmpty(searchTerm) ||
                u.username.ToLower().Contains(searchTerm) ||
                (u.employee_name?.ToLower().Contains(searchTerm) ?? false) ||
                (u.employee_code?.ToLower().Contains(searchTerm) ?? false)
            ).ToList();

            for (int i = 0; i < filtered.Count; i++)
                filtered[i].RowNumber = i + 1;
            UsersDataGrid.ItemsSource = filtered;
        }
        #endregion

        #region Event Handlers
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void RoleFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoleFilterComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var roleFilter = selectedItem.Tag?.ToString();
                LoadUsers(string.IsNullOrEmpty(roleFilter) ? null : roleFilter);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
            RoleFilterComboBox.SelectedIndex = 0;
            LoadUsers();
        }

        private void AddUserButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddUserDialog();
            if (dialog.ShowDialog() == true)
            {
                CreateUser(dialog.Username, dialog.Password, dialog.Role);
            }
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is UserDto user)
            {
                var dialog = new ResetPasswordDialog(user.username);
                if (dialog.ShowDialog() == true)
                {
                    ResetUserPassword(user.id, dialog.NewPassword);
                }
            }
        }

        private async void ToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is UserDto user)
            {
                // Prevent admin from deactivating themselves
                if (user.id == UserSessionService.CurrentUser?.id)
                {
                    MessageBox.Show("You cannot deactivate your own account.", "Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var action = user.is_active ? "deactivate" : "activate";
                var result = MessageBox.Show(
                    $"Are you sure you want to {action} user '{user.username}'?",
                    "Confirm",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await ToggleUserStatus(user.id, !user.is_active);
                }
            }
        }

        private async void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is UserDto user)
            {
                // Prevent admin from deleting themselves
                if (user.id == UserSessionService.CurrentUser?.id)
                {
                    MessageBox.Show("You cannot delete your own account.", "Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to permanently delete user '{user.username}'?\n\nThis action cannot be undone!",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await DeleteUser(user.id);
                }
            }
        }
        #endregion

        #region API Operations
        private async void CreateUser(string username, string password, string role)
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                
                var requestBody = new { username, password, role };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{_backendUrl}/api/v1/users/admin-manager", content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"User '{username}' created successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadUsers();
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to create user: {error}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ResetUserPassword(int userId, string newPassword)
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();

                var response = await httpClient.PostAsync(
                    $"{_backendUrl}/api/v1/users/{userId}/reset-password?new_password={Uri.EscapeDataString(newPassword)}",
                    null);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Password reset successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to reset password: {error}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ToggleUserStatus(int userId, bool newStatus)
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                
                var requestBody = new { is_active = newStatus };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PutAsync($"{_backendUrl}/api/v1/users/{userId}", content);

                if (response.IsSuccessStatusCode)
                {
                    var status = newStatus ? "activated" : "deactivated";
                    MessageBox.Show($"User {status} successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadUsers();
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to update user status: {error}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteUser(int userId)
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.DeleteAsync($"{_backendUrl}/api/v1/users/{userId}");

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("User deleted successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadUsers();
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to delete user: {error}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DataGrid_LoadingRow(object sender, System.Windows.Controls.DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
            if (e.Row.DataContext is UserDto user)
            {
                user.RowNumber = e.Row.GetIndex() + 1;
            }
        }
        #endregion
    }

    #region Dialog Windows
    /// <summary>
    /// Dialog for adding new Admin/Manager user
    /// </summary>
    public class AddUserDialog : Window
    {
        public string Username { get; private set; } = "";
        public string Password { get; private set; } = "";
        public string Role { get; private set; } = "manager";

        private TextBox _usernameTextBox;
        private PasswordBox _passwordTextBox;
        private ComboBox _roleComboBox;

        public AddUserDialog()
        {
            Title = "Add New User";
            Width = 400;
            Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = Application.Current.MainWindow;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Title
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Username label
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Username textbox
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: Password label
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4: Password textbox
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 5: Role label
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 6: Role combobox
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 7: Spacer
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 8: Buttons

            // Title
            var titleText = new TextBlock
            {
                Text = "➕ Create New Admin/Manager",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(titleText, 0);
            grid.Children.Add(titleText);

            // Username
            var usernameLabel = new TextBlock { Text = "Username:", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(usernameLabel, 1);
            grid.Children.Add(usernameLabel);

            _usernameTextBox = new TextBox { Height = 30, Padding = new Thickness(5), Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(_usernameTextBox, 2);
            grid.Children.Add(_usernameTextBox);

            // Password
            var passwordLabel = new TextBlock { Text = "Password (min 6 characters):", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(passwordLabel, 3);
            grid.Children.Add(passwordLabel);

            _passwordTextBox = new PasswordBox { Height = 30, Padding = new Thickness(5), Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(_passwordTextBox, 4);
            grid.Children.Add(_passwordTextBox);

            // Role
            var roleLabel = new TextBlock { Text = "Role:", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(roleLabel, 5);
            grid.Children.Add(roleLabel);

            _roleComboBox = new ComboBox { Height = 30, Margin = new Thickness(0, 0, 0, 10) };
            _roleComboBox.Items.Add(new ComboBoxItem { Content = "Administrator", Tag = "admin" });
            _roleComboBox.Items.Add(new ComboBoxItem { Content = "Manager", Tag = "manager" });
            _roleComboBox.SelectedIndex = 1; // Default to Manager
            Grid.SetRow(_roleComboBox, 6);
            grid.Children.Add(_roleComboBox);

            // Buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            
            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
            buttonPanel.Children.Add(cancelButton);

            var createButton = new Button
            {
                Content = "Create",
                Width = 80,
                Height = 30,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27AE60")),
                Foreground = System.Windows.Media.Brushes.White
            };
            createButton.Click += CreateButton_Click;
            buttonPanel.Children.Add(createButton);

            Grid.SetRow(buttonPanel, 8);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            Username = _usernameTextBox.Text.Trim();
            Password = _passwordTextBox.Password;
            
            if (string.IsNullOrEmpty(Username) || Username.Length < 3)
            {
                MessageBox.Show("Username must be at least 3 characters.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(Password) || Password.Length < 6)
            {
                MessageBox.Show("Password must be at least 6 characters.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_roleComboBox.SelectedItem is ComboBoxItem selectedRole)
            {
                Role = selectedRole.Tag?.ToString() ?? "manager";
            }

            DialogResult = true;
            Close();
        }
    }

    /// <summary>
    /// Dialog for resetting user password
    /// </summary>
    public class ResetPasswordDialog : Window
    {
        public string NewPassword { get; private set; } = "";
        private PasswordBox _passwordBox;
        private PasswordBox _confirmPasswordBox;

        public ResetPasswordDialog(string username)
        {
            Title = "Reset Password";
            Width = 350;
            Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = Application.Current.MainWindow;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Title
            var titleText = new TextBlock
            {
                Text = $"Reset Password for: {username}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(titleText, 0);
            grid.Children.Add(titleText);

            // New Password
            var passwordLabel = new TextBlock { Text = "New Password (min 6 characters):", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(passwordLabel, 1);
            grid.Children.Add(passwordLabel);

            _passwordBox = new PasswordBox { Height = 30, Padding = new Thickness(5), Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(_passwordBox, 2);
            grid.Children.Add(_passwordBox);

            // Confirm Password
            var confirmLabel = new TextBlock { Text = "Confirm Password:", Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(confirmLabel, 3);
            grid.Children.Add(confirmLabel);

            _confirmPasswordBox = new PasswordBox { Height = 30, Padding = new Thickness(5), Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(_confirmPasswordBox, 4);
            grid.Children.Add(_confirmPasswordBox);

            // Buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            
            var cancelButton = new Button { Content = "Cancel", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
            buttonPanel.Children.Add(cancelButton);

            var resetButton = new Button
            {
                Content = "Reset",
                Width = 80,
                Height = 30,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9B59B6")),
                Foreground = System.Windows.Media.Brushes.White
            };
            resetButton.Click += ResetButton_Click;
            buttonPanel.Children.Add(resetButton);

            Grid.SetRow(buttonPanel, 6);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var password = _passwordBox.Password;
            var confirmPassword = _confirmPasswordBox.Password;

            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                MessageBox.Show("Password must be at least 6 characters.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Passwords do not match.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewPassword = password;
            DialogResult = true;
            Close();
        }
    }
    #endregion
}