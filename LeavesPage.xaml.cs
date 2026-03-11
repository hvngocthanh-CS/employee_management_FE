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
    public partial class LeavesPage : Page
    {
        private readonly string _backendUrl = "http://127.0.0.1:8000";
        private ObservableCollection<LeaveRecord> _leaveRecords = new ObservableCollection<LeaveRecord>();
        private int? _selectedEmployeeId = null;

        public LeavesPage()
        {
            InitializeComponent();
            CheckPermissionsAndSetupUI();
            // LoadLeaveData() is triggered by StatusFilterComboBox.SelectedIndex = 0 in CheckPermissionsAndSetupUI()
        }

        private void CheckPermissionsAndSetupUI()
        {
            if (!UserSessionService.IsAuthenticated)
            {
                MessageBox.Show("You need to be logged in to view leaves.", "Authentication Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // All roles can create leave requests
            LeaveRequestPanel.Visibility = Visibility.Visible;

            // Employee role: hide employee search, only see own leaves
            if (UserSessionService.IsEmployee)
            {
                EmployeeSearchPanel.Visibility = Visibility.Collapsed;
                _selectedEmployeeId = UserSessionService.CurrentUser?.employee_id;
                // Hide employee name column for employees since they only see their own data
                LeaveEmployeeColumn.Visibility = Visibility.Collapsed;
                
                // Check if user has employee_id - Admin/Manager converted to Employee may not have one
                if (UserSessionService.CurrentUser?.employee_id == null)
                {
                    // Hide leave request panel - can't create leave without employee record
                    LeaveRequestPanel.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Admin/Manager: show employee search to select employee
                EmployeeSearchPanel.Visibility = Visibility.Visible;
                LeaveEmployeeColumn.Visibility = Visibility.Visible;
            }

            // Set default values
            LeaveTypeComboBox.SelectedIndex = 0; // Annual Leave
            StartDatePicker.SelectedDate = DateTime.Today.AddDays(1);
            EndDatePicker.SelectedDate = DateTime.Today.AddDays(1);
            
            // Set default status filter
            StatusFilterComboBox.SelectedIndex = 0; // All
            LeaveDataGrid.ItemsSource = _leaveRecords;
        }

        #region Data Models
        public class LeaveRecord
        {
            public int id { get; set; }
            public int employee_id { get; set; }
            public string leave_type { get; set; } = "";
            public DateTime start_date { get; set; }
            public DateTime end_date { get; set; }
            public int total_days { get; set; }
            public string reason { get; set; } = "";
            public string status { get; set; } = "";
            public DateTime created_at { get; set; }
            public DateTime? approved_at { get; set; }
            public string employee_name { get; set; } = "";
            public string employee_code { get; set; } = "";
            public string department_name { get; set; } = "";
            public int RowNumber { get; set; }
        }

        public class LeaveRequest
        {
            public int employee_id { get; set; }
            public string leave_type { get; set; } = "";
            public string start_date { get; set; } = "";  // YYYY-MM-DD format
            public string end_date { get; set; } = "";    // YYYY-MM-DD format
            public int total_days { get; set; }
            public string reason { get; set; } = "";
        }
        
        public class Employee
        {
            public int id { get; set; }
            public string employee_code { get; set; } = "";
            public string full_name { get; set; } = "";
            public string email { get; set; } = "";
            public DateTime? hire_date { get; set; }
        }
        #endregion
        
        #region Employee Search
        private async void EmployeeSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchTerm = EmployeeSearchTextBox.Text?.Trim() ?? "";
            
            if (searchTerm.Length < 2)
            {
                EmployeeSearchResults.Visibility = Visibility.Collapsed;
                EmployeeSearchResults.ItemsSource = null;
                return;
            }

            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.GetAsync(
                    $"{_backendUrl}/api/v1/employees/?search={Uri.EscapeDataString(searchTerm)}&limit=10");

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    
                    // API returns array directly, not wrapped object
                    var employees = JsonSerializer.Deserialize<List<Employee>>(jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Employee>();
                    
                    if (employees.Count > 0)
                    {
                        EmployeeSearchResults.ItemsSource = employees;
                        EmployeeSearchResults.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        EmployeeSearchResults.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching employees: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EmployeeSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EmployeeSearchResults.SelectedItem is Employee employee)
            {
                _selectedEmployeeId = employee.id;
                EmployeeSearchTextBox.Text = $"{employee.employee_code} - {employee.full_name}";
                EmployeeSearchResults.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Data Loading
        private async void LoadLeaveData()
        {
            try
            {
                LoadingLeavesLabel.Visibility = Visibility.Visible;
                _leaveRecords.Clear();

                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                
                string endpoint;
                if (UserSessionService.IsEmployee)
                {
                    // Check if user has employee_id
                    if (UserSessionService.CurrentUser?.employee_id == null)
                    {
                        LoadingLeavesLabel.Visibility = Visibility.Collapsed;
                        return;
                    }
                    // Employee sees only their own leaves
                    endpoint = $"{_backendUrl}/api/v1/leaves/my-leaves";
                }
                else
                {
                    // Admin/Manager sees all leaves
                    endpoint = $"{_backendUrl}/api/v1/leaves/";
                }

                // Add status filter if selected
                var selectedStatus = (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (!string.IsNullOrEmpty(selectedStatus))
                {
                    endpoint += $"?status={selectedStatus}";
                }

                // Ensure all records are loaded
                endpoint += (endpoint.Contains("?") ? "&" : "?") + "limit=10000";

                var response = await httpClient.GetAsync(endpoint);
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    
                    // The API might return a wrapper object, let's handle both cases
                    List<LeaveRecord> leaves;
                    try
                    {
                        // Try to deserialize as LeaveListResponse first
                        var leaveResponse = JsonSerializer.Deserialize<LeaveListResponse>(jsonContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        leaves = leaveResponse?.leaves ?? new List<LeaveRecord>();
                    }
                    catch
                    {
                        // Fallback to direct list deserialization
                        leaves = JsonSerializer.Deserialize<List<LeaveRecord>>(jsonContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                            ?? new List<LeaveRecord>();
                    }

                    // Sort by created_at date descending (newest first)
                    leaves = leaves.OrderByDescending(l => l.created_at).ToList();

                    int idx = 1;
                    foreach (var leave in leaves)
                    {
                        leave.RowNumber = idx++;
                        _leaveRecords.Add(leave);
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to load leave data: {errorContent}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading leave data: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingLeavesLabel.Visibility = Visibility.Collapsed;
                // Update action button visibility after data loads
                UpdateActionButtonsVisibility();
            }
        }

        private void UpdateActionButtonsVisibility()
        {
            // Refresh all currently visible rows (handles initial load)
            Dispatcher.InvokeAsync(() =>
            {
                LeaveDataGrid.UpdateLayout();
                for (int i = 0; i < LeaveDataGrid.Items.Count; i++)
                {
                    var row = (DataGridRow)LeaveDataGrid.ItemContainerGenerator.ContainerFromIndex(i);
                    if (row != null && row.Item is LeaveRecord record)
                        UpdateLeaveRowButtons(row, record);
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

        public class LeaveListResponse
        {
            public List<LeaveRecord> leaves { get; set; } = new List<LeaveRecord>();
            public int total { get; set; }
        }
        #endregion

        #region Leave Request Actions
        private async void SubmitLeaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validation for employee selection (Manager/Admin must select an employee)
                if (!UserSessionService.IsEmployee && !_selectedEmployeeId.HasValue)
                {
                    MessageBox.Show("Please select an employee before creating leave request.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Validation
                if (LeaveTypeComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Please select a leave type.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
                {
                    MessageBox.Show("Please select both start and end dates.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (StartDatePicker.SelectedDate.Value > EndDatePicker.SelectedDate.Value)
                {
                    MessageBox.Show("End date must be after start date.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (StartDatePicker.SelectedDate.Value < DateTime.Today)
                {
                    MessageBox.Show("Start date cannot be in the past.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(ReasonTextBox.Text))
                {
                    MessageBox.Show("Please provide a reason for the leave request.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Calculate total days
                var startDate = StartDatePicker.SelectedDate.Value;
                var endDate = EndDatePicker.SelectedDate.Value;
                var totalDays = (endDate - startDate).Days + 1;

                // Create leave request using selected employee ID (for Employee, it's their own ID)
                var leaveRequest = new LeaveRequest
                {
                    employee_id = _selectedEmployeeId.Value,
                    leave_type = (LeaveTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "",
                    start_date = startDate.ToString("yyyy-MM-dd"),
                    end_date = endDate.ToString("yyyy-MM-dd"),
                    total_days = totalDays,
                    reason = ReasonTextBox.Text.Trim()
                };

                var json = JsonSerializer.Serialize(leaveRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.PostAsync($"{_backendUrl}/api/v1/leaves/", content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Leave request submitted successfully!", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearLeaveForm();
                    LoadLeaveData(); // Refresh the data
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    
                    // Try to parse error message
                    try
                    {
                        var errorJson = JsonSerializer.Deserialize<Dictionary<string, object>>(errorContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                        if (errorJson != null && errorJson.ContainsKey("detail"))
                        {
                            MessageBox.Show($"Error: {errorJson["detail"]}", "Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    catch { }
                    
                    MessageBox.Show($"Failed to submit leave request: {errorContent}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error submitting leave request: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLeaveButton_Click(object sender, RoutedEventArgs e)
        {
            ClearLeaveForm();
        }

        private void ClearLeaveForm()
        {
            LeaveTypeComboBox.SelectedIndex = 0;
            StartDatePicker.SelectedDate = DateTime.Today.AddDays(1);
            EndDatePicker.SelectedDate = DateTime.Today.AddDays(1);
            ReasonTextBox.Text = "";
            
            // Clear employee search for Manager/Admin
            if (!UserSessionService.IsEmployee)
            {
                EmployeeSearchTextBox.Text = "";
                _selectedEmployeeId = null;
                EmployeeSearchResults.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Event Handlers
        private void StatusFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Auto-reload when status filter changes
            LoadLeaveData();
        }

        private void RefreshLeavesButton_Click(object sender, RoutedEventArgs e)
        {
            LoadLeaveData();
        }

        private async void ApproveLeaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LeaveRecord record)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to approve this leave request?\n\n" +
                    $"Employee: {record.employee_name}\n" +
                    $"Type: {record.leave_type}\n" +
                    $"Dates: {record.start_date:dd/MM/yyyy} - {record.end_date:dd/MM/yyyy}\n" +
                    $"Days: {record.total_days}",
                    "Confirm Approval", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                        var response = await httpClient.PostAsync(
                            $"{_backendUrl}/api/v1/leaves/{record.id}/approve", null);

                        if (response.IsSuccessStatusCode)
                        {
                            MessageBox.Show("Leave request approved successfully.", "Success", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadLeaveData();
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            MessageBox.Show($"Approval failed: {errorContent}", "Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error approving leave: {ex.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void RejectLeaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LeaveRecord record)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to reject this leave request?\n\n" +
                    $"Employee: {record.employee_name}\n" +
                    $"Type: {record.leave_type}\n" +
                    $"Dates: {record.start_date:dd/MM/yyyy} - {record.end_date:dd/MM/yyyy}\n" +
                    $"Days: {record.total_days}",
                    "Confirm Rejection", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                        var response = await httpClient.PostAsync(
                            $"{_backendUrl}/api/v1/leaves/{record.id}/reject", null);

                        if (response.IsSuccessStatusCode)
                        {
                            MessageBox.Show("Leave request rejected.", "Success", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadLeaveData();
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            MessageBox.Show($"Rejection failed: {errorContent}", "Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error rejecting leave: {ex.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void DeleteLeaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LeaveRecord record)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete this leave record?\n\n" +
                    $"Employee: {record.employee_name}\n" +
                    $"Type: {record.leave_type}\n" +
                    $"Dates: {record.start_date:dd/MM/yyyy} - {record.end_date:dd/MM/yyyy}\n" +
                    $"Status: {record.status}\n\n" +
                    $"This action cannot be undone.",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                        var response = await httpClient.DeleteAsync(
                            $"{_backendUrl}/api/v1/leaves/{record.id}");

                        if (response.IsSuccessStatusCode)
                        {
                            MessageBox.Show("Leave record deleted successfully.", "Success", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadLeaveData();
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            MessageBox.Show($"Delete failed: {errorContent}", "Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting leave: {ex.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DataGrid_LoadingRow(object sender, System.Windows.Controls.DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is LeaveRecord leave)
            {
                leave.RowNumber = e.Row.GetIndex() + 1;
            }

            // Defer so DataTemplate cells are fully applied before searching for named buttons
            // This handles both initial render AND virtualization recycling (scrolling)
            e.Row.Dispatcher.BeginInvoke(() =>
            {
                if (e.Row.DataContext is LeaveRecord record)
                    UpdateLeaveRowButtons(e.Row, record);
            }, System.Windows.Threading.DispatcherPriority.DataBind);
        }

        private void UpdateLeaveRowButtons(DataGridRow row, LeaveRecord record)
        {
            var approveButton = FindVisualChild<Button>(row, "ApproveButton");
            var rejectButton  = FindVisualChild<Button>(row, "RejectButton");
            var deleteButton  = FindVisualChild<Button>(row, "DeleteButton");

            bool isPending       = record.status?.ToLower() == "pending";
            bool canApproveReject = (UserSessionService.IsManager || UserSessionService.IsAdmin) && isPending;

            if (approveButton != null) approveButton.Visibility = canApproveReject      ? Visibility.Visible : Visibility.Collapsed;
            if (rejectButton  != null) rejectButton.Visibility  = canApproveReject      ? Visibility.Visible : Visibility.Collapsed;
            if (deleteButton  != null) deleteButton.Visibility  = UserSessionService.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion
    }
}