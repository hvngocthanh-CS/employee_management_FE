using System.Windows.Controls;
using EmployeeManagement.Services;
using System.Windows;

namespace EmployeeManagement
{
    public partial class AttendancesPage : Page
    {
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

            // Employee role can only view/mark their own attendance
            if (UserSessionService.IsEmployee)
            {
                // Hide any management functionality for employees
                // Load only their own attendance data
            }
        }
    }
}