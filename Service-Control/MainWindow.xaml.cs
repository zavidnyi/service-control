﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.ServiceProcess;
using System.Management;
using System.ComponentModel;

namespace Service_Control {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public class ServiceInfo : INotifyPropertyChanged {
        private string _status;
        private bool _canBeStopped;
        private bool _canBeContinued;
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Status {
            get {
                string _s = new ServiceController(Name).Status.ToString();
                if (_s != _status) {
                    _status = _s;
                    OnPropertyChanged("Status");
                    CanBeStopped = Status == "Running" ? true : false;
                    CanBeContinued = Status == "Stopped" ? true : false;
                }
                return _status;
            }
            set { 
                _status = value; 
                OnPropertyChanged("Status"); 
                CanBeStopped = Status == "Running" ? true : false; 
                CanBeContinued = Status == "Stopped" ? true : false; 
            } 
        }
        public string Account { get; set; }
        public bool CanBeStopped { get => _canBeStopped; set { _canBeStopped = value; OnPropertyChanged("CanBeStopped"); } }
        public bool CanBeContinued { get => _canBeContinued; set { _canBeContinued = value; OnPropertyChanged("CanBeContinued"); } }

        public ServiceInfo(ManagementObject so) {
            Name = so["Name"].ToString();
            DisplayName = so["DisplayName"].ToString();
            Status = so["Status"].ToString();
            CanBeStopped = Status == "Running" ? true : false;
            CanBeContinued = Status == "Stopped" ? true : false;
            try
            {
                Account = so["StartName"].ToString();
            } catch (Exception)
            {
                // According to https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/win32-service
                // Start Name is null when service was created by I/O system
                Account = "I/O system";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public partial class MainWindow : Window {

        private ObservableCollection<ServiceInfo> services;

        public MainWindow() {
            InitializeComponent();
            
            // Find all availiable services
            SelectQuery query = new SelectQuery(string.Format("SELECT * FROM Win32_Service"));
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                var col = searcher.Get();
                ManagementObject[] arr = new ManagementObject[col.Count];
                col.CopyTo(arr, 0);
                services = new ObservableCollection<ServiceInfo>();
                foreach (ManagementObject s in arr)
                {
                    services.Add( new ServiceInfo(s));
                }
            }
            serviceInfo.DataContext = services;
            // Update statuses of services once evry second
            // There used to be an event which is triggered when status is changes, but it was in 2008
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(RefreshStatuses);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        // Refreshes statuses by calling property getter, which will trigger an update if status is changed
        public void RefreshStatuses (object sender, EventArgs e) { foreach (ServiceInfo s in services) { var _ = s.Status; } }

        private void Stop(object sender, RoutedEventArgs e) {
            Button b = (Button)sender;
            // I really don't like this type casts
            // But I failed to figure out how to correctly use bindings :)
            var service = (ServiceInfo)((Button)sender).Tag;
            ServiceController sc = new ServiceController(service.Name);
            try {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped);
                service.Status = "Stopped";
            } catch (Exception) {
                MessageBox.Show("Service could not be stopped.", "Stopping issue", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Continue(object sender, RoutedEventArgs e) {
            Button b = (Button)sender;
            var service = (ServiceInfo)((Button)sender).Tag;
            ServiceController sc = new ServiceController(service.Name);
            try {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running);
                service.Status = "Running";
            }
            catch (Exception){
                MessageBox.Show("Service could not be started, some services can't be started unless in use by other services.", "Starting issue", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
