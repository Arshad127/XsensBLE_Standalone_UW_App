using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace XsensBLE_Communication
{
    public class XsensDotDevice : IEquatable<XsensDotDevice>
    {
        #region Error Codes
        readonly int E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED = unchecked((int)0x80650003);
        readonly int E_BLUETOOTH_ATT_INVALID_PDU = unchecked((int)0x80650004);
        readonly int E_ACCESSDENIED = unchecked((int)0x80070005);
        readonly int E_DEVICE_NOT_AVAILABLE = unchecked((int)0x800710df); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)
        #endregion

        #region Class Variables and Constants
        private static readonly Guid BatteryCharacteristicUuid = Guid.Parse("15173001-4947-11e9-8646-d663bd873d93");
        private static readonly Guid BatteryServiceUuid = Guid.Parse("15173000-4947-11e9-8646-d663bd873d93");
        private static readonly Guid MeasurementServiceUuid = Guid.Parse("15172000-4947-11e9-8646-d663bd873d93");
        private static readonly Guid MeasurementCharacteristicUuid = Guid.Parse("15172003-4947-11e9-8646-d663bd873d93");
        private static readonly Guid ControlCharacteristicUuid = Guid.Parse("15172001-4947-11e9-8646-d663bd873d93");
        public static readonly string targetDeviceName = "Xsens DOT";

        private bool isBatterySubscribed = false;
        private bool isMeasurementSubscribed = false;
        private GattDeviceService batteryService = null;
        private GattCharacteristic batteryCharacteristic = null;
        private GattCharacteristic registeredBatteryCharacteristic = null;
        private GattDeviceService measurementService = null;
        private GattCharacteristic measurementCharacteristic = null;
        private GattCharacteristic registeredMeasurementCharacteristic = null;
        private GattCharacteristic controlCharacteristic = null;
        private GattPresentationFormat presentationFormat;
        private BluetoothLEDevice bluetoothLeDevice = null;

        private IReadOnlyList<GattDeviceService> services;
        private IReadOnlyList<GattCharacteristic> batteryCharacteristics;
        private IReadOnlyList<GattCharacteristic> measurementCharacteristics;



        private MainPage rootPage;


        #endregion



        public XsensDotDevice(DeviceInformation deviceInfoIn, MainPage rootPage)
        {
            if (!deviceInfoIn.Name.Equals(targetDeviceName))
            {
                throw new InvalidDataException("Only Xsens DOT BLE devices can be added.");
            }

            DeviceInformation = deviceInfoIn;
            this.UniqueDeviceName = "DOT " + deviceInfoIn.Id.Split("-")[1].ToUpper();
            this.rootPage = rootPage;
        }

        public string UniqueDeviceName { get; private set; }

        public DeviceInformation DeviceInformation { get; private set; }
        public string Id => DeviceInformation.Id;
        public string Name => DeviceInformation.Name;
        public bool IsPaired => DeviceInformation.Pairing.IsPaired;
        public bool IsConnected => (bool?)DeviceInformation.Properties["System.Devices.Aep.IsConnected"] == true;
        public bool IsConnectable => (bool?)DeviceInformation.Properties["System.Devices.Aep.Bluetooth.Le.IsConnectable"] == true;

        public string ToString()
        {
            return UniqueDeviceName;
        }

        public async Task PairToDevice()
        {
            rootPage.NotifyUser($"[info] Pairing to {UniqueDeviceName} started, please wait...");
            DevicePairingResult resultPair = await DeviceInformation.Pairing.PairAsync();
            rootPage.NotifyUser($"[info] Pairing result to {UniqueDeviceName} = {resultPair.Status}");
        }

        private async Task Connect()
        {
            if (!IsPaired)
            {
                rootPage.NotifyUser($"[err] Cannot Connect without being paired to {UniqueDeviceName} first.");
                return;
            }

            // Attempt connection now
            try
            {
                // BT_Code: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
                bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(Id);

                if (bluetoothLeDevice == null)
                {
                    rootPage.NotifyUser($"[err] Failed to connect to device {UniqueDeviceName}.");
                }
            }
            catch (Exception ex) when (ex.HResult == E_DEVICE_NOT_AVAILABLE)
            {
                rootPage.NotifyUser($"[err] Bluetooth radio is not on for {UniqueDeviceName}.");
            }
        }

        private async Task GetServices()
        {
            if (bluetoothLeDevice != null)
            {
                // Note: BluetoothLEDevice.GattServices property will return an empty list for unpaired devices. For all uses we recommend using the GetGattServicesAsync method.
                // BT_Code: GetGattServicesAsync returns a list of all the supported services of the device (even if it's not paired to the system).
                // If the services supported by the device are expected to change during BT usage, subscribe to the GattServicesChanged event.
                GattDeviceServicesResult serviceResult = await bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

                if (serviceResult.Status == GattCommunicationStatus.Success)
                {
                    services = serviceResult.Services;
                    rootPage.NotifyUser($"[info] Services successfully extracted from {UniqueDeviceName}.");
                }
                else
                {
                    rootPage.NotifyUser($"[err] Cannot extracted services. {UniqueDeviceName} is unreachable.");
                }
            }
        }


        private async Task<GattCharacteristic> GetSpecificCharacteristic(Guid serviceUuid, Guid characteristicUuid)
        {
            GattDeviceService targettedService = null;
            GattCharacteristic targettedCharacteristic = null;

            foreach (var service in services)
            {
                if (service.Uuid.Equals(serviceUuid))
                {
                    targettedService = service;
                    rootPage.NotifyUser($"[info] Service {serviceUuid.ToString()} of {UniqueDeviceName} found.");
                }
            }

            if (targettedService != null)
            {
                IReadOnlyList<GattCharacteristic> characteristics = null;
                try
                {
                    // Ensure we have access to the device.
                    var accessStatus = await targettedService.RequestAccessAsync();
                    if (accessStatus == DeviceAccessStatus.Allowed)
                    {
                        // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characteristics only 
                        // and the new Async functions to get the characteristics of unpaired devices as well. 
                        var charResult = await targettedService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                        if (charResult.Status == GattCommunicationStatus.Success)
                        {
                            characteristics = charResult.Characteristics;
                            foreach (var characteristic in characteristics)
                            {
                                if (characteristic.Uuid.Equals(characteristicUuid))
                                {
                                    targettedCharacteristic = characteristic;
                                    rootPage.NotifyUser($"[info] Characteristic {characteristicUuid.ToString()} of {UniqueDeviceName} found.");
                                    //batteryCharacteristic = characteristic; // set the global variable while we are here
                                }
                            }
                        }
                        else
                        {
                            rootPage.NotifyUser($"[err] Error accessing service in {UniqueDeviceName}.");

                            // On error, act as if there are no characteristics.
                            characteristics = new List<GattCharacteristic>();
                        }
                    }
                    else
                    {
                        // Not granted access
                        rootPage.NotifyUser($"[err] Error accessing service in {UniqueDeviceName}.");

                        // On error, act as if there are no characteristics.
                        characteristics = new List<GattCharacteristic>();
                    }
                }
                catch (Exception ex)
                {
                    rootPage.NotifyUser($"[err] Restricted service. Can't read characteristics of {UniqueDeviceName}: " + ex.Message);
                    // On error, act as if there are no characteristics.
                    characteristics = new List<GattCharacteristic>();
                }
            }
            
            return targettedCharacteristic;
        }


        private void GetBatteryService()
        {
            foreach (var service in services)
            {
                if (service.Uuid.Equals(BatteryServiceUuid))
                {
                    batteryService = service;
                    rootPage.NotifyUser($"[info] Battery service of {UniqueDeviceName} found.");
                }
            }

            if (batteryService == null)
            {
                rootPage.NotifyUser($"[err] Battery service of {UniqueDeviceName} not found.");
            }
        }

        public async Task SubscribeToMeasurement() // to subscribe and unsubscribe to the measurement service
        {
            if (!IsPaired) // ensure device is paired to
            {
                await PairToDevice();
            }

            if (!IsConnected) // ensure device is connected to 
            {
                await Connect();
            }

            if (measurementCharacteristic == null) // hunt for that battery service
            {
                await GetServices(); // gets all the services on the device
                measurementCharacteristic = await GetSpecificCharacteristic(MeasurementServiceUuid, MeasurementCharacteristicUuid);
            }

            // Doing the Subscribing
            RemoveValueChangedHandlerMeasurement(); // remove any prior handler of the battery

            if (!isMeasurementSubscribed)
            {
                // Initialise the status
                GattCommunicationStatus status = GattCommunicationStatus.Unreachable;

                try
                {
                    // BT_Code: Must write the CCCD in order for server to send indications.
                    // We receive them in the ValueChanged event handler.
                    //status = await batteryCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);
                    status = await measurementCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);

                    if (status == GattCommunicationStatus.Success)
                    {
                        AddValueChangedHandlerMeasurement();
                    }
                    else
                    {
                        rootPage.NotifyUser($"[err] Error registering for measurement changes: {status} for {UniqueDeviceName}.");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support indicate, but it actually doesn't.
                    rootPage.NotifyUser($"[excpt] for {UniqueDeviceName} -> {ex.Message}");
                }
            }
            else // here we are unsubscribing
            {
                try
                {
                    // BT_Code: Must write the CCCD in order for server to send notifications.
                    // We receive them in the ValueChanged event handler.
                    // Note that this sample configures either Indicate or Notify, but not both.
                    var result = await
                        measurementCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.None);
                    if (result == GattCommunicationStatus.Success)
                    {
                        isMeasurementSubscribed = false;
                        RemoveValueChangedHandlerMeasurement();
                        rootPage.NotifyUser($"[info] Successfully un-registered for notifications for {UniqueDeviceName}.");
                    }
                    else
                    {
                        rootPage.NotifyUser($"[err] Error un-registering for notifications: {result} for {UniqueDeviceName}.");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support notify, but it actually doesn't.
                    rootPage.NotifyUser($"[excpt] for {UniqueDeviceName} -> {ex.Message}");
                }
            }

            // Set payload and start streaming
            controlCharacteristic = await GetSpecificCharacteristic(MeasurementServiceUuid, ControlCharacteristicUuid);

            // Read the existing data byte array
            GattReadResult xSensControlData = await controlCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

            // Lets see what we get hey
            rootPage.NotifyUser(FormatValueByPresentation(xSensControlData.Value, CustomPresentationFormat.MeasurementDetails));

        }

        public async Task SubscribeToBattery() // to subscribe and unsubscribe to the battery service
        {
            if (!IsPaired) // ensure device is paired to
            {
                await PairToDevice();
            }

            if (!IsConnected) // ensure device is connected to 
            {
                await Connect();
            }

            if (batteryCharacteristic == null) // hunt for that battery service
            {
                await GetServices(); // gets all the services on the device
                batteryCharacteristic = await GetSpecificCharacteristic(BatteryServiceUuid, BatteryCharacteristicUuid);
            }

            // Doing the Subscribing
            RemoveValueChangedHandlerBattery(); // remove any prior handler of the battery


            if (!isBatterySubscribed)
            {
                // Initialise the status
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
                        AddValueChangedHandlerBattery();
                    }
                    else
                    {
                        rootPage.NotifyUser($"[err] Error registering for value changes: {status} for {UniqueDeviceName}.");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support indicate, but it actually doesn't.
                    rootPage.NotifyUser($"[excpt] for {UniqueDeviceName} -> {ex.Message}");
                }
            }
            else // here we are unsubscribing
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
                        isBatterySubscribed = false;
                        RemoveValueChangedHandlerBattery();
                        rootPage.NotifyUser($"[info] Successfully un-registered for notifications for {UniqueDeviceName}.");
                    }
                    else
                    {
                        rootPage.NotifyUser($"[err] Error un-registering for notifications: {result} for {UniqueDeviceName}.");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support notify, but it actually doesn't.
                    rootPage.NotifyUser($"[excpt] for {UniqueDeviceName} -> {ex.Message}");
                }
            }

            // Lets read the first value
            // BT_Code: Read the actual value from the device by using Uncached.
            GattReadResult gattReadTempResult1 = await batteryCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (gattReadTempResult1.Status == GattCommunicationStatus.Success)
            {

                rootPage.NotifyUser(FormatValueByPresentation(gattReadTempResult1.Value, CustomPresentationFormat.Battery));
            }
            else
            {
                rootPage.NotifyUser($"[err] Read failed in {UniqueDeviceName}: {gattReadTempResult1.Status}");
            }
        }



        #region Event Handlers

        private void AddValueChangedHandlerBattery()
        {
            if (isBatterySubscribed) return; // Guard

            registeredBatteryCharacteristic = batteryCharacteristic;
            registeredBatteryCharacteristic.ValueChanged += Characteristic_BatteryValueChanged;
            isBatterySubscribed = true;
            rootPage.NotifyUser($"[info] {UniqueDeviceName} Successfully subscribed for battery changes");
        }

        private void RemoveValueChangedHandlerBattery()
        {
            if (!isBatterySubscribed) return; // Guard

            registeredBatteryCharacteristic.ValueChanged -= Characteristic_BatteryValueChanged;
            registeredBatteryCharacteristic = null;
            isBatterySubscribed = false;
            rootPage.NotifyUser($"[info] {UniqueDeviceName} Successfully un subscribed for battery changes");
        }

        private void Characteristic_BatteryValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            rootPage.NotifyUser(FormatValueByPresentation(args.CharacteristicValue, CustomPresentationFormat.Battery));
        }

        private void AddValueChangedHandlerMeasurement()
        {
            if (isBatterySubscribed) return; // Guard

            registeredMeasurementCharacteristic = measurementCharacteristic;
            registeredMeasurementCharacteristic.ValueChanged += Characteristic_MeasurementValueChanged;
            isMeasurementSubscribed = true;
            rootPage.NotifyUser($"[info] {UniqueDeviceName} Successfully subscribed for measurement value changes");
        }

        private void RemoveValueChangedHandlerMeasurement()
        {
            if (!isBatterySubscribed) return; // Guard

            registeredMeasurementCharacteristic.ValueChanged -= Characteristic_MeasurementValueChanged;
            registeredMeasurementCharacteristic = null;
            isMeasurementSubscribed = false;
            rootPage.NotifyUser($"[info] {UniqueDeviceName} Successfully un subscribed for measurement changes");
        }

        private void Characteristic_MeasurementValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            rootPage.NotifyUser(FormatValueByPresentation(args.CharacteristicValue, CustomPresentationFormat.MeasurementEuler));
        }

        private string FormatValueByPresentation(IBuffer buffer, CustomPresentationFormat format)
        {
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);
            string outString = "";

            switch (format)
            {
                case CustomPresentationFormat.Battery:
                    outString = $"[Battery] [{UniqueDeviceName}]: {data[0].ToString()}%, State: {data[1].ToString()}";
                    break;

                case CustomPresentationFormat.MeasurementEuler:
                    outString = $"[EulerMeasurement] [{UniqueDeviceName}]: Not setup to present data yet";
                    break;

                case CustomPresentationFormat.MeasurementQuaternion:
                    outString = $"[QuaternionMeasurement] [{UniqueDeviceName}]: Not setup to present data yet";
                    break;
                case CustomPresentationFormat.MeasurementDetails:
                    outString = $"[MeasurementDetails] [{UniqueDeviceName}]: Type:{data[0]}, Action:{data[1]}, Payload:{data[2]}";
                    break;
            }

            return outString;
        }

        public bool Equals(XsensDotDevice other)
        {
            return other != null && other.Id.Equals(this.Id);
        }

        public enum CustomPresentationFormat
        {
            Battery,
            MeasurementQuaternion,
            MeasurementEuler,
            MeasurementDetails
        }


        #endregion
    }
}
