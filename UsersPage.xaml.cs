using System.Windows.Controls;
using EmployeeManagement.Services;
using System.Windows;

namespace EmployeeManagement
{
    public partial class UsersPage : Page
    {
        public UsersPage()
        {
            InitializeComponent();
            CheckPermissionsAndSetupUI();
        }

        private void CheckPermissionsAndSetupUI()
        {
            if (!UserSessionService.IsAuthenticated)
            {
                MessageBox.Show("You need to be logged in to view users.", "Authentication Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Only admin/manager can access Users page
            if (UserSessionService.IsEmployee)
            {
                MessageBox.Show("You don't have permission to access user management.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                
                // Navigate back to Dashboard or Employee page
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowDashboard();
                return;
            }
        }
    }
}