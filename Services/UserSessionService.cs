using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace EmployeeManagement.Services
{
    /// <summary>
    /// Service để quản lý authentication và user session
    /// </summary>
    public static class UserSessionService
    {
        private static UserInfo? _currentUser = null;
        private static string? _accessToken = null;
        
        public static UserInfo? CurrentUser => _currentUser;
        public static string? AccessToken => _accessToken;
        public static bool IsAuthenticated => _currentUser != null && !string.IsNullOrEmpty(_accessToken);

        private static readonly string _backendUrl = "http://127.0.0.1:8000";

        #region User Models
        public class UserInfo
        {
            public int id { get; set; }
            public string username { get; set; } = "";
            public string role { get; set; } = "";
            public int? employee_id { get; set; }
            public bool is_active { get; set; }
            public List<string> permissions { get; set; } = new();
            public MenuPermissions menu_permissions { get; set; } = new();
        }

        public class MenuPermissions
        {
            public bool dashboard { get; set; }
            public bool employees { get; set; }
            public bool users { get; set; }
            public bool departments { get; set; }
            public bool positions { get; set; }
            public bool salaries { get; set; }
            public bool attendances { get; set; }
            public bool leaves { get; set; }
            public bool can_create_employee { get; set; }
            public bool can_edit_employee { get; set; }
            public bool can_delete_employee { get; set; }
            public bool can_approve_leave { get; set; }
            public bool can_manage_departments { get; set; }
            public bool can_manage_positions { get; set; }
        }

        public class LoginRequest
        {
            public string identifier { get; set; } = "";
            public string password { get; set; } = "";
        }

        public class LoginResponse
        {
            public string access_token { get; set; } = "";
            public string token_type { get; set; } = "";
            public UserInfo user { get; set; } = new();
        }
        #endregion

        #region Authentication Methods
        public static async Task<bool> LoginAsync(string identifier, string password)
        {
            try
            {
                var loginRequest = new LoginRequest { identifier = identifier, password = password };
                var json = JsonSerializer.Serialize(loginRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var httpClient = new HttpClient();
                var response = await httpClient.PostAsync($"{_backendUrl}/api/v1/auth/login", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    if (loginResponse != null)
                    {
                        _currentUser = loginResponse.user;
                        _accessToken = loginResponse.access_token;
                        return true;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Login failed: {errorContent}", "Login Error");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during login: {ex.Message}", "Login Error");
            }

            return false;
        }

        public static void Logout()
        {
            _currentUser = null;
            _accessToken = null;
        }

        public static async Task<UserInfo?> GetCurrentUserProfileAsync()
        {
            if (!IsAuthenticated) return null;

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/auth/me");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var userProfile = JsonSerializer.Deserialize<UserInfo>(responseContent, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    if (userProfile != null)
                    {
                        _currentUser = userProfile;
                        return userProfile;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting user profile: {ex.Message}", "Profile Error");
            }

            return null;
        }
        #endregion

        #region Permission Methods
        public static bool HasPermission(string permission)
        {
            return _currentUser?.permissions?.Contains(permission) == true;
        }

        public static bool CanViewMenu(string menuItem)
        {
            if (_currentUser?.menu_permissions == null) return false;

            return menuItem.ToLower() switch
            {
                "dashboard" => _currentUser.menu_permissions.dashboard,
                "employees" => _currentUser.menu_permissions.employees,
                "users" => _currentUser.menu_permissions.users,
                "departments" => _currentUser.menu_permissions.departments,
                "positions" => _currentUser.menu_permissions.positions,
                "salaries" => _currentUser.menu_permissions.salaries,
                "attendances" => _currentUser.menu_permissions.attendances,
                "leaves" => _currentUser.menu_permissions.leaves,
                _ => false
            };
        }

        public static bool CanCreateEmployee => _currentUser?.menu_permissions?.can_create_employee == true;
        public static bool CanEditEmployee => _currentUser?.menu_permissions?.can_edit_employee == true;
        public static bool CanDeleteEmployee => _currentUser?.menu_permissions?.can_delete_employee == true;
        public static bool CanApproveLeave => _currentUser?.menu_permissions?.can_approve_leave == true;
        public static bool CanManageDepartments => _currentUser?.menu_permissions?.can_manage_departments == true;
        public static bool CanManagePositions => _currentUser?.menu_permissions?.can_manage_positions == true;

        public static bool IsAdmin => _currentUser?.role == "admin";
        public static bool IsManager => _currentUser?.role == "manager";
        public static bool IsEmployee => _currentUser?.role == "employee";
        #endregion

        #region HTTP Helper
        public static HttpClient GetAuthenticatedHttpClient()
        {
            var httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(_accessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            }
            return httpClient;
        }
        #endregion

        #region Role Display
        public static string GetRoleDisplayName(string role)
        {
            return role switch
            {
                "admin" => "Administrator",
                "manager" => "Manager",
                "employee" => "Employee",
                _ => "Unknown"
            };
        }

        public static string GetWelcomeMessage()
        {
            if (_currentUser == null) return "Welcome";
            var roleDisplay = GetRoleDisplayName(_currentUser.role);
            return $"Welcome, {_currentUser.username} ({roleDisplay})";
        }
        #endregion
    }
}