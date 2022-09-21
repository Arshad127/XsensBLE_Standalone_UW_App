using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace XsensBLE_Communication
{
    public class ConnectedBLEDevice
    {
        public DeviceInformation deviceInformation { get; }

        ConnectedBLEDevice(DeviceInformation deviceInformation)
        {
            this.deviceInformation = deviceInformation;
        }
    }
}
