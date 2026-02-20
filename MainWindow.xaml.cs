using System.Windows;
using System.Windows.Controls;
using EmployeeManagement.Services;
using System.Net.Http;
using System;

namespace EmployeeManagement
{
    public partial class MainWindow : Window
    {
        private readonly string _backendUrl = "http://127.0.0.1:8000";
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Check backend connection on startup
            CheckBackendConnection();
            
            ShowLoginPage();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            Logout();
        }

        #region Backend Connection Check
        private async void CheckBackendConnection()
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/health_check");
                // Backend will log this request via middleware
            }
            catch (Exception)
            {
                // Connection failed - user will see errors when using features
            }
        }
        #endregion

        #region Authentication & Session Management
        public void OnLoginSuccess()
        {
            ShowNavigation();
            UpdateUserInfo();
            UpdateMenuVisibility();
            ShowDashboard();
        }

        private void UpdateUserInfo()
        {
            if (UserSessionService.IsAuthenticated && UserSessionService.CurrentUser != null)
            {
                UsernameLabel.Text = UserSessionService.GetWelcomeMessage();
                UserInfoPanel.Visibility = Visibility.Visible;
            }
        }

        private void UpdateMenuVisibility()
        {
            if (!UserSessionService.IsAuthenticated) return;

            // Update menu visibility based on permissions
            DashboardButton.Visibility = UserSessionService.CanViewMenu("dashboard") ? Visibility.Visible : Visibility.Collapsed;
            DepartmentsButton.Visibility = UserSessionService.CanViewMenu("departments") ? Visibility.Visible : Visibility.Collapsed;
            PositionsButton.Visibility = UserSessionService.CanViewMenu("positions") ? Visibility.Visible : Visibility.Collapsed;
            EmployeesButton.Visibility = UserSessionService.CanViewMenu("employees") ? Visibility.Visible : Visibility.Collapsed;
            UsersButton.Visibility = UserSessionService.CanViewMenu("users") ? Visibility.Visible : Visibility.Collapsed;
            SalariesButton.Visibility = UserSessionService.CanViewMenu("salaries") ? Visibility.Visible : Visibility.Collapsed;
            AttendancesButton.Visibility = UserSessionService.CanViewMenu("attendances") ? Visibility.Visible : Visibility.Collapsed;
            LeavesButton.Visibility = UserSessionService.CanViewMenu("leaves") ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Logout()
        {
            UserSessionService.Logout();
            HideNavigation();
            ShowLoginPage();
        }
        #endregion

        #region Navigation Control
        private void ShowNavigation()
        {
            SideNavPanel.Visibility = Visibility.Visible;
            UserInfoPanel.Visibility = Visibility.Visible;
        }

        private void HideNavigation()
        {
            SideNavPanel.Visibility = Visibility.Collapsed;
            UserInfoPanel.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region Authentication Pages
        public void ShowLoginPage()
        {
            HideNavigation();
            MainFrame.Navigate(new LoginPage());
        }

        public void ShowRegisterPage()
        {
            HideNavigation();
            MainFrame.Navigate(new RegisterPage());
        }
        #endregion

        #region Main Application Pages
        public void ShowDashboard()
        {
            if (!CheckPermissionAndNavigate("dashboard")) return;
            MainFrame.Navigate(new DashboardPage());
        }

        public void ShowDepartmentsPage()
        {
            if (!CheckPermissionAndNavigate("departments")) return;
            MainFrame.Navigate(new DepartmentsPage());
        }

        public void ShowPositionsPage()
        {
            if (!CheckPermissionAndNavigate("positions")) return;
            MainFrame.Navigate(new PositionsPage());
        }

        public void ShowEmployeesPage()
        {
            if (!CheckPermissionAndNavigate("employees")) return;
            MainFrame.Navigate(new EmployeesPage());
        }

        public void ShowUsersPage()
        {
            if (!CheckPermissionAndNavigate("users")) return;
            MainFrame.Navigate(new UsersPage());
        }

        public void ShowSalariesPage()
        {
            if (!CheckPermissionAndNavigate("salaries")) return;
            MainFrame.Navigate(new SalariesPage());
        }

        public void ShowAttendancesPage()
        {
            if (!CheckPermissionAndNavigate("attendances")) return;
            MainFrame.Navigate(new AttendancesPage());
        }

        public void ShowLeavesPage()
        {
            if (!CheckPermissionAndNavigate("leaves")) return;
            MainFrame.Navigate(new LeavesPage());
        }
        #endregion

        #region Permission Checking
        private bool CheckPermissionAndNavigate(string menuItem)
        {
            if (!UserSessionService.IsAuthenticated)
            {
                MessageBox.Show("Please login first.", "Authentication Required");
                ShowLoginPage();
                return false;
            }

            if (!UserSessionService.CanViewMenu(menuItem))
            {
                MessageBox.Show($"You don't have permission to access {menuItem}.", "Access Denied");
                return false;
            }

            return true;
        }
        #endregion

        #region Navigation Event Handlers
        private void NavigateToDashboard(object sender, RoutedEventArgs e)
        {
            ShowDashboard();
        }

        private void NavigateToDepartments(object sender, RoutedEventArgs e)
        {
            ShowDepartmentsPage();
        }

        private void NavigateToPositions(object sender, RoutedEventArgs e)
        {
            ShowPositionsPage();
        }

        private void NavigateToEmployees(object sender, RoutedEventArgs e)
        {
            ShowEmployeesPage();
        }

        private void NavigateToUsers(object sender, RoutedEventArgs e)
        {
            ShowUsersPage();
        }

        private void NavigateToSalaries(object sender, RoutedEventArgs e)
        {
            ShowSalariesPage();
        }

        private void NavigateToAttendances(object sender, RoutedEventArgs e)
        {
            ShowAttendancesPage();
        }

        private void NavigateToLeaves(object sender, RoutedEventArgs e)
        {
            ShowLeavesPage();
        }
        #endregion
    }
}
