using System;
using System.Collections.Generic;
using System.Text;

namespace XsensDOT_StandardLibrary
{
    /// <summary>
    /// Error types that can show on the UI with corresponding error background colours.
    /// </summary>
    public enum ErrorTypes
    {
        Info,
        Warning,
        Error,
        Exception
    }

    /// <summary>
    /// Table Headers that are read and written into the CSVs parsed. They must be exactly
    /// as logged from the Xsens DOT sensors or else it will cause parsing errors and
    /// inconsistencies.
    /// </summary>
    public enum Header
    {
        PacketCount,
        SampleTimeFine,
        Quat_W,
        Quat_X,
        Quat_Y,
        Quat_Z,
        Euler_X,
        Euler_Y,
        Euler_Z,
        FreeAcc_X,
        FreeAcc_Y,
        FreeAcc_Z,
        Acc_X,
        Acc_Y,
        Acc_Z,
        Gyr_X,
        Gyr_Y,
        Gyr_Z,
        JointAngle_X,
        JointAngle_Y,
        JointAngle_Z,
        Quat_Combined,
        FreeAcc_Combined,
        Euler_Combined,
        Status
    }

    /// <summary>
    /// Identifiers for the body segments the sensors are attached to.
    /// </summary>
    public enum DeviceIdentifier
    {
        Hand,
        Arm
    }

    /// <summary>
    /// The various payload that can be outputted from the XsensDOT sensor.
    /// </summary>
    public enum PayloadType
    {
        BatteryDetails = 27,
        MeasurementGeneralDetails = 28,
        OrientationResetStatus = 29,
        OrientationResetControlData = 30,
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

    public enum HeadingResults
    {
        ResetHeading = 1,
        RevertHeadingToDefault = 7,
        DefaultStatus = 8
    }
}
