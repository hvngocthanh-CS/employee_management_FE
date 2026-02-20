using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Windows;
using Newtonsoft.Json.Linq;
using EmployeeManagement.Services;

namespace EmployeeManagement
{
    public partial class DepartmentStatisticsWindow : Window
    {
        private readonly string _backendUrl = "http://127.0.0.1:8000";
        private readonly int _departmentId;

        public DepartmentStatisticsWindow(int departmentId)
        {
            InitializeComponent();
            _departmentId = departmentId;
            LoadStatistics();
        }

        private async void LoadStatistics()
        {
            try
            {
                using var httpClient = UserSessionService.GetAuthenticatedHttpClient();
                var response = await httpClient.GetAsync($"{_backendUrl}/api/v1/departments/{_departmentId}/statistics");

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Lỗi khi tải thống kê", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var stats = JObject.Parse(json);

                // Set title
                TitleTextBlock.Text = $"Thống Kê: {stats["department_name"]}";

                // Summary
                TotalEmployeesText.Text = stats["total_employees"]?.ToString() ?? "0";
                
                // Count unique positions
                var positions = stats["employee_breakdown_by_position"] as JArray;
                UniquePositionsText.Text = positions?.Count.ToString() ?? "0";

                // Position breakdown
                if (positions != null && positions.Count > 0)
                {
                    var positionList = new List<PositionBreakdown>();
                    foreach (var pos in positions)
                    {
                        positionList.Add(new PositionBreakdown
                        {
                            position_title = pos["position_title"]?.ToString() ?? "N/A",
                            count = int.Parse(pos["count"]?.ToString() ?? "0")
                        });
                    }
                    PositionsDataGrid.ItemsSource = positionList;
                }

                // Salary stats
                var salaryStats = stats["salary_stats"];
                if (salaryStats != null)
                {
                    TotalBudgetText.Text = FormatCurrency(salaryStats["total_salary_budget"]?.ToString());
                    AvgSalaryText.Text = FormatCurrency(salaryStats["average_salary"]?.ToString());
                    MinSalaryText.Text = FormatCurrency(salaryStats["min_salary"]?.ToString());
                    MaxSalaryText.Text = FormatCurrency(salaryStats["max_salary"]?.ToString());
                }
                else
                {
                    TotalBudgetText.Text = "0 VND";
                    AvgSalaryText.Text = "0 VND";
                    MinSalaryText.Text = "0 VND";
                    MaxSalaryText.Text = "0 VND";
                }

                // Employee info
                var newest = stats["newest_employee"];
                if (newest != null)
                {
                    NewestEmployeeText.Text = $"{newest["name"]}\nHire date: {newest["hire_date"]}";
                }
                else
                {
                    NewestEmployeeText.Text = "No data";
                }

                var longest = stats["longest_serving_employee"];
                if (longest != null)
                {
                    LongestServingText.Text = $"{longest["name"]}\nHire date: {longest["hire_date"]}";
                }
                else
                {
                    LongestServingText.Text = "No data";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private string FormatCurrency(string? value)
        {
            if (string.IsNullOrEmpty(value) || !decimal.TryParse(value, out decimal amount))
            {
                return "0 VND";
            }
            return $"{amount:N0} VND";
        }
    }

    public class PositionBreakdown
    {
        public string position_title { get; set; } = string.Empty;
        public int count { get; set; }
    }
}
