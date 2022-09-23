using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using SDKTemplate;
using Windows.UI.Core;
using Windows.Devices.Bluetooth;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Composition;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace XsensBLE_Communication
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<BluetoothLEDeviceDisplay> KnownDevices = new ObservableCollection<BluetoothLEDeviceDisplay>();
        private List<DeviceInformation> UnknownDevices = new List<DeviceInformation>();
        private List<XsensDotDevice> xSensDotDevicesList = new List<XsensDotDevice>(); // Items queued for measurement

        private ConcurrentDictionary<XsensDotDevice, Quaternion> quaternionsDictionary = new ConcurrentDictionary<XsensDotDevice, Quaternion>();

        private string allMessages = "";
        private string allStreams = "";
        private bool isStreaming = false; // this is used for the multi-threaded streaming

        private PayloadType payloadType = PayloadType.CompleteQuaternion; // Default flag for recording and display types

        private string rootPath = @"C:\Users\masee\Downloads";
        private DeviceWatcher deviceWatcher;

        public string SelectedBleDeviceId;

        public string SelectedBleDeviceName = "No device selected";

        #region UI related items

        /// <summary>
        ///     Publishes messages on the UI in the MessageBox TextBox
        /// </summary>
        public void NotifyUser(string strMessage)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                allMessages = strMessage + "\n" + allMessages;
                MessageBox.Text = allMessages;
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    allMessages = strMessage + "\n" + allMessages;
                    MessageBox.Text = allMessages;
                });
            }
        }

        /// <summary>
        ///     Publishes messages on the UI in the SteamBox TextBox. Targets the incoming streams from the individual IMUs.
        /// </summary>
        public void StreamData(string steamedData)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                allStreams = steamedData + "\n" + allStreams;
                StreamBox.Text = allStreams;
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    allStreams = steamedData + "\n" + allStreams;
                    StreamBox.Text = allStreams;
                });
            }

            // We do a snip to keep the printing snappy
            if (allStreams.Length > 2000)
            {
                allStreams = allStreams.Substring(0, 1900);
            }
        }

        public void UpdateQuaternionsRegistry(XsensDotDevice refDevice, Quaternion newQuaternion)
        {
            if (quaternionsDictionary.ContainsKey(refDevice))
            {
                quaternionsDictionary[refDevice] = newQuaternion;
            }
            else
            {
                NotifyUser($"[ERR] Cannot update the quaternions dictionary since {refDevice.UniqueDeviceName} is not in the dictionary.");
            }

            if (quaternionsDictionary.Count >= 2) // If we dont have two devices queued up, can't get an angle can we now?
            {
                // Fire & Forget kinda task
                Task.Run(() =>
                  {
                      Vector3 angle;
                      Quaternion deltaQuaternion = Quaternion.Identity;

                      deltaQuaternion = quaternionsDictionary.ElementAt(0).Value * Quaternion.Inverse(quaternionsDictionary.ElementAt(1).Value);
                      angle = ToEulerAngles(deltaQuaternion);

                      if (angle.X > 180) { angle.X -= 360.0f; }
                      if (angle.Y > 180) { angle.Y -= 360.0f; }
                      if (angle.Z > 180) { angle.Z -= 360.0f; }

                    // Update the UI
                    var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        //InstantAngleX.Text = angle.X.ToString();
                        //InstantAngleY.Text = angle.Y.ToString();
                        //InstantAngleZ.Text = angle.Z.ToString();

                        InstantAngleX.Text = Math.Round(angle.X, 1).ToString();
                        InstantAngleY.Text = Math.Round(angle.Y, 1).ToString();
                        InstantAngleZ.Text = Math.Round(angle.Z, 1).ToString();
                    });
                });
            }
        }


        // Bit of a math method here from https://stackoverflow.com/questions/70462758/c-sharp-how-to-convert-quaternions-to-euler-angles-xyz
        public static Vector3 ToEulerAngles(Quaternion q)
        {
            Vector3 angleInRad = new Vector3();
            Vector3 angleInDeg = new Vector3();

            // roll / x
            double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            angleInRad.X = (float)Math.Atan2(sinr_cosp, cosr_cosp);

            // pitch / y
            double sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (Math.Abs(sinp) >= 1)
            {
                if (sinp >= 0) // positive
                {
                    angleInRad.Y = (float)Math.PI / 2;
                }
                else // she negative
                {
                    angleInRad.Y = -(float)Math.PI / 2;
                }
            }
            else
            {
                angleInRad.Y = (float)Math.Asin(sinp);
            }

            // yaw / z
            double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            angleInRad.Z = (float)Math.Atan2(siny_cosp, cosy_cosp);

            angleInDeg.X = 180.0f / (float)Math.PI * angleInRad.X;
            angleInDeg.Y = 180.0f / (float)Math.PI * angleInRad.Y;
            angleInDeg.Z = 180.0f / (float)Math.PI * angleInRad.Z;

            return angleInDeg;
        }


        public MainPage()
        {
            this.InitializeComponent();
            NotifyUser("[info] Message Box");
            StreamData("[info] Streaming Result Box");

            // UI Elements content updates
            DiscoveringButton.Content = $"Discover DOTs";
            QueueDeviceButton.Content = $"Queue DOT";
            BatterySubscribeButton.Content = $"Check Battery ({xSensDotDevicesList.Count})";
            StreamingButton.Content = $"Start Streaming ({xSensDotDevicesList.Count})";
            ResetHeadingButton.Content = $"Reset Heading ({xSensDotDevicesList.Count})";
            SynchroniseButton.Content = $"Synchronise ({xSensDotDevicesList.Count})";

        }

        /// <summary>
        ///     Initiates the discovery of Xsens DOT IMUs in range.
        /// </summary>
        private void DiscoveringButton_Click(object sender, RoutedEventArgs e)
        {
            if (deviceWatcher == null)
            {
                StartBleDeviceWatcher();
                DiscoveringButton.Content = "Stop discovering";
                NotifyUser("[INFO] Device watcher started.");
            }
            else
            {
                StopBleDeviceWatcher();
                DiscoveringButton.Content = "Discover DOTs";
                NotifyUser("[INFO] Device watcher stopped.");
            }
        }

        private async void SynchroniseButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void QueueDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the device from the list view
            var tempBleDeviceDisplay = DeviceListBox.SelectedItem as BluetoothLEDeviceDisplay;

            if (tempBleDeviceDisplay == null)
            {
                return;
            }

            XsensDotDevice newDevice = new XsensDotDevice(tempBleDeviceDisplay.DeviceInformation, this);

            if (!xSensDotDevicesList.Contains(newDevice))
            {
                xSensDotDevicesList.Add(newDevice);
                quaternionsDictionary.TryAdd(newDevice,new Quaternion());

                NotifyUser($"The device {newDevice.UniqueDeviceName} has been queued up");
            }
            else
            {
                xSensDotDevicesList.Remove(newDevice);
                quaternionsDictionary.TryRemove(newDevice, out Quaternion outValue);

                NotifyUser($"The device {newDevice.UniqueDeviceName} has been removed");
            }

            // Update the buttons for feedback
            BatterySubscribeButton.Content = $"Check Battery ({xSensDotDevicesList.Count})";
            StreamingButton.Content = $"Start Streaming ({xSensDotDevicesList.Count})";
            SynchroniseButton.Content = $"Synchronise ({xSensDotDevicesList.Count})";
            ResetHeadingButton.Content = $"Reset Heading ({xSensDotDevicesList.Count})";
        }

        private async void Subscribe2BatteryButton_Click(object sender, RoutedEventArgs e)
        {
            // makes sure we have devices queued up to connect to and get battery levels, else what's the point man?
            if (xSensDotDevicesList.Count > 0)
            {
                foreach (var device in xSensDotDevicesList)
                {
                    await device.SubscribeToBattery();
                }
            }
            else
            {
                NotifyUser($"[err] No devices considered for battery subscriptions");
            }
        }

        private async void ResetHeadingButton_Click(object sender, RoutedEventArgs e)
        {
            NotifyUser("[err] Button Not Mapped");

        }

        private void SyncButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Subscribe2MeasurementButton_Click(object sender, RoutedEventArgs e)
        {

            // Makes sure we have devices queued up to connect to
            if (xSensDotDevicesList.Count > 0)
            {
                foreach (var device in xSensDotDevicesList)
                {
                    NotifyUser($"[info] Running .SubscribeToMeasurement on {device.UniqueDeviceName} from root page");
                    device.SubscribeToMeasurement(payloadType);

                    //device.StreamSplitThread();
                }
            }
            else
            {
                NotifyUser($"[err] No devices considered for streaming");
            }
                    
        }
        #endregion

        #region Device Discovery Methods

        /// <summary>
        /// Starts a device watcher that looks for all nearby Bluetooth devices (paired or unpaired). 
        /// Attaches event handlers to populate the device collection.
        /// </summary>
        private void StartBleDeviceWatcher()
        {
            // Additional properties we would like about the device.
            // Property strings are documented here https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };

            // BT_Code: Example showing paired and non-paired in a single query.
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

            deviceWatcher = DeviceInformation.CreateWatcher(aqsAllBluetoothLEDevices, requestedProperties, DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start over with an empty collection.
            KnownDevices.Clear();

            // Start the watcher. Active enumeration is limited to approximately 30 seconds.
            // This limits power usage and reduces interference with other Bluetooth activities.
            // To monitor for the presence of Bluetooth LE devices for an extended period,
            // use the BluetoothLEAdvertisementWatcher runtime class. See the BluetoothAdvertisement
            // sample for an example.
            deviceWatcher.Start();
        }

        /// <summary>
        /// Stops watching for all nearby Bluetooth devices.
        /// </summary>
        private void StopBleDeviceWatcher()
        {
            if (deviceWatcher != null)
            {
                // Unregister the event handlers.
                deviceWatcher.Added -= DeviceWatcher_Added;
                deviceWatcher.Updated -= DeviceWatcher_Updated;
                deviceWatcher.Removed -= DeviceWatcher_Removed;
                deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
                deviceWatcher.Stopped -= DeviceWatcher_Stopped;

                // Stop the watcher.
                deviceWatcher.Stop();
                deviceWatcher = null;
            }
        }

        private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in KnownDevices)
            {
                if (bleDeviceDisplay.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }

        private DeviceInformation FindUnknownDevices(string id)
        {
            foreach (DeviceInformation bleDeviceInfo in UnknownDevices)
            {
                if (bleDeviceInfo.Id == id)
                {
                    return bleDeviceInfo;
                }
            }
            return null;
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        // Make sure device isn't already present in the list.
                        if (FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                        {
                            if (deviceInfo.Name != string.Empty && deviceInfo.Name.Equals(Constants.targetDeviceName))
                            {
                                // If device has a friendly name display it immediately.
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                            }
                            else
                            {
                                // Add it to a list in case the name gets updated later. 
                                UnknownDevices.Add(deviceInfo);
                            }
                        }
                    }
                }
            });
        }

        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    //
                    //Debug.WriteLine(String.Format("Updated {0}{1}", deviceInfoUpdate.Id, ""));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            // Device is already being displayed - update UX.
                            bleDeviceDisplay.Update(deviceInfoUpdate);
                            return;
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            deviceInfo.Update(deviceInfoUpdate);
                            // If device has been updated with a friendly name it's no longer unknown.
                            if (deviceInfo.Name != String.Empty && deviceInfo.Name.Equals(Constants.targetDeviceName))
                            {
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                                UnknownDevices.Remove(deviceInfo);
                            }
                        }
                    }
                }
            });
        }

        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    //Debug.WriteLine(String.Format("Removed {0}{1}", deviceInfoUpdate.Id, ""));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        // Find the corresponding DeviceInformation in the collection and remove it.
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            KnownDevices.Remove(bleDeviceDisplay);
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            UnknownDevices.Remove(deviceInfo);
                        }
                    }
                }
            });
        }

        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    NotifyUser($"{KnownDevices.Count} devices found. Enumeration completed.");
                }
                DiscoveringButton.Content = $"Discover DOTs";
            });
        }

        private async void DeviceWatcher_Stopped(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    NotifyUser($"No longer watching for devices.");
                }
                DiscoveringButton.Content = $"Discover DOTs";
            });
        }

        #endregion

        private void WeWantEuler_Toggled(object sender, RoutedEventArgs e)
        {
            if (WeWantEulerToggle.IsOn)
            {
                NotifyUser("[info] Alright Euler then!");
                payloadType = PayloadType.CompleteEuler;
            }
            else
            {
                NotifyUser("[info] Yeah got it we want Quaternions");
                payloadType = PayloadType.CompleteQuaternion;
            }
        }
    }
}