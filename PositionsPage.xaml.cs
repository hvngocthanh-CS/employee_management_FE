using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using EmployeeManagement.Services;

namespace EmployeeManagement
{
    public partial class PositionsPage : Page
    {
        private readonly string baseUrl = "http://127.0.0.1:8000/api/v1";

        public PositionsPage()
        {
            InitializeComponent();
            CheckPermissionsAndSetupUI();
            LoadPositions();
        }

        private void CheckPermissionsAndSetupUI()
        {
            // Check if user is authenticated
            if (!UserSessionService.IsAuthenticated)
            {
                MessageBox.Show("You need to be logged in to view positions.", "Authentication Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if user has permission to manage positions
            if (!UserSessionService.CanManagePositions)
            {
                // Hide Add button for employees
                AddPositionButton.Visibility = Visibility.Collapsed;
                
                // Hide Actions column (Edit/Delete) for employees
                ActionsColumn.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadPositions()
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.GetAsync($"{baseUrl}/positions");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var positions = JsonConvert.DeserializeObject<List<Position>>(content);
                    if (positions != null)
                        for (int i = 0; i < positions.Count; i++)
                            positions[i].RowNumber = i + 1;
                    PositionsDataGrid.ItemsSource = positions;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // User doesn't have permission - silently handle instead of showing popup
                    PositionsDataGrid.ItemsSource = new List<Position>();
                }
                else
                {
                    MessageBox.Show("Failed to load positions.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to server: {ex.Message}", "Connection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement search functionality
            var searchTerm = SearchTextBox.Text.ToLower();
            if (PositionsDataGrid.ItemsSource is List<Position> positions)
            {
                var filteredPositions = positions.FindAll(p => 
                    p.Title.ToLower().Contains(searchTerm) ||
                    p.Code.ToLower().Contains(searchTerm) ||
                    p.Level.ToLower().Contains(searchTerm));
                for (int i = 0; i < filteredPositions.Count; i++)
                    filteredPositions[i].RowNumber = i + 1;
                PositionsDataGrid.ItemsSource = filteredPositions;
            }
        }

        private void AddPositionButton_Click(object sender, RoutedEventArgs e)
        {
            // Open Add Position dialog
            var addDialog = new AddEditPositionDialog();
            if (addDialog.ShowDialog() == true)
            {
                AddPosition(addDialog.PositionData);
            }
        }

        private void EditPosition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Position position)
            {
                var editDialog = new AddEditPositionDialog(position);
                if (editDialog.ShowDialog() == true)
                {
                    UpdatePosition(position.Id, editDialog.PositionData);
                }
            }
        }

        private async void DeletePosition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Position position)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the position '{position.Title}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await DeletePosition(position.Id);
                }
            }
        }

        private async void AddPosition(Position positionData)
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var json = JsonConvert.SerializeObject(positionData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{baseUrl}/positions", content);
                
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Position added successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadPositions();
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to add position: {error}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding position: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void UpdatePosition(int id, Position positionData)
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var json = JsonConvert.SerializeObject(positionData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PutAsync($"{baseUrl}/positions/{id}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Position updated successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadPositions();
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to update position: {error}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating position: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeletePosition(int id)
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.DeleteAsync($"{baseUrl}/positions/{id}");
                
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Position deleted successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadPositions();
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to delete position: {error}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting position: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }  
        }

        private void DataGrid_LoadingRow(object sender, System.Windows.Controls.DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
            if (e.Row.DataContext is Position position)
            {
                position.RowNumber = e.Row.GetIndex() + 1;
            }
        }
    }

    // Position data model
    public class Position
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int RowNumber { get; set; }
    }

    // Placeholder for Add/Edit dialog - this would need to be implemented
    public class AddEditPositionDialog : Window
    {
        public Position PositionData { get; private set; } = new Position();
        
        public AddEditPositionDialog(Position? existingPosition = null)
        {
            Title = existingPosition == null ? "Add Position" : "Edit Position";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            
            if (existingPosition != null)
            {
                PositionData = existingPosition;
            }
        }
    }
}