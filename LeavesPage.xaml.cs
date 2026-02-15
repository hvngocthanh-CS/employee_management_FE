using System.Windows.Controls;
using EmployeeManagement.Services;
using System.Windows;

namespace EmployeeManagement
{
    public partial class LeavesPage : Page
    {
        public LeavesPage()
        {
            InitializeComponent();
            CheckPermissionsAndSetupUI();
        }

        private void CheckPermissionsAndSetupUI()
        {
            if (!UserSessionService.IsAuthenticated)
            {
                MessageBox.Show("You need to be logged in to view leaves.", "Authentication Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Employee role can only view/request their own leaves
            if (UserSessionService.IsEmployee)
            {
                // Hide approve/management functionality for employees
                // Load only their own leave requests
            }
        }
    }
}