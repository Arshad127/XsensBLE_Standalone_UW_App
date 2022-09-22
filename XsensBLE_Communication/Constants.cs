using System;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace XsensBLE_Communication
{
    // Define the characteristics and other properties of our custom service.
    public class Constants
    {
        public static readonly string targetDeviceName = "Xsens DOT";

        public static readonly Guid BatteryCharacteristicUuid = Guid.Parse("15173001-4947-11e9-8646-d663bd873d93");
        public static readonly Guid BatteryServiceUuid = Guid.Parse("15173000-4947-11e9-8646-d663bd873d93");

        public static readonly Guid MeasurementServiceUuid = Guid.Parse("15172000-4947-11e9-8646-d663bd873d93");
        public static readonly Guid MeasurementCharacteristicUuid = Guid.Parse("15172003-4947-11e9-8646-d663bd873d93");
    };

    public enum PayloadType
    {
        BatteryDetails = 27,
        MeasurementGeneralDetails = 28,
        HighFidelityWithMag = 1,
        ExtendedQuaternion = 2,
        CompleteQuaternion = 3,
        OrientationEuler = 4,
        OrientationQuaternion = 5,
        FreeAcceleration = 6,
        ExtendedEuler = 7,
        CompleteEuler = 16,
        HighFidelity = 17,
        DeltaQuantitiesWithMag = 18,
        DeltaQuantities = 19,
        RateQuantitiesWithMag = 20,
        RateQuantities = 21,
        CustomMode1 = 22,
        CustomMode2 = 23,
        CustomMode3 = 24,
        CustomMode4 = 25,
        CustomMode5 = 26
    }
}
