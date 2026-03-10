using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using EmployeeManagement.Services;

namespace EmployeeManagement
{
    public partial class DashboardPage : Page
    {
        private readonly string baseUrl = "http://127.0.0.1:8000/api/v1";

        public DashboardPage()
        {
            InitializeComponent();
            CheckAuthenticationAndLoadData();
        }

        private async void CheckAuthenticationAndLoadData()
        {
            if (!UserSessionService.IsAuthenticated)
            {
                System.Windows.MessageBox.Show("You need to be logged in to view dashboard.", "Authentication Required",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            await LoadDashboardData();
        }

        private async Task LoadDashboardData()
        {
            try
            {
                await LoadStatistics();
            }
            catch (Exception ex)
            {
                // Handle any errors gracefully
                System.Windows.MessageBox.Show($"Error loading dashboard data: {ex.Message}", 
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        private async Task LoadStatistics()
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                
                // Load all metrics from new dashboard API
                var response = await httpClient.GetAsync($"{baseUrl}/statistics/dashboard");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var metrics = JObject.Parse(content);
                    
                    // Employees
                    TotalEmployeesText.Text = metrics["employees"]["total"].ToString();
                    
                    // Departments
                    TotalDepartmentsText.Text = metrics["departments"]["total"].ToString();
                    
                    // Positions
                    TotalPositionsText.Text = metrics["positions"]["total"].ToString();
                    
                    // Leaves
                    PendingLeavesText.Text = metrics["leaves"]["pending_requests"].ToString();
                    
                    // Attendance Today
                    PresentTodayText.Text = metrics["attendance_today"]["present"].ToString();
                    LateTodayText.Text = metrics["attendance_today"]["late"].ToString();
                    
                    // Users - may be null for Employee role
                    var usersTotal = metrics["users"]["total"];
                    ActiveUsersText.Text = usersTotal != null && usersTotal.Type != JTokenType.Null 
                        ? usersTotal.ToString() 
                        : "N/A";
                    
                    // Salary - may be null for Employee role (sensitive data)
                    var avgSalary = metrics["salaries"]["average_salary"];
                    if (avgSalary != null && avgSalary.Type != JTokenType.Null)
                    {
                        AverageSalaryText.Text = decimal.Parse(avgSalary.ToString()).ToString("N0") + " VND";
                    }
                    else
                    {
                        AverageSalaryText.Text = "N/A";
                    }
                }
                else
                {
                    // API failed, show zeros
                    SetDefaultValues();
                }
            }
            catch (HttpRequestException)
            {
                // API not available, show default values
                SetDefaultValues();
            }
            catch (Exception ex)
            {
                // Parsing or other error
                System.Windows.MessageBox.Show($"Lỗi khi tải dữ liệu dashboard: {ex.Message}", 
                    "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                SetDefaultValues();
            }
        }
        
        private void SetDefaultValues()
        {
            TotalEmployeesText.Text = "0";
            TotalDepartmentsText.Text = "0";
            TotalPositionsText.Text = "0";
            PendingLeavesText.Text = "0";
            PresentTodayText.Text = "0";
            ActiveUsersText.Text = "0";
            AverageSalaryText.Text = "0 VND";
            LateTodayText.Text = "0";
        }
    }
}