using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EmployeeManagement.Services;

namespace EmployeeManagement
{
    public partial class SalariesPage : Page
    {
        private readonly string _backendUrl = "http://127.0.0.1:8000";
        private ObservableCollection<SalaryRecord> _salaryRecords = new ObservableCollection<SalaryRecord>();
        private List<Employee> _employees = new List<Employee>();
        private int? SelectedEmployeeId = null;
        private string? SelectedEmployeeHireDate = null; // Store hire_date of selected employee

        public SalariesPage()
        {
            InitializeComponent();
            CheckPermissionsAndSetupUI();
            LoadSalaryData();
        }

        private void CheckPermissionsAndSetupUI()
        {
            if (!UserSessionService.IsAuthenticated)
            {
                MessageBox.Show("Bạn cần đăng nhập để xem lương.", "Yêu cầu xác thực",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Employee role can only view their own salary
            if (UserSessionService.IsEmployee)
            {
                CurrentSalaryPanel.Visibility = Visibility.Visible;
                EmployeeSearchPanel.Visibility = Visibility.Collapsed;
                AddSalaryForm.Visibility = Visibility.Collapsed;
                // Hide employee name column for employees since they only see their own data
                EmployeeColumn.Visibility = Visibility.Collapsed;
                LoadCurrentSalary();
            }
            else
            {
                // Admin/Manager see employee search and can manage salaries
                CurrentSalaryPanel.Visibility = Visibility.Collapsed;
                EmployeeSearchPanel.Visibility = Visibility.Visible;
                AddSalaryForm.Visibility = Visibility.Collapsed;
            }

            SalaryDataGrid.ItemsSource = _salaryRecords;
        }

        #region Data Models
        public class SalaryRecord
        {
            public int id { get; set; }
            public int employee_id { get; set; }
            public decimal? base_salary { get; set; }
            public decimal? amount { get; set; }
            public string currency { get; set; } = "VND";
            public DateTime effective_from { get; set; }
            public DateTime? effective_to { get; set; }
            public string notes { get; set; } = "";
            public string employee_name { get; set; } = "";
            public string employee_code { get; set; } = "";
            
            // Helper property to get display amount
            public decimal DisplayAmount => base_salary ?? amount ?? 0;
            
            // Helper property to display amount with currency
            public string DisplayAmountWithCurrency => $"{DisplayAmount:N0} {currency}";
        }
        
        public class Employee
        {
            public int id { get; set; }
            public string first_name { get; set; } = "";
            public string last_name { get; set; } = "";
            public string full_name => $"{first_name} {last_name}";
            public string email { get; set; } = "";
            public string employee_code { get; set; } = "";
            public string? hire_date { get; set; } // YYYY-MM-DD format
        }
        #endregion

        #region Data Loading
        private async void SearchEmployees(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2)
            {
                EmployeeSearchResults.ItemsSource = null;
                EmployeeSearchResults.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/employees/");
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var allEmployees = JsonSerializer.Deserialize<List<Employee>>(jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new List<Employee>();
                    
                    // Filter employees by search text
                    var filteredEmployees = allEmployees
                        .Where(e => e.full_name.ToLower().Contains(searchText.ToLower()) ||
                                   e.employee_code.ToLower().Contains(searchText.ToLower()) ||
                                   e.email.ToLower().Contains(searchText.ToLower()))
                        .Take(10) // Only show top 10 results
                        .ToList();
                    
                    EmployeeSearchResults.ItemsSource = filteredEmployees;
                    EmployeeSearchResults.Visibility = filteredEmployees.Count > 0 
                        ? Visibility.Visible 
                        : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tìm kiếm nhân viên: {ex.Message}",
                    "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadCurrentSalary()
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/salaries/my-salary");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var salary = JsonSerializer.Deserialize<SalaryRecord>(jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (salary != null)
                    {
                        CurrentSalaryAmount.Text = $"{salary.DisplayAmount:N0} {salary.currency}";
                        EffectiveFromDate.Text = salary.effective_from.ToString("dd/MM/yyyy");
                        CurrencyLabel.Text = salary.currency;
                    }
                    else
                    {
                        CurrentSalaryAmount.Text = "No salary record found";
                        EffectiveFromDate.Text = "N/A";
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    CurrentSalaryAmount.Text = "No salary record found";
                    EffectiveFromDate.Text = "N/A";
                }
                else
                {
                    CurrentSalaryAmount.Text = "Error loading salary";
                    EffectiveFromDate.Text = "N/A";
                }
            }
            catch (Exception ex)
            {
                CurrentSalaryAmount.Text = "Error loading salary";
                EffectiveFromDate.Text = ex.Message;
            }
        }

        private async void LoadSalaryData()
        {
            try
            {
                LoadingLabel.Visibility = Visibility.Visible;
                _salaryRecords.Clear();

                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                
                string endpoint;
                if (UserSessionService.IsEmployee)
                {
                    // Employee sees only their own salary history
                    endpoint = $"{_backendUrl}/api/v1/salaries/my-salaries";
                }
                else
                {
                    // Admin/Manager sees all salaries (would need different endpoint)
                    endpoint = $"{_backendUrl}/api/v1/salaries/";
                }

                var response = await httpClient.GetAsync(endpoint);
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var salaries = JsonSerializer.Deserialize<List<SalaryRecord>>(jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new List<SalaryRecord>();

                    // Sort by effective_from date descending (newest first)
                    salaries = salaries.OrderByDescending(s => s.effective_from).ToList();

                    foreach (var salary in salaries)
                    {
                        _salaryRecords.Add(salary);
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    // Only show error if not a 403 (permission denied) - that's handled by permission check
                    if (response.StatusCode != System.Net.HttpStatusCode.Forbidden)
                    {
                        MessageBox.Show($"Không thể tải dữ liệu lương: {errorContent}",
                            "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu lương: {ex.Message}",
                    "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingLabel.Visibility = Visibility.Collapsed;
                // Update action button visibility after data loads
                UpdateActionButtonsVisibility();
            }
        }

        private void UpdateActionButtonsVisibility()
        {
            // Use dispatcher to ensure DataGrid is fully rendered
            Dispatcher.InvokeAsync(() =>
            {
                SalaryDataGrid.UpdateLayout();
                
                for (int i = 0; i < SalaryDataGrid.Items.Count; i++)
                {
                    var row = (DataGridRow)SalaryDataGrid.ItemContainerGenerator.ContainerFromIndex(i);
                    if (row != null)
                    {
                        var editButton = FindVisualChild<Button>(row, "EditButton");
                        var deleteButton = FindVisualChild<Button>(row, "DeleteButton");

                        if (editButton != null)
                        {
                            // Manager and Admin can edit
                            editButton.Visibility = (UserSessionService.IsManager || UserSessionService.IsAdmin) 
                                ? Visibility.Visible : Visibility.Collapsed;
                        }

                        if (deleteButton != null)
                        {
                            // Only Admin can delete
                            deleteButton.Visibility = UserSessionService.IsAdmin 
                                ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private T? FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild && (child as FrameworkElement)?.Name == childName)
                {
                    return typedChild;
                }

                var foundChild = FindVisualChild<T>(child, childName);
                if (foundChild != null)
                    return foundChild;
            }
            return null;
        }
        #endregion

        #region Event Handlers
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserSessionService.IsEmployee)
            {
                LoadCurrentSalary();
            }
            LoadSalaryData();
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (SelectedEmployeeId == null)
            {
                MessageBox.Show("Vui lòng tìm và chọn nhân viên trước.", "Lỗi xác thực",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(BaseSalaryTextBox.Text) || 
                !decimal.TryParse(BaseSalaryTextBox.Text, out var baseSalary) || 
                baseSalary <= 0)
            {
                MessageBox.Show("Vui lòng nhập mức lương cơ bản hợp lệ (lớn hơn 0).", "Lỗi xác thực",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (EffectiveFromPicker.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày hiệu lực bắt đầu.", "Lỗi xác thực",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate effective dates
            if (EffectiveToPicker.SelectedDate != null && 
                EffectiveToPicker.SelectedDate < EffectiveFromPicker.SelectedDate)
            {
                MessageBox.Show("Ngày hiệu lực kết thúc phải lớn hơn hoặc bằng ngày hiệu lực bắt đầu.", 
                    "Lỗi xác thực",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Validate effective_from >= employee hire_date
            if (!string.IsNullOrEmpty(SelectedEmployeeHireDate))
            {
                if (DateTime.TryParse(SelectedEmployeeHireDate, out var hireDate))
                {
                    if (EffectiveFromPicker.SelectedDate < hireDate)
                    {
                        MessageBox.Show($"Ng\u00e0y hi\u1ec7u l\u1ef1c l\u01b0\u01a1ng ({EffectiveFromPicker.SelectedDate:yyyy-MM-dd}) ph\u1ea3i >= ng\u00e0y thu\u00ea nh\u00e2n vi\u00ean ({hireDate:yyyy-MM-dd}).",
                            "Lỗi xác thực",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            // Check if we're in edit mode
            bool isEditMode = AddButton.Tag is int;

            // Prepare salary data
            var salaryData = new
            {
                employee_id = SelectedEmployeeId.Value,
                base_salary = baseSalary,
                effective_from = EffectiveFromPicker.SelectedDate.Value.ToString("yyyy-MM-dd"),
                effective_to = EffectiveToPicker.SelectedDate?.ToString("yyyy-MM-dd")
            };

            var content = new StringContent(JsonSerializer.Serialize(salaryData), Encoding.UTF8, "application/json");

            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                
                HttpResponseMessage response;
                if (isEditMode)
                {
                    int salaryId = (int)AddButton.Tag;
                    response = await httpClient.PutAsync($"{_backendUrl}/api/v1/salaries/{salaryId}", content);
                }
                else
                {
                    response = await httpClient.PostAsync($"{_backendUrl}/api/v1/salaries/", content);
                }
                
                if (response.IsSuccessStatusCode)
                {
                    string successMessage = isEditMode ? "Cập nhật lương thành công!" : "Thêm lương thành công!";
                    MessageBox.Show(successMessage, "Thành công",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                    LoadSalaryData();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    string errorMessage = isEditMode ? "Không thể cập nhật lương" : "Không thể thêm lương";
                    MessageBox.Show($"{errorMessage}: {errorContent}", "Lỗi",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void AddSalaryButton_Click(object sender, RoutedEventArgs e)
        {
            // This button is now hidden, form is shown by default for Admin/Manager
            // Kept for backwards compatibility
        }

        private async void EditSalaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SalaryRecord record)
            {
                // Populate form with salary data for editing
                EmployeeNameTextBlock.Text = record.employee_name;
                SelectedEmployeeId = record.employee_id;
                BaseSalaryTextBox.Text = record.DisplayAmount.ToString("F0");
                EffectiveFromPicker.SelectedDate = record.effective_from;
                EffectiveToPicker.SelectedDate = record.effective_to;
                
                // IMPORTANT: Fetch employee hire_date for validation
                await LoadEmployeeHireDate(record.employee_id);
                
                // Change Add button to Update mode
                AddButton.Content = "Update Salary";
                AddButton.Background = System.Windows.Media.Brushes.Orange;
                AddButton.Tag = record.id; // Store ID for update operation
                
                // Show form and hide search panel
                EmployeeSearchPanel.Visibility = Visibility.Collapsed;
                AddSalaryForm.Visibility = Visibility.Visible;
                
                // Scroll to form
                AddSalaryForm.BringIntoView();
            }
        }
        
        private async Task LoadEmployeeHireDate(int employeeId)
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/employees/{employeeId}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var employee = JsonSerializer.Deserialize<Employee>(jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (employee != null)
                    {
                        SelectedEmployeeHireDate = employee.hire_date;
                    }
                }
            }
            catch (Exception ex)
            {
                // Silent fail - validation will use backend as fallback
                System.Diagnostics.Debug.WriteLine($"Failed to load employee hire_date: {ex.Message}");
            }
        }

        private async void DeleteSalaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SalaryRecord record)
            {
                var result = MessageBox.Show(
                    $"Bạn có chắc chắn muốn xóa bảng lương này?\n\n" +
                    $"Nhân viên: {record.employee_name}\n" +
                    $"Số tiền: {record.DisplayAmount:N0} VND\n" +
                    $"Hiệu lực từ: {record.effective_from:dd/MM/yyyy}",
                    "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                        var response = await httpClient.DeleteAsync($"{_backendUrl}/api/v1/salaries/{record.id}");

                        if (response.IsSuccessStatusCode)
                        {
                            MessageBox.Show("Xóa bảng lương thành công.", "Thành công", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadSalaryData();
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            MessageBox.Show($"Xóa thất bại: {errorContent}", "Lỗi", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khi xóa lương: {ex.Message}", "Lỗi", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        #endregion
        
        #region Helper Methods
        private void ClearForm()
        {
            SelectedEmployeeId = null;
            SelectedEmployeeHireDate = null; // Clear hire_date
            EmployeeNameTextBlock.Text = "No employee selected";
            BaseSalaryTextBox.Clear();
            EffectiveFromPicker.SelectedDate = DateTime.Now;
            EffectiveToPicker.SelectedDate = null;
            EmployeeSearchBox.Clear();
            EmployeeSearchResults.ItemsSource = null;
            EmployeeSearchResults.Visibility = Visibility.Collapsed;
            
            // Reset Add button to normal mode
            AddButton.Content = "Add Salary";
            AddButton.Background = System.Windows.Media.Brushes.LimeGreen;
            AddButton.Tag = null;
            
            // Hide form and show search panel
            AddSalaryForm.Visibility = Visibility.Collapsed;
            if (UserSessionService.IsAdmin || UserSessionService.IsManager)
            {
                EmployeeSearchPanel.Visibility = Visibility.Visible;
            }
        }
        
        private void EmployeeSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchEmployees(EmployeeSearchBox.Text);
        }
        
        private void EmployeeSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EmployeeSearchResults.SelectedItem is Employee selectedEmployee)
            {
                SelectedEmployeeId = selectedEmployee.id;
                SelectedEmployeeHireDate = selectedEmployee.hire_date; // Store hire_date
                EmployeeNameTextBlock.Text = $"{selectedEmployee.full_name} ({selectedEmployee.employee_code})";
                EmployeeSearchPanel.Visibility = Visibility.Collapsed;
                AddSalaryForm.Visibility = Visibility.Visible;
                EmployeeSearchResults.ItemsSource = null;
                EmployeeSearchResults.Visibility = Visibility.Collapsed;
                EmployeeSearchBox.Clear();
            }
        }
        #endregion
    }
}