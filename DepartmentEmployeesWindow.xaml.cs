using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using EmployeeManagement.Services;

namespace EmployeeManagement
{
    public partial class DepartmentEmployeesWindow : Window
    {
        private readonly string _backendUrl = "http://127.0.0.1:8000";
        private readonly int _departmentId;
        private string _departmentName = "";
        
        private int _currentPage = 1;
        private int _totalPages = 1;
        private int _totalRecords = 0;
        private int _pageSize = 20;
        private string _sortBy = "name";
        private string _order = "asc";

        public DepartmentEmployeesWindow(int departmentId, string departmentName)
        {
            InitializeComponent();
            _departmentId = departmentId;
            _departmentName = departmentName;
            TitleTextBlock.Text = $"Employees - {departmentName}";
            LoadEmployees();
        }

        private async void LoadEmployees()
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var url = $"{_backendUrl}/api/v1/departments/{_departmentId}/employees?" +
                         $"page={_currentPage}&page_size={_pageSize}&sort_by={_sortBy}&order={_order}";
                
                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Lỗi khi tải danh sách nhân viên", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(json);

                // Update pagination info
                var pagination = result["pagination"];
                if (pagination != null)
                {
                    _currentPage = int.Parse(pagination["page"]?.ToString() ?? "1");
                    _pageSize = int.Parse(pagination["page_size"]?.ToString() ?? "20");
                    _totalRecords = int.Parse(pagination["total_records"]?.ToString() ?? "0");
                    _totalPages = int.Parse(pagination["total_pages"]?.ToString() ?? "1");
                    
                    PageInfoText.Text = $"Page {_currentPage} of {_totalPages} (Total: {_totalRecords} employees)";
                }

                // Load employees
                var employees = result["employees"] as JArray;
                if (employees != null && employees.Count > 0)
                {
                    var employeeList = new List<EmployeeDto>();
                    foreach (var emp in employees)
                    {
                        var salary = decimal.Parse(emp["current_salary"]?.ToString() ?? "0");
                        employeeList.Add(new EmployeeDto
                        {
                            id = int.Parse(emp["id"]?.ToString() ?? "0"),
                            name = emp["name"]?.ToString() ?? "",
                            email = emp["email"]?.ToString() ?? "",
                            position = emp["position"]?.ToString() ?? "N/A",
                            hire_date = emp["hire_date"]?.ToString() ?? "",
                            current_salary = salary,
                            current_salary_formatted = $"{salary:N0} VND"
                        });
                    }
                    EmployeesDataGrid.ItemsSource = employeeList;
                }
                else
                {
                    EmployeesDataGrid.ItemsSource = null;
                }

                // Update button states
                FirstPageButton.IsEnabled = _currentPage > 1;
                PrevPageButton.IsEnabled = _currentPage > 1;
                NextPageButton.IsEnabled = _currentPage < _totalPages;
                LastPageButton.IsEnabled = _currentPage < _totalPages;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SortByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortByComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                _sortBy = item.Tag.ToString() ?? "name";
                _currentPage = 1;
                LoadEmployees();
            }
        }

        private void OrderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OrderComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                _order = item.Tag.ToString() ?? "asc";
                _currentPage = 1;
                LoadEmployees();
            }
        }

        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PageSizeComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                _pageSize = int.Parse(item.Tag.ToString() ?? "20");
                _currentPage = 1;
                LoadEmployees();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadEmployees();
        }

        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            LoadEmployees();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                LoadEmployees();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                LoadEmployees();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = _totalPages;
            LoadEmployees();
        }
    }

    public class EmployeeDto
    {
        public int id { get; set; }
        public string name { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public string position { get; set; } = string.Empty;
        public string hire_date { get; set; } = string.Empty;
        public decimal current_salary { get; set; }
        public string current_salary_formatted { get; set; } = string.Empty;
    }
}
