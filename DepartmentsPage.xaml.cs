using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using EmployeeManagement.Services;

namespace EmployeeManagement
{
    public partial class DepartmentsPage : Page
    {
        private readonly string _backendUrl = "http://127.0.0.1:8000";

        public DepartmentsPage()
        {
            InitializeComponent();
            CheckPermissionsAndSetupUI();
            LoadDepartments();
        }

        private void CheckPermissionsAndSetupUI()
        {
            // Check if user is authenticated
            if (!UserSessionService.IsAuthenticated)
            {
                MessageBox.Show("You need to be logged in to view departments.", "Authentication Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if user has permission to manage departments
            if (!UserSessionService.CanManageDepartments)
            {
                AddButton.Visibility = Visibility.Collapsed;
            }
            
            // Hide Actions column (Statistics/Employees buttons) for Employee role
            // Only Admin and Manager can view detailed department statistics and employee lists
            if (UserSessionService.IsEmployee)
            {
                ActionsColumn.Visibility = Visibility.Collapsed;
                CompareButton.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadDepartments(string searchName = null)
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                
                // Use the new endpoint with employee counts
                string url = string.IsNullOrEmpty(searchName) 
                    ? $"{_backendUrl}/api/v1/departments/list/with-counts"
                    : $"{_backendUrl}/api/v1/departments/search?name={searchName}";
                
                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Lỗi khi tải danh sách phòng ban", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();

                var departments = JsonSerializer.Deserialize<List<DepartmentDto>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                DepartmentsDataGrid.ItemsSource = departments;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh sách phòng ban: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.Trim();
            LoadDepartments(string.IsNullOrEmpty(searchText) ? null : searchText);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
            LoadDepartments();
        }

        private void ViewStatistics_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                try
                {
                    int departmentId = Convert.ToInt32(button.Tag);
                    var statisticsWindow = new DepartmentStatisticsWindow(departmentId);
                    statisticsWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ViewEmployees_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                try
                {
                    int departmentId = Convert.ToInt32(button.Tag);
                    
                    // Get department name from grid
                    var dept = DepartmentsDataGrid.Items.Cast<DepartmentDto>()
                        .FirstOrDefault(d => d.id == departmentId);
                    
                    var deptName = dept?.name ?? "Department";
                    
                    var employeesWindow = new DepartmentEmployeesWindow(departmentId, deptName);
                    employeesWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected department IDs
                var selectedIds = new List<int>();
                foreach (var item in DepartmentsDataGrid.Items)
                {
                    if (item is DepartmentDto dept)
                    {
                        selectedIds.Add(dept.id);
                    }
                }

                if (selectedIds.Count < 2)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất 2 phòng ban để so sánh", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // For now, compare first 3 departments
                var idsToCompare = selectedIds.Take(3).ToList();

                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var content = new StringContent(
                    JsonSerializer.Serialize(idsToCompare),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.PostAsync($"{_backendUrl}/api/v1/departments/compare", content);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Lỗi khi so sánh phòng ban", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(json);

                var message = "SO SÁNH PHÒNG BAN\n\n";

                var summary = result["summary"];
                if (summary != null)
                {
                    message += "Tóm Tắt:\n";
                    message += $"• Phòng ban lớn nhất: {summary["largest_department"]}\n";
                    message += $"• Lương cao nhất: {summary["highest_paid_department"]}\n";
                    message += $"• Đa dạng vị trí nhất: {summary["most_diverse_positions"]}\n\n";
                }

                message += "Chi Tiết:\n";
                var comparison = result["comparison"] as JArray;
                if (comparison != null)
                {
                    foreach (var dept in comparison)
                    {
                        message += $"\n{dept["department_name"]}:\n";
                        message += $"  - Nhân viên: {dept["total_employees"]} (Hạng #{dept["rank_by_size"]})\n";
                        message += $"  - Lương TB: {decimal.Parse(dept["avg_salary"]?.ToString() ?? "0"):N0} VND (Hạng #{dept["rank_by_salary"]})\n";
                        message += $"  - Tổng lương: {decimal.Parse(dept["total_salary_budget"]?.ToString() ?? "0"):N0} VND\n";
                        message += $"  - Số vị trí: {dept["unique_positions"]}\n";
                    }
                }

                MessageBox.Show(message, "So Sánh Phòng Ban", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var departmentName = DepartmentNameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(departmentName))
            {
                MessageBox.Show("Please enter department name.", "Validation Error");
                return;
            }

            var departmentData = new { name = departmentName };

            var content = new StringContent(
                JsonSerializer.Serialize(departmentData),
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.PostAsync(
                    $"{_backendUrl}/api/v1/departments",
                    content
                );

                if (response.IsSuccessStatusCode)
                {
                    DepartmentNameTextBox.Clear();
                    LoadDepartments();
                }
                else
                {
                    MessageBox.Show("Failed to add department");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
    }

    // DTO matching FastAPI response (snake_case)
    public class DepartmentDto
    {
        public int id { get; set; }
        public string name { get; set; } = string.Empty;
        public int employee_count { get; set; }
        public double avg_salary { get; set; }
    }
}
