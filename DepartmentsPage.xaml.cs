using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using EmployeeManagement.Services;

namespace EmployeeManagement
{
    public partial class DepartmentsPage : Page   // ? Page, KH�NG ph?i UserControl
    {
        private readonly string _backendUrl = "http://localhost:8000";

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
        }

        private async void LoadDepartments()
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/departments");

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Failed to load departments");
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
                MessageBox.Show($"Error loading departments: {ex.Message}");
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

    // DTO kh?p FastAPI (snake_case)
    public class DepartmentDto
    {
        public int id { get; set; }
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
    }
}
