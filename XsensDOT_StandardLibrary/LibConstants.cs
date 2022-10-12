using System;
using System.Collections.Generic;
using System.Text;

namespace XsensDOT_StandardLibrary
{
    public static class LibConstants
    {
        public static readonly Guid BatteryCharacteristicUuid = Guid.Parse("15173001-4947-11e9-8646-d663bd873d93");
        public static readonly Guid BatteryServiceUuid = Guid.Parse("15173000-4947-11e9-8646-d663bd873d93");
        public static readonly Guid MeasurementServiceUuid = Guid.Parse("15172000-4947-11e9-8646-d663bd873d93");
        public static readonly Guid ShortPayloadCharacteristicUuid = Guid.Parse("15172004-4947-11e9-8646-d663bd873d93");
        public static readonly Guid MediumPayloadCharacteristicUuid = Guid.Parse("15172003-4947-11e9-8646-d663bd873d93");
        public static readonly Guid LongPayloadCharacteristicUuid = Guid.Parse("15172002-4947-11e9-8646-d663bd873d93");
        public static readonly Guid ControlCharacteristicUuid = Guid.Parse("15172001-4947-11e9-8646-d663bd873d93");
        public const string TargetDeviceName = "Xsens DOT";
    }
}
