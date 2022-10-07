using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XsensDOT_Offline_CSV_Processer.Utilities
{
    public enum ErrorTypes
    {
        Info,
        Warning,
        Error,
        Exception
    }

    public enum Header
    {
        PacketCount,
        SampleTimeFine,
        Quat_W,
        Quat_X,
        Quat_Y,
        Quat_Z,
        FreeAcc_X,
        FreeAcc_Y,
        FreeAcc_Z,
        Status,
        Quat_Combined,
        FreeAcc_Combined,
        JointAnglesDeg
    }
}
