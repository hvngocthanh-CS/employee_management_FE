using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
    public partial class EmployeesPage : Page
    {
        private readonly string _backendUrl = "http://localhost:8000";
        private ObservableCollection<Employee> _employees = new ObservableCollection<Employee>();
        private List<Department> _departments = new List<Department>();
        private List<Position> _positions = new List<Position>();

        public EmployeesPage()
        {
            InitializeComponent();
            CheckPermissionsAndSetupUI();
            InitializeDataAsync();
        }

        private async void InitializeDataAsync()
        {
            // Load departments and positions first (for ComboBoxes)
            await LoadDepartmentsAsync();
            await LoadPositionsAsync();
            // Then load employees (will use the loaded departments/positions)
            await LoadEmployeesAsync();
        }

        private void CheckPermissionsAndSetupUI()
        {
            // If employee role, only show their own data and hide add functionality
            if (UserSessionService.IsEmployee)
            {
                // Hide entire add employee form
                AddEmployeeForm.Visibility = Visibility.Collapsed;
            }
            else if (!UserSessionService.CanCreateEmployee)
            {
                // For other roles without create permission, just hide buttons
                AddButton.Visibility = Visibility.Collapsed;
                ClearButton.Visibility = Visibility.Collapsed;
            }
        }

        #region Data Models
        public class Employee
        {
            public int Id { get; set; }
            public string first_name { get; set; } = "";
            public string last_name { get; set; } = "";
            public string email { get; set; } = "";
            public string phone { get; set; } = "";
            public int? department_id { get; set; }
            public int? position_id { get; set; }
            public DateTime? hire_date { get; set; }
            public decimal? salary { get; set; }
            
            // Backend returns these fields directly
            public string department_name { get; set; } = "";
            public string position_title { get; set; } = "";
            
            // Alias for DataGrid column binding (if XAML uses position_name)
            public string position_name => position_title ?? "";
        }

        public class Department
        {
            public int id { get; set; }
            public string name { get; set; } = "";
            public string description { get; set; } = "";
        }

        public class Position
        {
            public int id { get; set; }
            public string title { get; set; } = "";
            public string description { get; set; } = "";
        }
        #endregion

        #region Load Data Methods
        private async Task LoadDepartmentsAsync()
        {
            try
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/departments/");
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    _departments = JsonSerializer.Deserialize<List<Department>>(jsonContent, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    }) ?? new List<Department>();
                    
                    DepartmentComboBox.ItemsSource = _departments;
                    DepartmentComboBox.DisplayMemberPath = "name";
                    DepartmentComboBox.SelectedValuePath = "id";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading departments: {ex.Message}");
            }
        }

        private async Task LoadPositionsAsync()
        {
            try
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/positions/");
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    _positions = JsonSerializer.Deserialize<List<Position>>(jsonContent, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    }) ?? new List<Position>();
                    
                    PositionComboBox.ItemsSource = _positions;
                    PositionComboBox.DisplayMemberPath = "title";
                    PositionComboBox.SelectedValuePath = "id";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading positions: {ex.Message}");
            }
        }

        private async Task LoadEmployeesAsync()
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                
                string endpoint = UserSessionService.IsEmployee 
                    ? $"{_backendUrl}/api/v1/employees/me"
                    : $"{_backendUrl}/api/v1/employees/";
                
                var response = await httpClient.GetAsync(endpoint);
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    
                    List<Employee> employees;
                    if (UserSessionService.IsEmployee)
                    {
                        // For single employee response
                        var employee = JsonSerializer.Deserialize<Employee>(jsonContent, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                        employees = employee != null ? new List<Employee> { employee } : new List<Employee>();
                    }
                    else
                    {
                        // For list of employees
                        employees = JsonSerializer.Deserialize<List<Employee>>(jsonContent, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        }) ?? new List<Employee>();
                    }

                    _employees.Clear();
                    foreach (var employee in employees)
                    {
                        // Backend now returns department_name and position_title directly
                        // No need to manually map from _departments and _positions
                        _employees.Add(employee);
                    }

                    EmployeesDataGrid.ItemsSource = _employees;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading employees: {ex.Message}");
            }
        }
        #endregion

        #region Button Event Handlers
        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var firstName = FirstNameTextBox.Text.Trim();
            var lastName = LastNameTextBox.Text.Trim();
            var email = EmailTextBox.Text.Trim();
            var password = PasswordBox.Password;
            var phone = PhoneTextBox.Text.Trim();

            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Please fill all required fields (marked with *).", "Validation Error");
                return;
            }

            // Check if we're in edit mode
            bool isEditMode = AddButton.Tag is int;

            // Validate password for new employee
            if (!isEditMode && string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter a password for the new employee.", "Validation Error");
                return;
            }

            if (!isEditMode && password.Length < 6)
            {
                MessageBox.Show("Password must be at least 6 characters.", "Validation Error");
                return;
            }

            // Validate email format
            if (!IsValidEmail(email))
            {
                MessageBox.Show("Please enter a valid email address.", "Validation Error");
                return;
            }
            
            // Validate phone format (if provided)
            if (!string.IsNullOrEmpty(phone))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[0-9+\-\s()]{8,20}$"))
                {
                    MessageBox.Show("Số điện thoại không hợp lệ (8-20 ký tự, chỉ số, +, -, khoảng trắng, dấu ngoặc).", "Validation Error");
                    return;
                }
            }
            
            // Validate hire date (must not be in the future)
            if (HireDatePicker.SelectedDate.HasValue && HireDatePicker.SelectedDate.Value > DateTime.Today)
            {
                MessageBox.Show("Ngày thuê không được trong tương lai.", "Validation Error");
                return;
            }

            // Prepare employee data
            object employeeData;
            
            if (isEditMode)
            {
                // For update: don't include password
                employeeData = new
                {
                    first_name = firstName,
                    last_name = lastName,
                    email = email,
                    phone = string.IsNullOrEmpty(phone) ? null : phone,
                    department_id = DepartmentComboBox.SelectedValue as int?,
                    position_id = PositionComboBox.SelectedValue as int?,
                    hire_date = HireDatePicker.SelectedDate?.ToString("yyyy-MM-dd"),
                    salary = decimal.TryParse(SalaryTextBox.Text, out var salaryEdit) ? salaryEdit : (decimal?)null
                };
            }
            else
            {
                // For create: include password
                employeeData = new
                {
                    first_name = firstName,
                    last_name = lastName,
                    email = email,
                    password = password,
                    phone = string.IsNullOrEmpty(phone) ? null : phone,
                    department_id = DepartmentComboBox.SelectedValue as int?,
                    position_id = PositionComboBox.SelectedValue as int?,
                    hire_date = HireDatePicker.SelectedDate?.ToString("yyyy-MM-dd"),
                    salary = decimal.TryParse(SalaryTextBox.Text, out var salaryCreate) ? salaryCreate : (decimal?)null
                };
            }

            var content = new StringContent(JsonSerializer.Serialize(employeeData), Encoding.UTF8, "application/json");

            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                
                HttpResponseMessage response;
                if (isEditMode)
                {
                    int employeeId = (int)AddButton.Tag;
                    // Update existing employee
                    response = await httpClient.PutAsync($"{_backendUrl}/api/v1/employees/{employeeId}", content);
                }
                else
                {
                    // Create new employee
                    response = await httpClient.PostAsync($"{_backendUrl}/api/v1/employees/", content);
                }
                
                if (response.IsSuccessStatusCode)
                {
                    string successMessage = isEditMode ? "Employee updated successfully!" : "Employee added successfully!";
                    MessageBox.Show(successMessage, "Success");
                    ClearForm();
                    await LoadEmployeesAsync();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    string errorMessage = isEditMode ? "Failed to update employee" : "Failed to add employee";
                    MessageBox.Show($"{errorMessage}: {errorContent}", "Error");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadEmployeesAsync();
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is int employeeId)
            {
                var result = MessageBox.Show("Are you sure you want to delete this employee?", 
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Check permission before delete
                        if (!UserSessionService.CanDeleteEmployee)
                        {
                            MessageBox.Show("You don't have permission to delete employees.", "Access Denied");
                            return;
                        }

                        using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                        var response = await httpClient.DeleteAsync($"{_backendUrl}/api/v1/employees/{employeeId}");
                        if (response.IsSuccessStatusCode)
                        {
                            MessageBox.Show("Employee deleted successfully!", "Success");
                            await LoadEmployeesAsync();
                        }
                        else
                        {
                            MessageBox.Show("Failed to delete employee!", "Error");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}", "Error");
                    }
                }
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is int employeeId)
            {
                var employee = _employees.FirstOrDefault(emp => emp.Id == employeeId);
                if (employee != null)
                {
                    // Populate form with employee data for editing
                    FirstNameTextBox.Text = employee.first_name;
                    LastNameTextBox.Text = employee.last_name;
                    EmailTextBox.Text = employee.email;
                    PhoneTextBox.Text = employee.phone ?? "";
                    DepartmentComboBox.SelectedValue = employee.department_id;
                    PositionComboBox.SelectedValue = employee.position_id;
                    HireDatePicker.SelectedDate = employee.hire_date;
                    SalaryTextBox.Text = employee.salary?.ToString("F2") ?? "";
                    
                    // Hide password field when editing (can't change password here)
                    PasswordBox.IsEnabled = false;
                    PasswordBox.Clear();
                    
                    // Change Add button to Update mode
                    AddButton.Content = "Update Employee";
                    AddButton.Background = System.Windows.Media.Brushes.Orange;
                    AddButton.Tag = employeeId; // Store ID for update operation
                }
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.ToLower();
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                EmployeesDataGrid.ItemsSource = _employees;
            }
            else
            {
                var filteredEmployees = _employees.Where(emp => 
                    emp.first_name.ToLower().Contains(searchText) ||
                    emp.last_name.ToLower().Contains(searchText) ||
                    emp.email.ToLower().Contains(searchText) ||
                    (emp.phone ?? "").ToLower().Contains(searchText)
                ).ToList();
                
                EmployeesDataGrid.ItemsSource = filteredEmployees;
            }
        }
        #endregion

        #region Helper Methods
        private void ClearForm()
        {
            FirstNameTextBox.Clear();
            LastNameTextBox.Clear();
            EmailTextBox.Clear();
            PasswordBox.Clear();
            PasswordBox.IsEnabled = true;  // Re-enable for new employee
            PhoneTextBox.Clear();
            DepartmentComboBox.SelectedIndex = -1;
            PositionComboBox.SelectedIndex = -1;
            HireDatePicker.SelectedDate = null;
            SalaryTextBox.Clear();
            
            // Reset Add button to normal mode
            AddButton.Content = "Add Employee";
            AddButton.Background = System.Windows.Media.Brushes.LimeGreen;
            AddButton.Tag = null;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}