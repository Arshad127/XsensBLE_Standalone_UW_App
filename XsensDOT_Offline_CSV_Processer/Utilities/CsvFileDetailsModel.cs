using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace XsensDOT_Offline_CSV_Processer.Utilities
{
    class CsvFileDetailsModel
    {
        public CsvFileDetailsModel(string filePath)
        {
            GetAndCheckFileName(filePath);
        }

        public CsvFileDetailsModel(StorageFile file)
        {
            GetAndCheckFileName(file.Path);
        }

        public CsvFileDetailsModel()
        {
            FileName = "";
            FirstTimeStamp = 0L;
            NumberOfRows = 0;
        }

        public void SetupFile(StorageFile file)
        {
            GetAndCheckFileName(file.Path);
        }

        public void SetupFile(string filePath)
        {
            GetAndCheckFileName(filePath);
        }

        public long FirstTimeStamp { get; set; }
        public string FileName { get; private set; }
        public string FilePath { get; private set; }
        public int NumberOfRows { get; set; }

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
