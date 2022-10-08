using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace XsensDOT_Offline_CSV_Processer.Utilities
{
    /// <summary>
    /// Stores brief details of the csv being parsed for easy information transfer
    /// </summary>
    class CsvFileDetailsModel
    {
        /// <summary>
        /// Constructor using the file path as input
        /// </summary>
        public CsvFileDetailsModel(string filePath)
        {
            GetAndCheckFileName(filePath);
        }

        /// <summary>
        /// Constructor using the file (StorageFile) as input
        /// </summary>
        public CsvFileDetailsModel(StorageFile file)
        {
            GetAndCheckFileName(file.Path);
        }

        /// <summary>
        /// Default constructor initialising field to zeros and ""
        /// </summary>
        public CsvFileDetailsModel()
        {
            FileName = "";
            FirstTimeStamp = 0L;
            NumberOfRows = 0;
        }

        /// <summary>
        /// Set up the already created object with a file (StorageFile)
        /// </summary>
        public void SetupFile(StorageFile file)
        {
            GetAndCheckFileName(file.Path);
        }

        /// <summary>
        /// Set up the already created object with a file path (string)
        /// </summary>
        public void SetupFile(string filePath)
        {
            GetAndCheckFileName(filePath);
        }

        /// <summary>
        /// Manually input the first time stamp of the csv for later referencing
        /// </summary>
        public long FirstTimeStamp { get; set; }

        /// <summary>
        /// File name is extracted from the constructor CsvFileDetailsModel() or SetupFile()
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// File path is extracted from the constructor CsvFileDetailsModel() or SetupFile()
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Manually input the number of rows in the CSV for later referencing
        /// </summary>
        public int NumberOfRows { get; set; }

        /// <summary>
        /// Manually input the device tag
        /// </summary>
        public string DeviceTag { get; set; }

        /// <summary>
        /// Manually input the firmware version
        /// </summary>
        public string FirmwareVersion { get; set; }

        /// <summary>
        /// Manually input the AppVersion
        /// </summary>
        public string AppVersion { get; set; }

        /// <summary>
        /// Manually input the SyncStatus
        /// </summary>
        public string SyncStatus { get; set; }

        /// <summary>
        /// Manually input the output rate
        /// </summary>
        public string OutputRate { get; set; }

        /// <summary>
        /// Manually input the filter profile
        /// </summary>
        public string FilterProfile { get; set; }

        /// <summary>
        /// Manually input the measurement mode
        /// </summary>
        public string MeasurementMode { get; set; }

        /// <summary>
        /// Manually input the recording start time
        /// </summary>
        public string StartTime { get; set; }

        private void GetAndCheckFileName(string filePath)
        {
            string[] tempSplitFileName = Path.GetFileNameWithoutExtension(filePath).Split("_");
            if (tempSplitFileName.Length != 4)
            {
                throw new FileLoadException("Unexpected file name. Expecting the format of SENSORADDR_DATE_TIME_MS.csv");
            }

            FileName = Path.GetFileNameWithoutExtension(filePath);
            FilePath = filePath;
        }


    }
}
