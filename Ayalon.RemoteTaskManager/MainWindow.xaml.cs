using Microsoft.Windows.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.DirectoryServices.AccountManagement;
using Fluent;
using static Ayalon.RemoteTaskManager.RemoteTaskManager;

namespace Ayalon.RemoteTaskManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Fluent.RibbonWindow
    {
        private DispatcherTimer refreshTimer;
        private bool isConnected;
        private ICollectionView processesView;
        private ObservableCollection<RemoteProcessInfo> processesSource = new ObservableCollection<RemoteProcessInfo>();
        private string adSecurityGroup = "HdTechTeam";

        public MainWindow()
        {
            InitializeComponent();

            CheckIinitialAccess();

            processesView = CollectionViewSource.GetDefaultView(processesSource);

            if (!processesView.GroupDescriptions.Any())
            {
                processesView.GroupDescriptions.Add(new PropertyGroupDescription("Name"));
            }

            dgProcesses.ItemsSource = processesView;

            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(5);
            refreshTimer.Tick += RefreshTimer_Tick;
        }

        private void CheckIinitialAccess()
        {
            try
            {
                using (PrincipalContext pc = new PrincipalContext(ContextType.Domain))
                {
                    UserPrincipal user = UserPrincipal.Current;
                    if (!user.IsMemberOf(pc, IdentityType.Name, adSecurityGroup))
                    {
                        MessageBox.Show("Access Denied, your are not authorized to run this app", "Security", MessageBoxButton.OK, MessageBoxImage.Stop);
                        Application.Current.Shutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not verify domain credentials. " + ex.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Application.Current.Shutdown();
            }
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (isConnected)
            {
                RefreshData(txtComputerName.Text, isAutoRefresh: true);
            }
        }

        private async void RefreshData(string computerName, bool isAutoRefresh = false)
        {
            // אם זה ריענון ידני, נשנה את מצב הכפתור
            if (!isAutoRefresh)
            {
                btnConnect.IsEnabled = false;
                btnConnect.Content = "Connecting...";
            }

            try
            {
                // 1. שליפת תהליכים (Process List) ב-Thread נפרד
                List<RemoteProcessInfo> newProcesses = await Task.Run(() =>
                {
                    return GetProcesses(computerName);
                });

                // 2. עדכון או יצירת ה-ICollectionView
                this.Dispatcher.Invoke(() =>
                {
                    processesSource.Clear();
                    foreach (var process in newProcesses)
                    {
                        processesSource.Add(process);
                    }
                    ApplyFilter();
                });

                // 3. שליפת נתוני המערכת הכלליים
                SystemPerformance performance = await Task.Run(() =>
                {
                    return SystemPerformance.GetSystemPerformance(computerName);
                });

                // 4. עדכון ה-UI
                dgProcesses.ItemsSource = processesView;
                txtCpuUsage.Text = $"CPU Usage: {performance.CpuUsagePercent:N1}%";
                txtFreeMemory.Text = $"Free RAM: {performance.FreeMemoryMB:N0} MB";

                isConnected = true;

                // 5. הפעלת הטיימר ועדכון פילטר
                if (!refreshTimer.IsEnabled)
                {
                    refreshTimer.Start();
                }

                // יישום סימון ויזואלי בהתאם לטקסט הקיים בתיבת החיפוש
                ApplyFilter();

                processesView.Refresh();

                if (!isAutoRefresh)
                {
                    MessageBox.Show($"Successfully connected and retrieved {newProcesses.Count} processes.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // עצירת הטיימר על כל שגיאת חיבור
                refreshTimer.Stop();
                isConnected = false;

                if (!isAutoRefresh)
                {
                    MessageBox.Show($"Connection failed: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                this.Dispatcher.Invoke(() =>
                {
                    processesSource.Clear();
                });

                // ניקוי נתונים
                //dgProcesses.ItemsSource = null;
                txtCpuUsage.Text = "CPU Usage: N/A";
                txtFreeMemory.Text = "Free RAM: N/A";
            }
            finally
            {
                // החזרת מצב הכפתור רק אם זה לא ריענון אוטומטי
                if (!isAutoRefresh)
                {
                    btnConnect.IsEnabled = true;
                    btnConnect.Content = "Connect / Refresh";
                }
            }
        }


        private void ApplyFilter()
        {
            string filterText = txtFilter.Text?.ToLower() ?? string.Empty;

            /*if (processesView != null && processesView.SourceCollection is ObservableCollection <RemoteProcessInfo> processes)
            {
                if (string.IsNullOrWhiteSpace(filterText))
                {
                    foreach (var process in processes)
                    {
                        process.IsFilteredMatch = false;
                    }
                }
                else
                {
                    foreach (var process in processes)
                    { 
                        bool isMach = process.Name.ToLower().Contains(filterText) || process.Path != null && process.Path.ToLower().Contains(filterText);
                        process.IsFilteredMatch = isMach;
                    }
                }

                processesView.Refresh();
            }*/
            if (string.IsNullOrWhiteSpace(filterText))
            {
                processesView.Filter = null;
            }
            else
            {
                processesView.Filter = item =>
                {
                    if (item is RemoteProcessInfo p)
                    {
                        return p.Name.ToLower().Contains(filterText) || (p.Path != null && p.Path.ToLower().Contains(filterText));
                    }
                    return false;
                };
            }
            processesView.Refresh();
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            string computerName = txtComputerName.Text;
         
            /*if (string.IsNullOrEmpty(computerName) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter computer name, username, and password.", "Missing Credentials", MessageBoxButton.OK, MessageBoxImage.Warning);            
                return;
            }*/

            RefreshData(computerName);
        }

        private void btnTerminate_Click(object sender, RoutedEventArgs e)
        {
            if (dgProcesses.SelectedItem is RemoteProcessInfo selectedProcess)
            {
                try
                {
                    if (MessageBox.Show($"Are you sure you want to terminate the process: {selectedProcess.Name} (PID: {selectedProcess.ProcessId})?",
                                        "Confirm Termination",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        TerminateProcess(selectedProcess);

                        MessageBox.Show($"Process {selectedProcess.Name} terminated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                        // רענון מיידי של הנתונים
                        btnConnect_Click(sender, e);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to terminate process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a process to terminate.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ThemeChange_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as RibbonButton;
            if (button == null) return;

            string themeTag = button.Tag.ToString();
            MessageBox.Show($"Theme changing to: {themeTag}");
        }
    }
}