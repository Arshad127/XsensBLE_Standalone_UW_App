using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using XsensDOT_StandardLibrary;

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
        private bool isBatterySubscribed = false;
        private bool isMeasurementSubscribed = false;
        private GattDeviceService batteryService = null;
        private GattCharacteristic batteryCharacteristic = null;
        private GattCharacteristic registeredBatteryCharacteristic = null;
        private GattCharacteristic mediumPayLoadCharacteristic = null;
        private GattCharacteristic registeredMeasurementCharacteristic = null;
        private GattCharacteristic controlCharacteristic = null;

        private Thread streamingThread = null;
        private bool isStreaming = false;
        private PayloadType payloadType = PayloadType.CompleteEuler;

        private BluetoothLEDevice bluetoothLeDevice = null;

        private IReadOnlyList<GattDeviceService> services;

        private Quaternion myQuaternion = new Quaternion();

        // Reference to the main page
        private readonly MainPage _rootPage;
        #endregion


        #region TYPICAL CLASS METHODS for XsensDotDevice

        /// <summary>
        /// Constructor for the XsensDotDevice class. 
        /// </summary>
        public XsensDotDevice(DeviceInformation deviceInfoIn, MainPage rootPage)
        {
            if (!deviceInfoIn.Name.Equals(LibConstants.TargetDeviceName))
            {
                throw new InvalidDataException("Only Xsens DOT BLE devices can be added.");
            }

            DeviceInformation = deviceInfoIn;
            this.UniqueDeviceName = "DOT " + deviceInfoIn.Id.Split("-")[1].ToUpper();
            this._rootPage = rootPage;
        }

        /// <summary>
        /// Short, Unique for the devices. More readable than the default BLE address.
        /// </summary>
        public string UniqueDeviceName { get; private set; }

        public DeviceInformation DeviceInformation { get; private set; }
        public string Id => DeviceInformation.Id;
        public string Name => DeviceInformation.Name;
        public bool IsPaired => DeviceInformation.Pairing.IsPaired;
        public bool IsConnected => (bool?)DeviceInformation.Properties["System.Devices.Aep.IsConnected"] == true;
        public bool IsConnectable => (bool?)DeviceInformation.Properties["System.Devices.Aep.Bluetooth.Le.IsConnectable"] == true;

        public override string ToString()
        {
            return UniqueDeviceName;
        }

        /// <summary>
        /// Implementation of the IEquatable so the xSensDOTDevice objects are comparable. 
        /// </summary>
        public bool Equals(XsensDotDevice other)
        {
            return other != null && other.Id.Equals(this.Id);
        }

        #endregion


        #region CONNECTION & PAIRING

        public async Task PairToDevice()
        {
            _rootPage.NotifyUser($"[info] Pairing to {UniqueDeviceName} started, please wait...");
            DevicePairingResult resultPair = await DeviceInformation.Pairing.PairAsync();
            _rootPage.NotifyUser($"[info] Pairing result to {UniqueDeviceName} = {resultPair.Status}");
        }

        private async Task Connect()
        {
            if (!IsPaired)
            {
                _rootPage.NotifyUser($"[err] Cannot connect without being paired to {UniqueDeviceName} first.");
                return;
            }

            // Attempt connection now
            try
            {
                // BT_Code: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
                bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(Id);

                if (bluetoothLeDevice == null)
                {
                    _rootPage.NotifyUser($"[err] Failed to connect to device {UniqueDeviceName}.");
                }
            }
            catch (Exception ex) when (ex.HResult == E_DEVICE_NOT_AVAILABLE)
            {
                _rootPage.NotifyUser($"[err] Bluetooth radio is not on for {UniqueDeviceName}.");
            }
        }

        #endregion


        /// <summary>
        /// Module to fetch a specific service from the device 
        /// </summary>
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
                    _rootPage.NotifyUser($"[info] Services successfully extracted from {UniqueDeviceName}.");
                }
                else
                {
                    _rootPage.NotifyUser($"[err] Cannot extracted services. {UniqueDeviceName} is unreachable.");
                }
            }
        }

        /// <summary>
        /// Module to fetch a specific characteristic from the device 
        /// </summary>
        private async Task<GattCharacteristic> GetSpecificCharacteristic(Guid serviceUuid, Guid characteristicUuid)
        {
            GattDeviceService targetedService = null;
            GattCharacteristic targetedCharacteristic = null;

            // Confirmation that we have the services listed and happy. Else the following
            // FOR loop will raise an exception and we wouldn't want that now would we?
            while (services == null)
            {
                await GetServices();
                Thread.Sleep(100);
            }

            foreach (var service in services)
            {
                if (service.Uuid.Equals(serviceUuid))
                {
                    targetedService = service;
                    _rootPage.NotifyUser($"[info] Service {serviceUuid.ToString()} of {UniqueDeviceName} found.");
                }
            }

            if (targetedService != null)
            {
                IReadOnlyList<GattCharacteristic> characteristics = null;
                try
                {
                    // Ensure we have access to the device.
                    var accessStatus = await targetedService.RequestAccessAsync();
                    if (accessStatus == DeviceAccessStatus.Allowed)
                    {
                        // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characteristics only 
                        // and the new Async functions to get the characteristics of unpaired devices as well. 
                        var charResult = await targetedService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                        if (charResult.Status == GattCommunicationStatus.Success)
                        {
                            characteristics = charResult.Characteristics;
                            foreach (var characteristic in characteristics)
                            {
                                if (characteristic.Uuid.Equals(characteristicUuid))
                                {
                                    targetedCharacteristic = characteristic;
                                    _rootPage.NotifyUser($"[info] Characteristic {characteristicUuid.ToString()} of {UniqueDeviceName} found.");
                                    //batteryCharacteristic = characteristic; // set the global variable while we are here
                                }
                            }
                        }
                        else
                        {
                            _rootPage.NotifyUser($"[err] Error accessing service in {UniqueDeviceName}.");

                            // On error, act as if there are no characteristics.
                            characteristics = new List<GattCharacteristic>();
                        }
                    }
                    else
                    {
                        // Not granted access
                        _rootPage.NotifyUser($"[err] Error accessing service in {UniqueDeviceName}.");

                        // On error, act as if there are no characteristics.
                        characteristics = new List<GattCharacteristic>();
                    }
                }
                catch (Exception ex)
                {
                    _rootPage.NotifyUser($"[err] Restricted service. Can't read characteristics of {UniqueDeviceName}: " + ex.Message);
                    // On error, act as if there are no characteristics.
                    characteristics = new List<GattCharacteristic>();
                }
            }
            
            return targetedCharacteristic;
        }


        #region TEST -> Run the stream in another thread
        public void StreamSplitThread()
        {
            if (isStreaming) return;

            streamingThread = new Thread(() => SubscribeToMeasurement(PayloadType.CompleteEuler));
            streamingThread.Start();
            isStreaming = true;
            _rootPage.NotifyUser($"[info] [{UniqueDeviceName}] NEW THREAD STARTED AND STREAMING");
        }

        public void StopStreaming()
        {
            if (!isStreaming) return;

            if (streamingThread != null)
            {
                streamingThread.Abort();
                isStreaming = false;
            }
        }

        #endregion


        #region BATTERY REGION
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
                batteryCharacteristic = await GetSpecificCharacteristic(LibConstants.BatteryServiceUuid, LibConstants.BatteryCharacteristicUuid);
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
                        _rootPage.NotifyUser($"[err] Error registering for value changes: {status} for {UniqueDeviceName}.");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support indicate, but it actually doesn't.
                    _rootPage.NotifyUser($"[excpt] for {UniqueDeviceName} -> {ex.Message}");
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
                        _rootPage.NotifyUser($"[info] Successfully un-registered for notifications for {UniqueDeviceName}.");
                    }
                    else
                    {
                        _rootPage.NotifyUser($"[err] Error un-registering for notifications: {result} for {UniqueDeviceName}.");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support notify, but it actually doesn't.
                    _rootPage.NotifyUser($"[excpt] for {UniqueDeviceName} -> {ex.Message}");
                }
            }

            // Lets read the first value
            // BT_Code: Read the actual value from the device by using Uncached.
            GattReadResult gattReadTempResult1 = await batteryCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (gattReadTempResult1.Status == GattCommunicationStatus.Success)
            {
                _rootPage.NotifyUser(FormatValueByPresentation(gattReadTempResult1.Value, PayloadType.BatteryDetails));
            }
            else
            {
                _rootPage.NotifyUser($"[err] Read failed in {UniqueDeviceName}: {gattReadTempResult1.Status}");
            }
        }

        private void AddValueChangedHandlerBattery()
        {
            if (isBatterySubscribed) return; // Guard

            registeredBatteryCharacteristic = batteryCharacteristic;
            registeredBatteryCharacteristic.ValueChanged += Characteristic_BatteryValueChanged;
            isBatterySubscribed = true;
            _rootPage.NotifyUser($"[info] {UniqueDeviceName} Successfully subscribed for battery changes");
        }

        private void RemoveValueChangedHandlerBattery()
        {
            if (!isBatterySubscribed) return; // Guard

            registeredBatteryCharacteristic.ValueChanged -= Characteristic_BatteryValueChanged;
            registeredBatteryCharacteristic = null;
            isBatterySubscribed = false;
            _rootPage.NotifyUser($"[info] {UniqueDeviceName} Successfully un subscribed for battery changes");
        }

        private void Characteristic_BatteryValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            _rootPage.NotifyUser(FormatValueByPresentation(args.CharacteristicValue, PayloadType.BatteryDetails));
        }
        #endregion


        #region MEASUREMENT REGION (STREAMING)

        public async Task SubscribeToMeasurement(PayloadType payload) // to subscribe and unsubscribe to the measurement service
        {
            this.payloadType = payload; // now set as the global variable

            if (!IsPaired) // ensure device is paired to
            {
                await PairToDevice();
            }

            if (!IsConnected) // ensure device is connected to 
            {
                await Connect();
            }

            if (mediumPayLoadCharacteristic == null) // hunt for that payload characteristic required to scream data back to us
            {
                await GetServices(); // gets all the services on the device
                mediumPayLoadCharacteristic = await GetSpecificCharacteristic(LibConstants.MeasurementServiceUuid, LibConstants.MediumPayloadCharacteristicUuid);
            }

            if (controlCharacteristic == null) // get the control characteristics
            {
                await GetServices(); // gets all the services on the device
                controlCharacteristic = await GetSpecificCharacteristic(LibConstants.MeasurementServiceUuid, LibConstants.ControlCharacteristicUuid);
            }

            // Doing the Subscribing
            //RemoveValueChangedHandlerMeasurement(); // remove any prior handler of the battery

            // if the device is not already subscribed
            if (!isMeasurementSubscribed)
            {
                // Initialise the status
                GattCommunicationStatus status = GattCommunicationStatus.Unreachable;

                try
                {
                    // BT_Code: Must write the CCCD in order for server to send indications.
                    // We receive them in the ValueChanged event handler.
                    //status = await batteryCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);
                    status = await mediumPayLoadCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);

                    if (status == GattCommunicationStatus.Success)
                    {
                        AddValueChangedHandlerMeasurement(); // will set isMeasurementSubscribed flag to true as well
                        _rootPage.NotifyUser($"[info] Measurement Notification for {UniqueDeviceName} is on.");
                    }
                    else
                    {
                        _rootPage.NotifyUser($"[err] Error registering for measurement changes: {status} for {UniqueDeviceName}.");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support indicate, but it actually doesn't.
                    _rootPage.NotifyUser($"[excpt] for {UniqueDeviceName} -> {ex.Message}");
                }

                // Writing the the device to start subscription and screaming
                // Set payload and start streaming
                // Read the existing data byte array
                GattReadResult xSensControlData = await controlCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

                // Lets see what we get eh
                _rootPage.NotifyUser("[BEFORE]" + FormatValueByPresentation(xSensControlData.Value, PayloadType.MeasurementGeneralDetails));

                // Get the byte array and edit
                byte[] controlDataArray = GetByteArray(xSensControlData.Value);
                controlDataArray[1] = 1; // Start the measurement
                controlDataArray[2] = (byte)(int)payloadType; // as per requested when calling the parent method

                // Write the information to the device
                await controlCharacteristic.WriteValueAsync(controlDataArray.AsBuffer());
                isStreaming = true;

                // Read again and see
                xSensControlData = await controlCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                _rootPage.NotifyUser("[AFTER]" + FormatValueByPresentation(xSensControlData.Value, PayloadType.MeasurementGeneralDetails));
            }
            else
            {
                _rootPage.NotifyUser($"[err] {UniqueDeviceName} is already subscribed to Measurement Service");
            }


        }

        public async Task UnSubscribeToMeasurement(PayloadType payload) // to subscribe and unsubscribe to the measurement service
        {
            this.payloadType = payload; // now set as the global variable

            if (!IsPaired) // ensure device is paired to
            {
                await PairToDevice();
            }

            if (!IsConnected) // ensure device is connected to 
            {
                await Connect();
            }

            if (mediumPayLoadCharacteristic == null) // hunt for that payload characteristic required to scream data back to us
            {
                await GetServices(); // gets all the services on the device
                mediumPayLoadCharacteristic = await GetSpecificCharacteristic(LibConstants.MeasurementServiceUuid, LibConstants.MediumPayloadCharacteristicUuid);
            }

            if (controlCharacteristic == null) // get the control characteristics
            {
                await GetServices(); // gets all the services on the device
                controlCharacteristic = await GetSpecificCharacteristic(LibConstants.MeasurementServiceUuid, LibConstants.ControlCharacteristicUuid);
            }

            // if the device is not already subscribed
            if (isMeasurementSubscribed)
            {
                // Set payload and start streaming
                // Read the existing data byte array
                GattReadResult xSensControlData = await controlCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

                // Get the byte array and edit
                byte[] controlDataArray = GetByteArray(xSensControlData.Value);
                controlDataArray[1] = 0; // Stop the measurement
                controlDataArray[2] = (byte)(int)payloadType; // as per requested when calling the parent method

                // Write the information to the device
                await controlCharacteristic.WriteValueAsync(controlDataArray.AsBuffer());
                isStreaming = false;

                try
                {
                    // BT_Code: Must write the CCCD in order for server to send notifications.
                    // We receive them in the ValueChanged event handler.
                    // Note that this sample configures either Indicate or Notify, but not both.
                    var result = await
                        mediumPayLoadCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.None);
                    if (result == GattCommunicationStatus.Success)
                    {
                        isMeasurementSubscribed = false;
                        RemoveValueChangedHandlerMeasurement();
                        _rootPage.NotifyUser($"[info] Successfully un-registered for notifications for {UniqueDeviceName}.");
                    }
                    else
                    {
                        _rootPage.NotifyUser($"[err] Error un-registering for notifications: {result} for {UniqueDeviceName}.");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support notify, but it actually doesn't.
                    _rootPage.NotifyUser($"[excpt] for {UniqueDeviceName} -> {ex.Message}");
                }
            }
            else // here we are unsubscribing since we were already subscribed
            {
                _rootPage.NotifyUser($"[err] for {UniqueDeviceName} Device is not subscribed to the Measurement Service");
            }
        }

        private void AddValueChangedHandlerMeasurement()
        {
            if (isMeasurementSubscribed) return; // Guard

            registeredMeasurementCharacteristic = mediumPayLoadCharacteristic;
            registeredMeasurementCharacteristic.ValueChanged += Characteristic_MeasurementValueChanged;
            isMeasurementSubscribed = true;
            _rootPage.NotifyUser($"[info] {UniqueDeviceName} Successfully subscribed for measurement value changes");
        }

        private void RemoveValueChangedHandlerMeasurement()
        {
            if (!isMeasurementSubscribed) return; // Guard

            registeredMeasurementCharacteristic.ValueChanged -= Characteristic_MeasurementValueChanged;
            registeredMeasurementCharacteristic = null;
            isMeasurementSubscribed = false;
            _rootPage.NotifyUser($"[info] {UniqueDeviceName} Successfully un-subscribed for measurement changes");
        }

        private void Characteristic_MeasurementValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            //Debug.WriteLine("Measurement Event Called");
            _rootPage.StreamData(FormatValueByPresentation(args.CharacteristicValue, payloadType));
        }

        #endregion


        #region HEADING RESET & REVERT REGION

        /// <summary>
        /// Method to asynchronously Reset the heading of the IMU
        /// </summary>
        public async Task ResetHeading()
        {
            if (isStreaming)
            {
                // fetch the characteristic
                GattCharacteristic resetHeadingControl = await GetSpecificCharacteristic(LibConstants.MeasurementServiceUuid, LibConstants.OrientationResetControlCharacteristicUuid);

                // read the value obtained
                GattReadResult resetHeadingControlData = await resetHeadingControl.ReadValueAsync(BluetoothCacheMode.Uncached);

                // push to the UI
                _rootPage.NotifyUser($"[info] [BEFORE]" + FormatValueByPresentation(resetHeadingControlData.Value, PayloadType.OrientationResetControlData));

                // Actually read the data and see what needs doing
                byte[] headingControlDataArray = GetByteArray(resetHeadingControlData.Value);

                do
                {
                    switch (headingControlDataArray[0])
                    {
                        case (int)HeadingResults.DefaultStatus:
                            // the sensor is set on default and we are simply going to reset it to 1
                            headingControlDataArray[0] = (int)HeadingResults.RevertHeadingToDefault; // setting the value to be 7
                            await resetHeadingControl.WriteValueAsync(headingControlDataArray.AsBuffer()); // write back
                            break;

                        case (int)HeadingResults.RevertHeadingToDefault:
                            // the sensor is being reverted to default & we are simply setting it to 1
                            headingControlDataArray[0] = (int)HeadingResults.ResetHeading; // setting the value to be 1
                            await resetHeadingControl.WriteValueAsync(headingControlDataArray.AsBuffer()); // write back
                            break;

                        case (int)HeadingResults.ResetHeading:
                            // the sensor is already reset, we have to set to default and re-reset
                            headingControlDataArray[0] = (int)HeadingResults.RevertHeadingToDefault; // setting the value to be 7
                            await resetHeadingControl.WriteValueAsync(headingControlDataArray.AsBuffer()); // write back

                            headingControlDataArray[0] = (int)HeadingResults.ResetHeading; // setting the value to be 1
                            await resetHeadingControl.WriteValueAsync(headingControlDataArray.AsBuffer()); // write back
                            break;
                    }

                    // Read again and see
                    resetHeadingControlData = await resetHeadingControl.ReadValueAsync(BluetoothCacheMode.Uncached);
                    headingControlDataArray = GetByteArray(resetHeadingControlData.Value);

                } while (headingControlDataArray[0] != (int)HeadingResults.ResetHeading); // Loop until the reset is completed

                _rootPage.NotifyUser($"[info] [AFTER]" + FormatValueByPresentation(resetHeadingControlData.Value, PayloadType.OrientationResetControlData));
            }
            else
            {
                _rootPage.NotifyUser($"[err] {UniqueDeviceName} can only perform the Heading Reset during steaming");
            }
        }
        #endregion


        #region OUTPUT PROCESSING REGION

        // Quick Access Variable
        private byte[] rawData, timeStampSubArr, eulerSubArrX, eulerSubArrY, eulerSubArrZ, freeAccSubArrX, freeAccSubArrY, freeAccSubArrZ;
        private byte[] quatSubArrW, quatSubArrX, quatSubArrY, quatSubArrZ;

        private string timeStamp, eulerX, eulerY, eulerZ, freeAccX, freeAccY, freeAccZ;
        private string quatW, quatX, quatY, quatZ;

        private string FormatValueByPresentation(IBuffer buffer, PayloadType payloadType)
        {
            CryptographicBuffer.CopyToByteArray(buffer, out rawData);
            string outString = "";

            switch (payloadType)
            {
                case PayloadType.BatteryDetails:

                    switch (rawData[1])
                    {
                        case 0:
                            // Not plugged in
                            outString = $"[Battery] [{UniqueDeviceName}]: {rawData[0]}% [NOT PLUGGED IN]";
                            break;
                        case 1:
                            // Plugged in
                            outString = $"[Battery] [{UniqueDeviceName}]: {rawData[0]}% [PLUGGED IN]";
                            break;
                    }
                    break;

                case PayloadType.CompleteEuler:
                    if (rawData.Length >= 28)
                    {
                        timeStampSubArr = GetSubArray(rawData, 0, 4);
                        eulerSubArrX = GetSubArray(rawData, 4, 4);
                        eulerSubArrY = GetSubArray(rawData, 8, 4);
                        eulerSubArrZ = GetSubArray(rawData, 12, 4);
                        freeAccSubArrX = GetSubArray(rawData, 16, 4);
                        freeAccSubArrY = GetSubArray(rawData, 20, 4);
                        freeAccSubArrZ = GetSubArray(rawData, 24, 4);

                        timeStamp = BitConverter.ToUInt32(timeStampSubArr, 0).ToString();
                        eulerX = BitConverter.ToSingle(eulerSubArrX, 0).ToString();
                        eulerY = BitConverter.ToSingle(eulerSubArrY, 0).ToString();
                        eulerZ = BitConverter.ToSingle(eulerSubArrZ, 0).ToString();
                        freeAccX = BitConverter.ToSingle(freeAccSubArrX, 0).ToString();
                        freeAccY = BitConverter.ToSingle(freeAccSubArrY, 0).ToString();
                        freeAccZ = BitConverter.ToSingle(freeAccSubArrZ, 0).ToString();

                        outString = $"[{UniqueDeviceName}] Time: {timeStamp}, X: {eulerX}, Y: {eulerY}, Z: {eulerZ}, AccX: {freeAccX}, AccY: {freeAccY}, AccZ: {freeAccZ}";
                    }
                    else
                    {
                        outString = "Insufficient Data";
                    }
                    break;

                case PayloadType.CompleteQuaternion:
                    if (rawData.Length >= 32)
                    {
                        timeStampSubArr = GetSubArray(rawData, 0, 4);
                        quatSubArrW = GetSubArray(rawData, 4, 4);
                        quatSubArrX = GetSubArray(rawData, 8, 4);
                        quatSubArrY = GetSubArray(rawData, 12, 4);
                        quatSubArrZ = GetSubArray(rawData, 16, 4);
                        freeAccSubArrX = GetSubArray(rawData, 20, 4);
                        freeAccSubArrY = GetSubArray(rawData, 24, 4);
                        freeAccSubArrZ = GetSubArray(rawData, 28, 4);

                        timeStamp = BitConverter.ToUInt32(timeStampSubArr, 0).ToString();
                        quatW = BitConverter.ToSingle(quatSubArrW, 0).ToString();
                        quatX = BitConverter.ToSingle(quatSubArrX, 0).ToString();
                        quatY = BitConverter.ToSingle(quatSubArrY, 0).ToString();
                        quatZ = BitConverter.ToSingle(quatSubArrZ, 0).ToString();
                        freeAccX = BitConverter.ToSingle(freeAccSubArrX, 0).ToString();
                        freeAccY = BitConverter.ToSingle(freeAccSubArrY, 0).ToString();
                        freeAccZ = BitConverter.ToSingle(freeAccSubArrZ, 0).ToString();

                        outString = $"[{UniqueDeviceName}] Time: {timeStamp}, W: {quatW}, X: {quatX}, Y: {quatY}, Z: {quatZ}, AccX: {freeAccX}, AccY: {freeAccY}, AccZ: {freeAccZ}";

                        // Fill up the myQuaternion details
                        myQuaternion.W = float.Parse(quatW, CultureInfo.InvariantCulture.NumberFormat);
                        myQuaternion.X = float.Parse(quatX, CultureInfo.InvariantCulture.NumberFormat);
                        myQuaternion.Y = float.Parse(quatY, CultureInfo.InvariantCulture.NumberFormat);
                        myQuaternion.Z = float.Parse(quatZ, CultureInfo.InvariantCulture.NumberFormat);

                        // send it and hope
                        _rootPage.UpdateQuaternionsRegistry(this, myQuaternion);
                    }
                    else
                    {
                        outString = "Insufficient Data";
                    }
                    break;
                case PayloadType.MeasurementGeneralDetails:
                    outString = $"[MeasurementDetails] [{UniqueDeviceName}]: Type:{rawData[0]}, Action:{rawData[1]}, Payload:{rawData[2]}";
                    break;

                case PayloadType.OrientationResetStatus:
                    outString = $"[OrientationResetStatus (1:Success, 2:Fail)] [{UniqueDeviceName}]: Result:{rawData[0]}";
                    break;

                case PayloadType.OrientationResetControlData:
                    outString = $"[OrientationResetControlData] [{UniqueDeviceName}]: {rawData[0]}, {rawData[1]}";
                    break;
            }

            return outString;
        }


        /// <summary>
        /// Method to extract the byte[] from the buffer for parsing
        /// </summary>
        private byte[] GetByteArray(IBuffer buffer)
        {
            CryptographicBuffer.CopyToByteArray(buffer, out var data);
            return data;
        }

        /// <summary>
        /// Method to extract subarray information from parent array
        /// </summary>
        private static T[] GetSubArray<T>(T[] array, int startIdx, int length)
        {
            T[] subArray = new T[length];

            Array.Copy(array, startIdx, subArray, 0, length);

            return subArray;
        }

        #endregion


    }
}
