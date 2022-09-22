using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace XsensBLE_Communication
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Error Codes
        readonly int E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED = unchecked((int)0x80650003);
        readonly int E_BLUETOOTH_ATT_INVALID_PDU = unchecked((int)0x80650004);
        readonly int E_ACCESSDENIED = unchecked((int)0x80070005);
        readonly int E_DEVICE_NOT_AVAILABLE = unchecked((int)0x800710df); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)
        #endregion

        private ObservableCollection<BluetoothLEDeviceDisplay> KnownDevices = new ObservableCollection<BluetoothLEDeviceDisplay>();
        private ObservableCollection<BluetoothLEDeviceDisplay> ConnectedDevices = new ObservableCollection<BluetoothLEDeviceDisplay>();
        private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
        private string allMessages = "";
        private string allStreams = "";
        private int XsensDotDeviceCount = 0;

        private List<DeviceInformation> UnknownDevices = new List<DeviceInformation>();
        private DeviceWatcher deviceWatcher;

        public string SelectedBleDeviceId;

        public string SelectedBleDeviceName = "No device selected";

        #region UI related items

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

        public void StreamThis(string steamedData)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                allStreams = steamedData + "\n" + allStreams;
                MessageBox.Text = allMessages;
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    allStreams = steamedData + "\n" + allStreams;
                    MessageBox.Text = allMessages;
                });
            }
        }


        public MainPage()
        {
            this.InitializeComponent();
            NotifyUser("[INFO] Message Box");
            StreamBox.Text = "Streaming Result Box";
        }

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
                DiscoveringButton.Content = "Discover";
                NotifyUser("[INFO] Device watcher stopped.");
            }
        }

        private bool isBusy = false;


        public bool commsIsBusy = false;

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            // Do not allow a new Pair operation to start if an existing one is in progress.
            if (isBusy)
            {
                return;
            }

            isBusy = true;

            NotifyUser("[INFO] Pairing started. Please wait...");

            // For more information about device pairing, including examples of
            // customizing the pairing process, see the DeviceEnumerationAndPairing sample.

            // Capture the current selected item in case the user changes it while we are pairing.
            var bleDeviceDisplay = DeviceListBox.SelectedItem as BluetoothLEDeviceDisplay;

            // BT_Code: Pair the currently selected device.
            DevicePairingResult resultPair = await bleDeviceDisplay.DeviceInformation.Pairing.PairAsync();
            NotifyUser($"[INFO] Pairing result = {resultPair.Status}");

            if (resultPair.Status == DevicePairingResultStatus.Paired || resultPair.Status == DevicePairingResultStatus.AlreadyPaired)
            {
                NotifyUser($"[INFO] Pairing done, now connecting to device");

                // here we call the connection method
                await ConnectBLEDevice(bleDeviceDisplay.DeviceInformation);
            }
            
            NotifyUser($"[INFO] Connection Established");
            isBusy = false;
        }

        private async void ResetHeadingButton_Click(object sender, RoutedEventArgs e)
        {
            NotifyUser("[WARN] Button remapped to reading battery levels");
            await SubscribeToBatteryServiceAsync();
        }

        private void SyncButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void StreamingButton_Click(object sender, RoutedEventArgs e)
        {

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
            });
        }

        #endregion

        #region Device Connection Methods
        //private GattCharacteristic selectedCharacteristic;
        private GattCharacteristic registeredCharacteristic;
        private GattPresentationFormat presentationFormat;
        private BluetoothLEDevice bluetoothLeDevice = null;
        private bool subscribedForNotifications = false;
        private GattDeviceService batteryService = null;
        private GattCharacteristic batteryCharacteristic = null;

        private async Task<bool> ClearBluetoothLEDeviceAsync()
        {
            if (subscribedForNotifications)
            {
                // Need to clear the CCCD from the remote device so we stop receiving notifications
                var result = await registeredCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                if (result != GattCommunicationStatus.Success)
                {
                    return false;
                }
                else
                {
                    batteryCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                    subscribedForNotifications = false;
                }
            }
            bluetoothLeDevice?.Dispose();
            bluetoothLeDevice = null;
            return true;
        }

        private async Task ConnectBLEDevice(DeviceInformation inDeviceInformation)
        {

            if (!await ClearBluetoothLEDeviceAsync())
            {
                NotifyUser("[ERR] Unable to reset state, try again.");
                return;
            }

            try
            {
                // BT_Code: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
                bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(inDeviceInformation.Id);

                if (bluetoothLeDevice == null)
                {
                    NotifyUser($"[ERR] Failed to connect to device.");
                }
            }
            catch (Exception ex) when (ex.HResult == E_DEVICE_NOT_AVAILABLE)
            {
                NotifyUser("[ERR] Bluetooth radio is not on.");
            }

            if (bluetoothLeDevice != null)
            {
                // Note: BluetoothLEDevice.GattServices property will return an empty list for unpaired devices. For all uses we recommend using the GetGattServicesAsync method.
                // BT_Code: GetGattServicesAsync returns a list of all the supported services of the device (even if it's not paired to the system).
                // If the services supported by the device are expected to change during BT usage, subscribe to the GattServicesChanged event.
                GattDeviceServicesResult serviceResult = await bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

                if (serviceResult.Status == GattCommunicationStatus.Success)
                {
                    var services = serviceResult.Services;

                    foreach (var service in services)
                    {
                        Debug.WriteLine($"Service -> {service.Uuid.ToString()}");
                        if (service.Uuid.Equals(Constants.BatteryServiceUuid))
                        {
                            batteryService = service;
                            Debug.WriteLine("[INFO] The battery service was found");
                        }
                    }

                    NotifyUser(String.Format($"[INFO] Found {services.Count} services including {batteryService.Uuid}"));
                }
                else
                {
                    NotifyUser("[ERR] Device unreachable");
                }
            }
        }

        private async Task SubscribeToMeasurementServiceAsync()
        {
            // Finding the characteristic ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            RemoveValueChangedHandler();

            IReadOnlyList<GattCharacteristic> characteristics = null;
            try
            {
                // Ensure we have access to the device.
                var accessStatus = await batteryService.RequestAccessAsync();
                if (accessStatus == DeviceAccessStatus.Allowed)
                {
                    // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characteristics only 
                    // and the new Async functions to get the characteristics of unpaired devices as well. 
                    var charResult = await batteryService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (charResult.Status == GattCommunicationStatus.Success)
                    {
                        characteristics = charResult.Characteristics;
                        foreach (var characteristic in characteristics)
                        {
                            Debug.WriteLine($"Characteristics -> {characteristic.Uuid.ToString()}");

                            if (characteristic.Uuid.Equals(Constants.BatteryCharacteristicUuid))
                            {
                                batteryCharacteristic = characteristic;
                                NotifyUser("[INFO] the battery characteristics has been found");
                            }
                        }
                    }
                    else
                    {
                        NotifyUser("[ERR] Error accessing service.");

                        // On error, act as if there are no characteristics.
                        characteristics = new List<GattCharacteristic>();
                    }
                }
                else
                {
                    // Not granted access
                    NotifyUser("[ERR] Error accessing service.");

                    // On error, act as if there are no characteristics.
                    characteristics = new List<GattCharacteristic>();
                }
            }
            catch (Exception ex)
            {
                NotifyUser("[ERR] Restricted service. Can't read characteristics: " + ex.Message);

                // On error, act as if there are no characteristics.
                characteristics = new List<GattCharacteristic>();
            }

            // Subscribe here then ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            if (!subscribedForNotifications)
            {
                // initialize status
                GattCommunicationStatus status = GattCommunicationStatus.Unreachable;

                try
                {
                    // BT_Code: Must write the CCCD in order for server to send indications.
                    // We receive them in the ValueChanged event handler.
                    //status = await batteryCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);
                    status = await batteryCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);

                    if (status == GattCommunicationStatus.Success)
                    {
                        AddValueChangedHandler();
                    }
                    else
                    {
                        NotifyUser($"[ERR] Error registering for value changes: {status}");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support indicate, but it actually doesn't.
                    NotifyUser($"[EXCEPTION] {ex.Message}");
                }
            }
            else // we are already subscribed
            {
                try
                {
                    // BT_Code: Must write the CCCD in order for server to send notifications.
                    // We receive them in the ValueChanged event handler.
                    // Note that this sample configures either Indicate or Notify, but not both.
                    var result = await
                        batteryCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                GattClientCharacteristicConfigurationDescriptorValue.None);
                    if (result == GattCommunicationStatus.Success)
                    {
                        subscribedForNotifications = false;
                        RemoveValueChangedHandler();
                        NotifyUser("[INFO] Successfully un-registered for notifications");
                    }
                    else
                    {
                        NotifyUser($"[ERR] Error un-registering for notifications: {result}");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support notify, but it actually doesn't.
                    NotifyUser($"[EXCEPTION] {ex.Message}");
                }

            }

            // regardless, let's read
            // BT_Code: Read the actual value from the device by using Uncached.
            GattReadResult result2 = await batteryCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (result2.Status == GattCommunicationStatus.Success)
            {

                NotifyUser(FormatValueByPresentation(result2.Value, presentationFormat));
            }
            else
            {
                NotifyUser($"[ERR] Read failed: {result2.Status}");
            }

        }


        private async Task SubscribeToBatteryServiceAsync()
        {
            // Finding the characteristic ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            RemoveValueChangedHandler();

            IReadOnlyList<GattCharacteristic> characteristics = null;
            try
            {
                // Ensure we have access to the device.
                var accessStatus = await batteryService.RequestAccessAsync();
                if (accessStatus == DeviceAccessStatus.Allowed)
                {
                    // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characteristics only 
                    // and the new Async functions to get the characteristics of unpaired devices as well. 
                    var charResult = await batteryService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (charResult.Status == GattCommunicationStatus.Success)
                    {
                        characteristics = charResult.Characteristics;
                        foreach (var characteristic in characteristics)
                        {
                            Debug.WriteLine($"Characteristics -> {characteristic.Uuid.ToString()}");

                            if (characteristic.Uuid.Equals(Constants.BatteryCharacteristicUuid))
                            {
                                batteryCharacteristic = characteristic;
                                NotifyUser("[INFO] the battery characteristics has been found");
                            }
                        }
                    }
                    else
                    {
                        NotifyUser("[ERR] Error accessing service.");

                        // On error, act as if there are no characteristics.
                        characteristics = new List<GattCharacteristic>();
                    }
                }
                else
                {
                    // Not granted access
                    NotifyUser("[ERR] Error accessing service.");

                    // On error, act as if there are no characteristics.
                    characteristics = new List<GattCharacteristic>();

                }
            }
            catch (Exception ex)
            {
                NotifyUser("[ERR] Restricted service. Can't read characteristics: " + ex.Message);
                // On error, act as if there are no characteristics.
                characteristics = new List<GattCharacteristic>();
            }

            // Subscribe here then ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            if (!subscribedForNotifications)
            {
                // initialize status
                GattCommunicationStatus status = GattCommunicationStatus.Unreachable;

                try
                {
                    // BT_Code: Must write the CCCD in order for server to send indications.
                    // We receive them in the ValueChanged event handler.
                    //status = await batteryCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);
                    status = await batteryCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);

                    if (status == GattCommunicationStatus.Success)
                    {
                        AddValueChangedHandler();
                    }
                    else
                    {
                        NotifyUser($"[ERR] Error registering for value changes: {status}");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support indicate, but it actually doesn't.
                    NotifyUser($"[EXCEPTION] {ex.Message}");
                }
            }
            else // we are already subscribed
            {
                try
                {
                    // BT_Code: Must write the CCCD in order for server to send notifications.
                    // We receive them in the ValueChanged event handler.
                    // Note that this sample configures either Indicate or Notify, but not both.
                    var result = await
                        batteryCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                GattClientCharacteristicConfigurationDescriptorValue.None);
                    if (result == GattCommunicationStatus.Success)
                    {
                        subscribedForNotifications = false;
                        RemoveValueChangedHandler();
                        NotifyUser("[INFO] Successfully un-registered for notifications");
                    }
                    else
                    {
                        NotifyUser($"[ERR] Error un-registering for notifications: {result}");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support notify, but it actually doesn't.
                    NotifyUser($"[EXCEPTION] {ex.Message}");
                }

            }

            // regardless, let's read
            // BT_Code: Read the actual value from the device by using Uncached.
            GattReadResult result2 = await batteryCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (result2.Status == GattCommunicationStatus.Success)
            {

                NotifyUser(FormatValueByPresentation(result2.Value, presentationFormat));
            }
            else
            {
                NotifyUser($"[ERR] Read failed: {result2.Status}");
            }
        }

        private void AddValueChangedHandler()
        {
            if (!subscribedForNotifications)
            {
                registeredCharacteristic = batteryCharacteristic;
                registeredCharacteristic.ValueChanged += Characteristic_ValueChanged;
                subscribedForNotifications = true;
                NotifyUser("[INFO] Successfully subscribed for value changes");
            }
        }

        private void RemoveValueChangedHandler()
        {
            if (subscribedForNotifications)
            {
                registeredCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                registeredCharacteristic = null;
                subscribedForNotifications = false;
                NotifyUser("[INFO] Successfully un subscribed for value changes");
            }
        }

        private async void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            NotifyUser("[INFO] Characteristic Value was changed");

            NotifyUser(FormatValueByPresentation(args.CharacteristicValue, presentationFormat));
            /*
            byte[] data;
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out data);

            NotifyUser($"[BATTERY] Level: {data[0].ToString()}%, State: {data[1].ToString()}");
            */

            // BT_Code: An Indicate or Notify reported that the value has changed.
            // Display the new value with a timestamp.
            //var newValue = FormatValueByPresentation(args.CharacteristicValue, presentationFormat);
            //var message = $"Value at {DateTime.Now:hh:mm:ss.FFF}: {newValue}";
            //await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => CharacteristicLatestValue.Text = message);

        }

        private string FormatValueByPresentation(IBuffer buffer, GattPresentationFormat format)
        {
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);

            return $"[BATTERY] Level: {data[0].ToString()}%, State: {data[1].ToString()}";
        }

        #endregion


    }
}