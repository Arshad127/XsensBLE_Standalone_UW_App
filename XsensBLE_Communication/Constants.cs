using System;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace XsensBLE_Communication
{
    // Define the characteristics and other properties of our custom service.
    public class Constants
    {
        public static readonly string targetDeviceName = "Xsens DOT";

        // BT_Code: Initializes custom local parameters w/ properties, protection levels as well as common descriptors like User Description. 
        public static readonly GattLocalCharacteristicParameters gattOperandParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Write |
                                       GattCharacteristicProperties.WriteWithoutResponse,
            WriteProtectionLevel = GattProtectionLevel.Plain,
            UserDescription = "Operand Characteristic"
        };

        public static readonly GattLocalCharacteristicParameters gattOperatorParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Write |
                                       GattCharacteristicProperties.WriteWithoutResponse,
            WriteProtectionLevel = GattProtectionLevel.Plain,
            UserDescription = "Operator Characteristic"
        };

        public static readonly GattLocalCharacteristicParameters gattResultParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read |
                                       GattCharacteristicProperties.Notify,
            WriteProtectionLevel = GattProtectionLevel.Plain,
            UserDescription = "Result Characteristic"
        };

        public static readonly Guid CalcServiceUuid = Guid.Parse("caecface-e1d9-11e6-bf01-fe55135034f0");

        public static readonly Guid Op1CharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f1");
        public static readonly Guid Op2CharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f2");
        public static readonly Guid OperatorCharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f3");
        public static readonly Guid ResultCharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f4");

        public static readonly Guid BatteryCharacteristicUuid = Guid.Parse("15173001-4947-11e9-8646-d663bd873d93");
        public static readonly Guid BatteryServiceUuid = Guid.Parse("15173000-4947-11e9-8646-d663bd873d93");

        public static readonly Guid MeasurementServiceUuid = Guid.Parse("15172000-4947-11e9-8646-d663bd873d93");
        public static readonly Guid MeasurementCharacteristicUuid = Guid.Parse("15172003-4947-11e9-8646-d663bd873d93");
    };
}
