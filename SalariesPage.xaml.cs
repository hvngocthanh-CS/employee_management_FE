using System.Windows.Controls;
using EmployeeManagement.Services;
using System.Windows;

namespace EmployeeManagement
{
    public partial class SalariesPage : Page
    {
        public SalariesPage()
        {
            InitializeComponent();
            CheckPermissionsAndSetupUI();
        }

        private void CheckPermissionsAndSetupUI()
        {
            if (!UserSessionService.IsAuthenticated)
            {
                MessageBox.Show("You need to be logged in to view salaries.", "Authentication Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Employee role can only view their own salary
            if (UserSessionService.IsEmployee)
            {
                // Hide any add/edit functionality for employees
                // Load only their own salary data
            }
        }
    }
}