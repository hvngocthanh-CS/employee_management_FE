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
using System.Windows.Input;
using System.Windows.Media;
using EmployeeManagement.Services;

namespace EmployeeManagement
{
    public partial class AttendancesPage : Page
    {
        private readonly string _backendUrl = "http://localhost:8000";
        private ObservableCollection<AttendanceRecord> _attendanceRecords = new ObservableCollection<AttendanceRecord>();
        private int? _selectedEmployeeId = null;
        private string _selectedEmployeeName = null;
        private System.Threading.CancellationTokenSource? _searchCancellation = null;
        private bool _isUpdatingSearchBox = false;
        private bool _isLoadingAttendance = false;

        public AttendancesPage()
        {
            InitializeComponent();
            CheckPermissionsAndSetupUI();
        }

        private void CheckPermissionsAndSetupUI()
        {
            if (!UserSessionService.IsAuthenticated)
            {
                MessageBox.Show("You need to be logged in to view attendance.", "Authentication Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AttendanceDataGrid.ItemsSource = _attendanceRecords;

            if (UserSessionService.IsEmployee)
            {
                // Employee: Show check-in/check-out panel, hide search and employee columns
                CheckInOutPanel.Visibility = Visibility.Visible;
                EmployeeSearchPanel.Visibility = Visibility.Collapsed;
                EmployeeFilterPanel.Visibility = Visibility.Collapsed;
                EmployeeCodeColumn.Visibility = Visibility.Collapsed;
                EmployeeNameColumn.Visibility = Visibility.Collapsed;
                DepartmentColumn.Visibility = Visibility.Collapsed;
                
                UpdateCheckInOutStatus();
                LoadAttendanceRecords();
            }
            else
            {
                // Admin/Manager: Show search panel and employee columns
                CheckInOutPanel.Visibility = Visibility.Collapsed;
                EmployeeSearchPanel.Visibility = Visibility.Visible;
                EmployeeFilterPanel.Visibility = Visibility.Visible;
                EmployeeCodeColumn.Visibility = Visibility.Visible;
                EmployeeNameColumn.Visibility = Visibility.Visible;
                DepartmentColumn.Visibility = Visibility.Visible;
                
                // Show action buttons in data grid based on role
                AttendanceDataGrid.LoadingRow += AttendanceDataGrid_LoadingRow;
                
                LoadAttendanceRecords();
            }
        }

        #region Data Models
        public class AttendanceRecord
        {
            public int id { get; set; }
            public int employee_id { get; set; }
            public DateTime attendance_date { get; set; }
            public string? check_in_time { get; set; }
            public string? check_out_time { get; set; }
            public decimal? working_hours { get; set; }
            public string status { get; set; } = "";
            public string employee_name { get; set; } = "";
            public string employee_code { get; set; } = "";
            public string department_name { get; set; } = "";
            
            // Formatted time properties for display
            public string CheckInDisplay => FormatTime(check_in_time);
            public string CheckOutDisplay => FormatTime(check_out_time);
            
            private string FormatTime(string? timeString)
            {
                if (string.IsNullOrEmpty(timeString))
                    return "";
                    
                // Parse time string like "17:45:43.100721" and return "HH:mm"
                if (TimeSpan.TryParse(timeString, out TimeSpan time))
                {
                    return $"{time.Hours:D2}:{time.Minutes:D2}";
                }
                
                return timeString;
            }
        }

        public class CheckInRequest
        {
            public int employee_id { get; set; }
            public DateTime? check_in_time { get; set; }
        }

        public class CheckOutRequest
        {
            public int employee_id { get; set; }
            public DateTime? check_out_time { get; set; }
        }

        public class Employee
        {
            public int id { get; set; }
            public string employee_code { get; set; } = "";
            public string full_name { get; set; } = "";
            public string email { get; set; } = "";
            public string? department_name { get; set; }
            public string? position_title { get; set; }
            
            public string DisplayText => string.IsNullOrEmpty(department_name) 
                ? $"{employee_code} - {full_name}"
                : $"{employee_code} - {full_name} ({department_name})";
        }

        public class AttendanceCreateRequest
        {
            public int employee_id { get; set; }
            public string date { get; set; } = "";
            public string? check_in_time { get; set; }
            public string? check_out_time { get; set; }
            public string status { get; set; } = "present";
        }

        public class AttendanceUpdateRequest
        {
            public string? check_in_time { get; set; }
            public string? check_out_time { get; set; }
            public string? status { get; set; }
        }
        #endregion

        #region UI Setup
        private void AttendanceDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is AttendanceRecord record)
            {
                var editButton = FindVisualChild<Button>(e.Row, "EditButton");
                var deleteButton = FindVisualChild<Button>(e.Row, "DeleteButton");

                if (UserSessionService.IsAdmin)
                {
                    if (editButton != null) editButton.Visibility = Visibility.Visible;
                    if (deleteButton != null) deleteButton.Visibility = Visibility.Visible;
                }
                else if (UserSessionService.IsManager)
                {
                    if (editButton != null) editButton.Visibility = Visibility.Visible;
                    if (deleteButton != null) deleteButton.Visibility = Visibility.Collapsed;
                }
            }
        }

