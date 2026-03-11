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
        private readonly string _backendUrl = "http://127.0.0.1:8000";
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
            // Load all data in parallel (3x faster than sequential)
            // Using Task.WhenAll to execute 3 API calls simultaneously
            await Task.WhenAll(
                LoadDepartmentsAsync(),
                LoadPositionsAsync(),
                LoadEmployeesAsync()
            );
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

            // Show salary filter for Admin and Manager only
            if (UserSessionService.IsAdmin || UserSessionService.IsManager)
            {
                SalaryFilterPanel.Visibility = Visibility.Visible;
            }
        }

        #region Data Models
        public class Employee
        {
            public int RowNumber { get; set; }  // For STT column
            public int Id { get; set; }
            public string first_name { get; set; } = "";
            public string last_name { get; set; } = "";
            public string email { get; set; } = "";
            public string phone { get; set; } = "";
            public int? department_id { get; set; }
            public int? position_id { get; set; }
            public DateTime? hire_date { get; set; }
            
            // Backend returns current salary from salaries table (LEFT JOIN with effective_to IS NULL)
            // Uses SQLAlchemy eager loading: joinedload(Employee.salaries)
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
            public int? department_id { get; set; }
        }
        #endregion

        #region Load Data Methods
        private async Task LoadDepartmentsAsync()
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
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
                    // Also bind to filter panel dropdown
                    FilterDepartmentComboBox.ItemsSource = _departments;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Không thể tải danh sách phòng ban: {response.StatusCode}\n{errorContent}", "Lỗi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải phòng ban: {ex.Message}", "Lỗi");
            }
        }

        private async Task LoadPositionsAsync()
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
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
                    // Also bind to filter panel dropdown
                    FilterPositionComboBox.ItemsSource = _positions;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Không thể tải danh sách chức vụ: {response.StatusCode}\n{errorContent}", "Lỗi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải chức vụ: {ex.Message}", "Lỗi");
            }
        }

        // Khi chọn phòng ban ở form Add Employee → lọc vị trí tương ứng
        private async void DepartmentComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            PositionComboBox.SelectedIndex = -1;
            if (DepartmentComboBox.SelectedValue is int deptId)
            {
                var filtered = await FetchPositionsByDepartmentAsync(deptId);
                PositionComboBox.ItemsSource = filtered;
            }
            else
            {
                PositionComboBox.ItemsSource = _positions;
            }
        }

        // Khi chọn phòng ban ở filter panel → lọc vị trí trong FilterPositionComboBox
        private async void FilterDepartmentComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            FilterPositionComboBox.SelectedIndex = -1;
            if (FilterDepartmentComboBox.SelectedValue is int deptId)
            {
                var filtered = await FetchPositionsByDepartmentAsync(deptId);
                FilterPositionComboBox.ItemsSource = filtered;
            }
            else
            {
                FilterPositionComboBox.ItemsSource = _positions;
            }
        }

        private async Task<List<Position>> FetchPositionsByDepartmentAsync(int departmentId)
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/positions/?department_id={departmentId}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<Position>>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<Position>();
                }
            }
            catch { }
            return new List<Position>();
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
                    int idx = 1;
                    foreach (var employee in employees)
                    {
                        // Backend now returns department_name and position_title directly
                        // No need to manually map from _departments and _positions
                        employee.RowNumber = idx++;
                        _employees.Add(employee);
                    }

                    EmployeesDataGrid.ItemsSource = _employees;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh sách nhân viên: {ex.Message}", "Lỗi");
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
                MessageBox.Show("Vui lòng điền đầy đủ các trường bắt buộc (đánh dấu *).", "Lỗi xác thực");
                return;
            }

            // Check if we're in edit mode
            bool isEditMode = AddButton.Tag is int;

            // Validate password for new employee
            if (!isEditMode && string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vui lòng nhập mật khẩu cho nhân viên mới.", "Lỗi xác thực");
                return;
            }

            if (!isEditMode && password.Length < 6)
            {
                MessageBox.Show("Mật khẩu phải có ít nhất 6 ký tự.", "Lỗi xác thực");
                return;
            }

            // Validate email format
            if (!IsValidEmail(email))
            {
                MessageBox.Show("Vui lòng nhập địa chỉ email hợp lệ.", "Lỗi xác thực");
                return;
            }
            
            // Validate phone format (if provided)
            if (!string.IsNullOrEmpty(phone))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[0-9+\-\s()]{8,20}$"))
                {
                    MessageBox.Show("Số điện thoại không hợp lệ (8-20 ký tự, chỉ số, +, -, khoảng trắng, dấu ngoặc).", "Lỗi xác thực");
                    return;
                }
            }
            
            // Validate hire date (must not be in the future)
            if (HireDatePicker.SelectedDate.HasValue && HireDatePicker.SelectedDate.Value > DateTime.Today)
            {
                MessageBox.Show("Ngày thuê không được trong tương lai.", "Lỗi xác thực");
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
                    hire_date = HireDatePicker.SelectedDate?.ToString("yyyy-MM-dd")
                    // NOTE: Salary is managed separately via salaries table
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
                    hire_date = HireDatePicker.SelectedDate?.ToString("yyyy-MM-dd")
                    // NOTE: Salary is managed separately via salaries table
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
                    string successMessage = isEditMode ? "Cập nhật nhân viên thành công!" : "Thêm nhân viên thành công!";
                    MessageBox.Show(successMessage, "Thành công");
                    ClearForm();
                    await LoadEmployeesAsync();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    string errorMessage = isEditMode ? "Không thể cập nhật nhân viên" : "Không thể thêm nhân viên";
                    MessageBox.Show($"{errorMessage}: {errorContent}", "Lỗi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi");
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

        private async void FilterSalaryButton_Click(object sender, RoutedEventArgs e)
        {
            var text = SalaryFilterTextBox.Text.Trim().Replace(",", "").Replace(".", "");
            decimal? salaryValue = null;
            
            // Salary is optional - only parse if provided
            if (!string.IsNullOrEmpty(text))
            {
                if (!decimal.TryParse(text, out decimal parsed) || parsed < 0)
                {
                    MessageBox.Show("Please enter a valid salary (e.g., 15000000)", "Error");
                    return;
                }
                salaryValue = parsed;
            }

            // Build query - at least one filter must be set
            var queryParams = new System.Text.StringBuilder();
            bool hasFilter = false;

            if (salaryValue.HasValue)
            {
                var op = (SalaryOperatorComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? ">=";
                string paramName = op == ">=" ? "min_salary" : "max_salary";
                queryParams.Append($"?{paramName}={salaryValue.Value}");
                hasFilter = true;
            }

            if (FilterDepartmentComboBox.SelectedValue is int deptId)
            {
                queryParams.Append(hasFilter ? "&" : "?");
                queryParams.Append($"department_id={deptId}");
                hasFilter = true;
            }

            if (FilterPositionComboBox.SelectedValue is int posId)
            {
                queryParams.Append(hasFilter ? "&" : "?");
                queryParams.Append($"position_id={posId}");
                hasFilter = true;
            }

            if (!hasFilter)
            {
                MessageBox.Show("Please select at least one filter (Salary, Department, or Position)", "No filter");
                return;
            }

            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/employees/filter/by-salary{queryParams}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var employees = JsonSerializer.Deserialize<List<Employee>>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<Employee>();

                    EmployeesDataGrid.ItemsSource = employees;
                    ClearSalaryFilterButton.Visibility = Visibility.Visible;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Error: {error}", "Cannot filter");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection error: {ex.Message}", "Error");
            }
        }

        private async void ClearSalaryFilterButton_Click(object sender, RoutedEventArgs e)
        {
            SalaryFilterTextBox.Clear();
            FilterDepartmentComboBox.SelectedIndex = -1;
            FilterPositionComboBox.SelectedIndex = -1;
            ClearSalaryFilterButton.Visibility = Visibility.Collapsed;
            await LoadEmployeesAsync();
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is int employeeId)
            {
                var result = MessageBox.Show("Bạn có chắc chắn muốn xóa nhân viên này?", 
                    "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Check permission before delete
                        if (!UserSessionService.CanDeleteEmployee)
                        {
                            MessageBox.Show("Bạn không có quyền xóa nhân viên.", "Từ chối truy cập");
                            return;
                        }

                        using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                        var response = await httpClient.DeleteAsync($"{_backendUrl}/api/v1/employees/{employeeId}");
                        if (response.IsSuccessStatusCode)
                        {
                            MessageBox.Show("Xóa nhân viên thành công!", "Thành công");
                            await LoadEmployeesAsync();
                        }
                        else
                        {
                            MessageBox.Show("Không thể xóa nhân viên!", "Lỗi");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi");
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
                    // NOTE: Salary is managed separately - see Salaries page
                    
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
                for (int i = 0; i < _employees.Count; i++)
                    _employees[i].RowNumber = i + 1;
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
                for (int i = 0; i < filteredEmployees.Count; i++)
                    filteredEmployees[i].RowNumber = i + 1;
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
            // NOTE: No salary field - managed separately
            
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

        private void DataGrid_LoadingRow(object sender, System.Windows.Controls.DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
            if (e.Row.DataContext is Employee emp)
            {
                emp.RowNumber = e.Row.GetIndex() + 1;
            }
        }
        #endregion
    }
}