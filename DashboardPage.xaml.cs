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
        private readonly string baseUrl = "http://localhost:8000/api/v1";

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
                
                // Load departments count
                var departmentsResponse = await httpClient.GetAsync($"{baseUrl}/departments");
                if (departmentsResponse.IsSuccessStatusCode)
                {
                    var departmentsContent = await departmentsResponse.Content.ReadAsStringAsync();
                    var departments = JArray.Parse(departmentsContent);
                    TotalDepartmentsText.Text = departments.Count.ToString();
                }

                // Load employees count
                var employeesResponse = await httpClient.GetAsync($"{baseUrl}/employees");
                if (employeesResponse.IsSuccessStatusCode)
                {
                    var employeesContent = await employeesResponse.Content.ReadAsStringAsync();
                    var employees = JArray.Parse(employeesContent);
                    TotalEmployeesText.Text = employees.Count.ToString();
                }

                // Set default values for other statistics
                // These would need proper API endpoints to get real data
                TotalPositionsText.Text = "5";
                PendingLeavesText.Text = "2";
                PresentTodayText.Text = "45";
                ActiveUsersText.Text = "12";
                AverageSalaryText.Text = "50,000,000 VND";
                LateTodayText.Text = "3";
            }
            catch (HttpRequestException)
            {
                // API not available, show default values
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
}