        private T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T element && (string.IsNullOrEmpty(name) || element.Name == name))
                {
                    return element;
                }

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }
        #endregion

        #region Employee Search
        private async void EmployeeSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Skip if we're programmatically updating the textbox
            if (_isUpdatingSearchBox)
                return;
                
            var searchText = EmployeeSearchTextBox.Text?.Trim();
            
            // Update placeholder visibility
            SearchPlaceholderText.Visibility = string.IsNullOrEmpty(searchText) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
            
            // Cancel previous search
            _searchCancellation?.Cancel();
            
            if (string.IsNullOrEmpty(searchText))
            {
                // Clear filter when search box is empty (only if a filter was previously set)
                if (_selectedEmployeeId.HasValue)
                {
                    _selectedEmployeeId = null;
                    _selectedEmployeeName = null;
                    SelectedEmployeeLabel.Text = "All Employees";
                    ClearFilterButton.Visibility = Visibility.Collapsed;
                    LoadAttendanceRecords();
                }
                return;
            }
            
            if (searchText.Length < 2)
            {
                return; // Don't search with less than 2 characters
            }

            // Debounce: wait 500ms before searching
            _searchCancellation = new System.Threading.CancellationTokenSource();
            var token = _searchCancellation.Token;
            
            try
            {
                await Task.Delay(500, token);
                
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/employees/?search={Uri.EscapeDataString(searchText)}", token);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var employees = JsonSerializer.Deserialize<List<Employee>>(jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new List<Employee>();
                    
                    if (employees.Count == 0)
                    {
                        // Show no results message on UI
                        SearchPlaceholderText.Text = "No employees found";
                        SearchPlaceholderText.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                        SearchPlaceholderText.Visibility = Visibility.Visible;
                        
                        // Clear the table since no employee was found
                        _selectedEmployeeId = null;
                        _selectedEmployeeName = null;
                        SelectedEmployeeLabel.Text = "All Employees";
                        ClearFilterButton.Visibility = Visibility.Collapsed;
                        _attendanceRecords.Clear();
                    }
                    else if (employees.Count == 1)
                    {
                        // Auto-select if only one result
                        SelectEmployee(employees[0]);
                    }
                    else
                    {
                        // Show selection dialog for multiple results
                        ShowEmployeeSelectionDialog(employees);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Search was cancelled, ignore
            }
            catch (Exception ex)
            {
                // Show error on UI instead of MessageBox
                SearchPlaceholderText.Text = "Error searching employees";
                SearchPlaceholderText.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                SearchPlaceholderText.Visibility = Visibility.Visible;
            }
        }

         private void ShowEmployeeSelectionDialog(List<Employee> employees)
        {
            var dialog = new Window
            {
                Title = "Select Employee",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(236, 240, 241))
            };

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Header
            var headerText = new TextBlock
            {
                Text = $"Found {employees.Count} employee(s). Select one:",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(headerText, 0);
            grid.Children.Add(headerText);

            var listBox = new ListBox
            {
                ItemsSource = employees,
                DisplayMemberPath = "DisplayText",
                FontSize = 14,
                Padding = new Thickness(5)
            };
            Grid.SetRow(listBox, 1);
            grid.Children.Add(listBox);

            listBox.MouseDoubleClick += (s, e) =>
            {
                if (listBox.SelectedItem is Employee emp)
                {
                    SelectEmployee(emp);
                    dialog.Close();
                }
            };

            // Cancel button
            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            cancelButton.Click += (s, e) => 
            {
                // Clear search box when dialog is cancelled
                _isUpdatingSearchBox = true;
                EmployeeSearchTextBox.Text = "";
                _isUpdatingSearchBox = false;
                SearchPlaceholderText.Text = "Type at least 2 characters to search...";
                SearchPlaceholderText.Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166));
                SearchPlaceholderText.Visibility = Visibility.Visible;
                dialog.Close();
            };
            Grid.SetRow(cancelButton, 2);
            grid.Children.Add(cancelButton);

            dialog.Content = grid;
            dialog.ShowDialog();
        }

        private void SelectEmployee(Employee employee)
        {
            _selectedEmployeeId = employee.id;
            _selectedEmployeeName = employee.full_name;
            SelectedEmployeeLabel.Text = $"{employee.employee_code} - {employee.full_name}";
            ClearFilterButton.Visibility = Visibility.Visible;
            
            // Keep search text, just hide placeholder
            SearchPlaceholderText.Visibility = Visibility.Collapsed;
            
            LoadAttendanceRecords();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            _selectedEmployeeId = null;
            _selectedEmployeeName = null;
            SelectedEmployeeLabel.Text = "All Employees";
            ClearFilterButton.Visibility = Visibility.Collapsed;
            
            // Clear search box (with flag to prevent re-triggering TextChanged)
            _isUpdatingSearchBox = true;
            EmployeeSearchTextBox.Text = "";
            _isUpdatingSearchBox = false;
            
            SearchPlaceholderText.Text = "Type at least 2 characters to search...";
            SearchPlaceholderText.Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166));
            SearchPlaceholderText.Visibility = Visibility.Visible;
            LoadAttendanceRecords();
        }
        #endregion

        #region Data Loading
        private async void LoadAttendanceRecords()
        {
            // Prevent duplicate concurrent loads
            if (_isLoadingAttendance)
                return;
                
            _isLoadingAttendance = true;
            
            try
            {
                LoadingLabel.Visibility = Visibility.Visible;
                _attendanceRecords.Clear();

                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                
                string endpoint;
                if (UserSessionService.IsEmployee)
                {
                    endpoint = $"{_backendUrl}/api/v1/attendances/my-attendances";
                }
                else
                {
                    endpoint = $"{_backendUrl}/api/v1/attendances/";
                    
                    if (_selectedEmployeeId.HasValue)
                    {
                        endpoint += $"?employee_id={_selectedEmployeeId.Value}";
                    }
                }

                // Add date filters
                var urlParams = new List<string>();
                if (endpoint.Contains("?"))
                {
                    if (StartDatePicker.SelectedDate.HasValue)
                        urlParams.Add($"start_date={StartDatePicker.SelectedDate.Value:yyyy-MM-dd}");
                    if (EndDatePicker.SelectedDate.HasValue)
                        urlParams.Add($"end_date={EndDatePicker.SelectedDate.Value:yyyy-MM-dd}");
                    
                    if (urlParams.Count > 0)
                        endpoint += "&" + string.Join("&", urlParams);
                }
                else
                {
                    if (StartDatePicker.SelectedDate.HasValue)
                        urlParams.Add($"start_date={StartDatePicker.SelectedDate.Value:yyyy-MM-dd}");
                    if (EndDatePicker.SelectedDate.HasValue)
                        urlParams.Add($"end_date={EndDatePicker.SelectedDate.Value:yyyy-MM-dd}");
                    
                    if (urlParams.Count > 0)
                        endpoint += "?" + string.Join("&", urlParams);
                }

                var response = await httpClient.GetAsync(endpoint);
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var attendances = JsonSerializer.Deserialize<List<AttendanceRecord>>(jsonContent, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                        ?? new List<AttendanceRecord>();

                    attendances = attendances.OrderByDescending(a => a.attendance_date).ToList();

                    foreach (var attendance in attendances)
                    {
                        _attendanceRecords.Add(attendance);
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to load attendance data: {errorContent}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading attendance data: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingLabel.Visibility = Visibility.Collapsed;                _isLoadingAttendance = false;            }
        }

        private void UpdateActionButtonsVisibility()
        {
            // Use dispatcher to ensure DataGrid is fully rendered
            Dispatcher.InvokeAsync(() =>
            {
                AttendanceDataGrid.UpdateLayout();
                
                for (int i = 0; i < AttendanceDataGrid.Items.Count; i++)
                {
                    var row = (DataGridRow)AttendanceDataGrid.ItemContainerGenerator.ContainerFromIndex(i);
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

        private async void UpdateCheckInOutStatus()
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var today = DateTime.Today.ToString("yyyy-MM-dd");
                var response = await httpClient.GetAsync(
                    $"{_backendUrl}/api/v1/attendances/my-attendances?start_date={today}&end_date={today}"
                );

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var attendances = JsonSerializer.Deserialize<List<AttendanceRecord>>(jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new List<AttendanceRecord>();

                    var todayAttendance = attendances.FirstOrDefault();
                    if (todayAttendance != null)
                    {
                        if (!string.IsNullOrEmpty(todayAttendance.check_out_time))
                        {
                            LastActionLabel.Text = $"Checked out at {todayAttendance.CheckOutDisplay}";
                            CheckInButton.IsEnabled = false;
                            CheckOutButton.IsEnabled = false;
                        }
                        else if (!string.IsNullOrEmpty(todayAttendance.check_in_time))
                        {
                            LastActionLabel.Text = $"Checked in at {todayAttendance.CheckInDisplay}";
                            CheckInButton.IsEnabled = false;
                            CheckOutButton.IsEnabled = true;
                        }
                    }
                    else
                    {
                        LastActionLabel.Text = "No attendance record for today";
                        CheckInButton.IsEnabled = true;
                        CheckOutButton.IsEnabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                LastActionLabel.Text = $"Error: {ex.Message}";
            }
        }
        #endregion

        #region Check In/Out Actions
        private async void CheckInButton_Click(object sender, RoutedEventArgs e)
        {
            if (!UserSessionService.CurrentUser?.employee_id.HasValue ?? true)
            {
                MessageBox.Show("No employee record found.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var request = new CheckInRequest
                {
                    employee_id = UserSessionService.CurrentUser.employee_id.Value,
                    check_in_time = null
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.PostAsync($"{_backendUrl}/api/v1/attendances/check-in", content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Checked in successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadAttendanceRecords();
                    UpdateCheckInOutStatus();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to check in: {errorContent}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CheckOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (!UserSessionService.CurrentUser?.employee_id.HasValue ?? true)
            {
                MessageBox.Show("No employee record found.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var request = new CheckOutRequest
                {
                    employee_id = UserSessionService.CurrentUser.employee_id.Value,
                    check_out_time = null
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.PostAsync($"{_backendUrl}/api/v1/attendances/check-out", content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Checked out successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadAttendanceRecords();
                    UpdateCheckInOutStatus();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to check out: {errorContent}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Event Handlers
        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Only load if attendance records is already initialized (not during page load)
            if (_attendanceRecords != null && AttendanceDataGrid.ItemsSource != null)
            {
                LoadAttendanceRecords();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadAttendanceRecords();
            if (UserSessionService.IsEmployee)
            {
                UpdateCheckInOutStatus();
            }
        }

        private void AddAttendanceButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedEmployeeId.HasValue)
            {
                MessageBox.Show("Please select an employee first.", "No Employee Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ShowAddEditAttendanceDialog(null, _selectedEmployeeId.Value, _selectedEmployeeName);
        }

        private void EditAttendanceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AttendanceRecord record)
            {
                ShowAddEditAttendanceDialog(record, record.employee_id, record.employee_name);
            }
        }

        private async void DeleteAttendanceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AttendanceRecord record)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete attendance record for {record.employee_name} on {record.attendance_date:dd/MM/yyyy}?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                        var response = await httpClient.DeleteAsync($"{_backendUrl}/api/v1/attendances/{record.id}");

                        if (response.IsSuccessStatusCode)
                        {
                            MessageBox.Show("Attendance deleted successfully!", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadAttendanceRecords();
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            MessageBox.Show($"Failed to delete: {errorContent}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        #endregion

        #region Add/Edit Dialog
        private void ShowAddEditAttendanceDialog(AttendanceRecord? existing, int employeeId, string employeeName)
        {
            var dialog = new Window
            {
                Title = existing == null ? "Add Attendance" : "Edit Attendance",
                Width = 400,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(236, 240, 241))
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Employee Name
            var employeeLabel = new TextBlock
            {
                Text = $"Employee: {employeeName}",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(employeeLabel, 0);
            grid.Children.Add(employeeLabel);

            // Date
            var dateLabel = new TextBlock { Text = "Date:", Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(dateLabel, 1);
            grid.Children.Add(dateLabel);

            var datePicker = new DatePicker
            {
                Margin = new Thickness(0, 0, 0, 10),
                SelectedDate = existing?.attendance_date ?? DateTime.Today,
                IsEnabled = existing == null
            };
            Grid.SetRow(datePicker, 2);
            grid.Children.Add(datePicker);

            // Check In Time
            var checkInLabel = new TextBlock { Text = "Check In Time (HH:mm):", Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(checkInLabel, 3);
            grid.Children.Add(checkInLabel);

            var checkInTextBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                Text = existing != null ? existing.CheckInDisplay : "08:00"
            };
            Grid.SetRow(checkInTextBox, 4);
            grid.Children.Add(checkInTextBox);

            // Check Out Time
            var checkOutLabel = new TextBlock { Text = "Check Out Time (HH:mm):", Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(checkOutLabel, 5);
            grid.Children.Add(checkOutLabel);

            var checkOutTextBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                Text = existing != null ? existing.CheckOutDisplay : "17:00"
            };
            Grid.SetRow(checkOutTextBox, 6);
            grid.Children.Add(checkOutTextBox);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var saveButton = new Button
            {
                Content = "Save",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            saveButton.Click += async (s, e) =>
            {
                await SaveAttendance(dialog, existing, employeeId, datePicker, checkInTextBox, checkOutTextBox);
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            cancelButton.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 7);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            dialog.ShowDialog();
        }

        private async Task SaveAttendance(Window dialog, AttendanceRecord? existing, int employeeId,
            DatePicker datePicker, TextBox checkInTextBox, TextBox checkOutTextBox)
        {
            if (!datePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Please select a date.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var checkInText = checkInTextBox.Text.Trim();
            var checkOutText = checkOutTextBox.Text.Trim();

            if (!string.IsNullOrEmpty(checkInText) && !TimeSpan.TryParse(checkInText, out _))
            {
                MessageBox.Show("Invalid check-in time format. Use HH:mm (e.g., 08:00)",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(checkOutText) && !TimeSpan.TryParse(checkOutText, out _))
            {
                MessageBox.Show("Invalid check-out time format. Use HH:mm (e.g., 17:00)",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                HttpResponseMessage response;

                if (existing == null)
                {
                    var request = new AttendanceCreateRequest
                    {
                        employee_id = employeeId,
                        date = datePicker.SelectedDate.Value.ToString("yyyy-MM-dd"),
                        check_in_time = string.IsNullOrEmpty(checkInText) ? null : checkInText + ":00",
                        check_out_time = string.IsNullOrEmpty(checkOutText) ? null : checkOutText + ":00",
                        status = "present"
                    };

                    var json = JsonSerializer.Serialize(request);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = await httpClient.PostAsync($"{_backendUrl}/api/v1/attendances/", content);
                }
                else
                {
                    var request = new AttendanceUpdateRequest
                    {
                        check_in_time = string.IsNullOrEmpty(checkInText) ? null : checkInText + ":00",
                        check_out_time = string.IsNullOrEmpty(checkOutText) ? null : checkOutText + ":00",
                        status = "present"
                    };

                    var json = JsonSerializer.Serialize(request);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = await httpClient.PutAsync($"{_backendUrl}/api/v1/attendances/{existing.id}", content);
                }

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        existing == null ? "Attendance added successfully!" : "Attendance updated successfully!",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information
                    );
                    dialog.Close();
                    LoadAttendanceRecords();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Error: {errorContent}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving attendance: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}