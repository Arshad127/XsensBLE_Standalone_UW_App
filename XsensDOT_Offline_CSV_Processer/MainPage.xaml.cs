using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using XsensDOT_Offline_CSV_Processer.Utilities;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace XsensDOT_Offline_CSV_Processer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private string dotCsvPath1 = null;
        private string dotCsvPath2 = null;

        private bool pathsValid = false;
        private string processedDotCsvPath = null;
        private FileOpenPicker openPicker = null;

        private DataTable csvDataTable1, csvDataTable2, combinedCsvDataTable;
        private DataSet dataSet;
        private string consoleMessageThread = "";

        private CsvFileDetailsModel csv1BriefDetails;
        private CsvFileDetailsModel csv2BriefDetails;

        public MainPage()
        {
            this.InitializeComponent();
            ComputeAngles.IsEnabled = false;
            SaveCsvFiles.IsEnabled = false;
        }

        /// <summary>
        /// Browse CSV for DOT 1
        /// </summary>
        private async void BrowseLoadDot1Csv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Dot1CsvTimeStamp.Text = "";     // set the time stamp to 0 in case the button is pressed repeatedly
                CsvTimeStampOffset.Text = "";   // set the offset time stamp to 0 in case the button is pressed repeatedly
                csvDataTable1 = SetUpDataTable("1"); // preps the table so we can insert data into it in the next step
                csv1BriefDetails = await BrowseAndLoadFilePath(LoadingCsv1ProgressBar, csvDataTable1); // file selection and parsing
                dotCsvPath1 = csv1BriefDetails.FilePath; // to keep the global variable happy
                Dot1CsvPath.Text = csv1BriefDetails.FilePath; // displays the file path on the UI
                Dot1CsvTimeStamp.Text = csv1BriefDetails.FirstTimeStamp.ToString();

                pathsValid = CheckPaths(dotCsvPath1, dotCsvPath2); // check the if the paths are good enough
                if (pathsValid)
                {
                    processedDotCsvPath = GenerateProcessedFilePath(dotCsvPath1, dotCsvPath2);
                    SaveCsvPath.Text = processedDotCsvPath;
                    ComputeAngles.IsEnabled = true; // enables the button

                    // calculate the time offset
                    CsvTimeStampOffset.Text = "Δt = " + Math.Abs(csv1BriefDetails.FirstTimeStamp - csv2BriefDetails.FirstTimeStamp);
                }
            }
            catch (ArgumentNullException excptDetails)
            {
                NotifyUser(excptDetails.Message, ErrorTypes.Exception);
            }
        }

        /// <summary>
        /// Browse CSV for DOT 2
        /// </summary>
        private async void BrowseLoadDot2Csv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Dot2CsvTimeStamp.Text = "0"; // set the time stamp to 0 in case the button is pressed repeatedly
                CsvTimeStampOffset.Text = "";   // set the offset time stamp to 0 in case the button is pressed repeatedly
                csvDataTable2 = SetUpDataTable("2"); // preps the table so we can insert data into it in the next step
                csv2BriefDetails = await BrowseAndLoadFilePath(LoadingCsv2ProgressBar, csvDataTable2); // file selection and parsing
                dotCsvPath2 = csv2BriefDetails.FilePath; // to keep the global variable happy
                Dot2CsvPath.Text = csv2BriefDetails.FilePath; // displays the file path on the UI
                Dot2CsvTimeStamp.Text = csv2BriefDetails.FirstTimeStamp.ToString();

                pathsValid = CheckPaths(dotCsvPath1, dotCsvPath2); // check the if the paths are good enough
                if (pathsValid)
                {
                    processedDotCsvPath = GenerateProcessedFilePath(dotCsvPath1, dotCsvPath2);
                    SaveCsvPath.Text = processedDotCsvPath;
                    ComputeAngles.IsEnabled = true; // enables the button

                    // calculate the time offset
                    CsvTimeStampOffset.Text = "Δt = " + Math.Abs(csv1BriefDetails.FirstTimeStamp - csv2BriefDetails.FirstTimeStamp);
                }
            }
            catch (ArgumentNullException excptDetails)
            {
                NotifyUser(excptDetails.Message, ErrorTypes.Exception);
            }
        }

        /// <summary>
        /// Load and parse the CSVs
        /// </summary>
        private void ComputeAngles_Click(object sender, RoutedEventArgs e)
        {
            // Disable the picker buttons.
            // It'll be messy if source files are changing while processing
            BrowseLoadDot1Csv.IsEnabled = false;
            BrowseLoadDot2Csv.IsEnabled = false;

            // Combine the tables
            csvDataTable1.Merge(csvDataTable2, false, MissingSchemaAction.Add);
            csvDataTable1.AcceptChanges();

            // Print the table
            FillDataGrid(csvDataTable1, UIDataGrid);

            // Parse through the table and compute the angles each
            // Create a new row where we are going to put the calculated joint angles
            csvDataTable1.Columns.Add(Header.JointAnglesDeg.ToString(), typeof(double));
            csvDataTable1.AcceptChanges();


            // Reenable the picker buttons.
            BrowseLoadDot1Csv.IsEnabled = true;
            BrowseLoadDot2Csv.IsEnabled = true;

        }

        /// <summary>
        /// https://stackoverflow.com/questions/53730207/how-to-fill-datagrid-with-datatable-content-in-uwp-c-sharp
        /// </summary>
        private static void FillDataGrid(DataTable table, DataGrid grid)
        {
            grid.Columns.Clear();
            grid.AutoGenerateColumns = false;
            for (int i = 0; i < table.Columns.Count; i++)
            {
                grid.Columns.Add(new DataGridTextColumn()
                {
                    Header = table.Columns[i].ColumnName,
                    Binding = new Binding { Path = new PropertyPath("[" + i.ToString() + "]") }
                });
            }

            var collection = new ObservableCollection<object>();
            foreach (DataRow row in table.Rows)
            {
                collection.Add(row.ItemArray);
            }

            grid.ItemsSource = collection;
        }

        private void SetProgressBarValue(ProgressBar progressBar, double progressValue)
        {
            if (Dispatcher.HasThreadAccess)
            {
                progressBar.Value = progressValue;
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => progressBar.Value = progressValue);
            }
        }

        /// <summary>
        /// Create the tables skeleton for storing the data from the csv files
        /// </summary>
        private DataTable SetUpDataTable(string identifier)
        {
            string tableName = "XSENS_MEASUREMENT_DATA_CSV_DOT_" + identifier;

            // need to check for duplicate tables in the dataset.
            // be weary of the race conditions given there is only one dataset being access by two tables
            // ****CHECK RACE CONDITIONS*****
            if (dataSet == null)
            {
                dataSet = new DataSet();
            }
            else if (dataSet.Tables.Contains(tableName))
            {
                // remove duplicate tables else problemo
                dataSet.Tables.Remove(tableName);
            }

            DataTable csvDataTable = new DataTable(tableName);

            // Column
            csvDataTable.Columns.Add(Header.PacketCount + identifier, typeof(int));

            // Unique Column & primary key column
            var dtColumn = new DataColumn();
            dtColumn.DataType = typeof(int);
            dtColumn.ColumnName = Header.SampleTimeFine.ToString(); // NO IDENTIFIER
            dtColumn.AutoIncrement = false;
            dtColumn.ReadOnly = true;
            dtColumn.Unique = true;
            csvDataTable.Columns.Add(dtColumn);

            // Workaround to create the unique primary key for this table
            csvDataTable.PrimaryKey = new DataColumn[] {csvDataTable.Columns[Header.SampleTimeFine.ToString()] }; // NO IDENTIFIER

            //csvDataTable.Columns.Add(Header.Quat_W + identifier, typeof(double));
            //csvDataTable.Columns.Add(Header.Quat_X + identifier, typeof(double));
            //csvDataTable.Columns.Add(Header.Quat_Y + identifier, typeof(double));
            //csvDataTable.Columns.Add(Header.Quat_Z + identifier, typeof(double));
            //csvDataTable.Columns.Add(Header.FreeAcc_X + identifier, typeof(double));
            //csvDataTable.Columns.Add(Header.FreeAcc_Y + identifier, typeof(double));
            //csvDataTable.Columns.Add(Header.FreeAcc_Z + identifier, typeof(double));
            csvDataTable.Columns.Add(Header.Status + identifier, typeof(int));
            csvDataTable.Columns.Add(Header.Quat_Combined + identifier, typeof(Quaternion));
            csvDataTable.Columns.Add(Header.FreeAcc_Combined + identifier, typeof(Vector3));

            // add the table to the dataset
            dataSet.Tables.Add(csvDataTable);

            return csvDataTable;
        }

        /// <summary>
        /// Check if the paths are good
        /// </summary>
        private bool CheckPaths(string path1, string path2)
        {
            if (path1 == null || path2 == null || path1.Equals("") || path2.Equals(""))
            {
                return false;
            }

            if (path1.Equals(path2))
            {
                NotifyUser("Same file selected for both fields.", ErrorTypes.Warning);
                return false;
            }

            if (!Path.GetDirectoryName(path1).Equals(Path.GetDirectoryName(path2)))
            {
                NotifyUser("Files are from different directories.", ErrorTypes.Warning);
               return true;
            }

            if (Path.GetDirectoryName(path1).Equals(Path.GetDirectoryName(path2)))
            {
                NotifyUser("Files are from the same directories and are valid.", ErrorTypes.Info);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Generates the file path name for the processed CSV file
        /// </summary>
        private string GenerateProcessedFilePath(string path1, string path2)
        {
            string outputPath = "";

            if (Path.GetDirectoryName(path1).Equals(Path.GetDirectoryName(path2)))
            {
                string fileName1Essence = Path.GetFileNameWithoutExtension(path1);
                string[] splitFileName1 = fileName1Essence.Split("_");
                if (splitFileName1.Length == 4)
                {
                    string fileName2Essence = Path.GetFileNameWithoutExtension(path2);
                    string[] splitFileName2 = fileName2Essence.Split("_");
                    if (splitFileName2.Length == 4)
                    {
                        if (splitFileName1[1].Equals(splitFileName2[1]))
                        {
                            outputPath = "PROCESSED_" + splitFileName1[0] + "_" + splitFileName2[0] + "_" + splitFileName1[1] + "_" + splitFileName1[2] + "_" +
                                         splitFileName1[3] + "_" + splitFileName2[2] + "_" + splitFileName2[3] + ".csv";
                            outputPath = Path.Combine(Path.GetDirectoryName(path1), outputPath);
                        }
                        else
                        {
                            outputPath = Path.GetDirectoryName(path1);
                        }
                    }
                }
            }
            else
            {
                outputPath = Path.GetDirectoryName(path1);
            }

            return outputPath;
        }

        private async Task<CsvFileDetailsModel> BrowseAndLoadFilePath(ProgressBar progressBar, DataTable dataTable)
        {
            string filePath = "";
            CsvFileDetailsModel csvFeedbackDetails = new CsvFileDetailsModel();

            try
            {
                openPicker = new FileOpenPicker
                {
                    ViewMode = PickerViewMode.List,
                    SuggestedStartLocation = PickerLocationId.Downloads
                };
                openPicker.FileTypeFilter.Add(".csv");
                openPicker.CommitButtonText = "Select";

                StorageFile file = await openPicker.PickSingleFileAsync();

                // Parse the file in a new task to not freeze the UI
                await Task.Run(async () =>
                {
                    if (file != null)
                    {
                        // Retrieves the filePath
                        filePath = file.Path;
                        csvFeedbackDetails.SetupFile(filePath); // will do the name checks
                        
                        NotifyUser($"File '{Path.GetFileName(filePath)}' was selected.", ErrorTypes.Info);

                        // Parsing the csv file into the table
                        // this is to skip the gap between the title on the CSV and the beginning of the data.
                        BooleanSecondChance csvGapsAllowance = new BooleanSecondChance(1); // skip csv gaps
                        
                        using (CsvFileReader csvReader = new CsvFileReader(await file.OpenStreamForReadAsync()))
                        {
                            int rowCounter = 0;
                            CsvRow extractedRow = new CsvRow();
                            while (csvReader.ReadRow(extractedRow) || csvGapsAllowance.FeelingLucky())
                            {
                                if ((extractedRow.Count > 9) && int.TryParse(extractedRow[0], out var packetNumber))
                                {
                                    var sampleTimeFine = long.Parse(extractedRow[1], CultureInfo.InvariantCulture);
                                    var quat_W = double.Parse(extractedRow[2], CultureInfo.InvariantCulture);
                                    var quat_X = double.Parse(extractedRow[3], CultureInfo.InvariantCulture);
                                    var quat_Y = double.Parse(extractedRow[4], CultureInfo.InvariantCulture);
                                    var quat_Z = double.Parse(extractedRow[5], CultureInfo.InvariantCulture);
                                    var freeAcc_X = double.Parse(extractedRow[6], CultureInfo.InvariantCulture);
                                    var freeAcc_Y = double.Parse(extractedRow[7], CultureInfo.InvariantCulture);
                                    var freeAcc_Z = double.Parse(extractedRow[8], CultureInfo.InvariantCulture);
                                    var status = int.Parse(extractedRow[9], CultureInfo.InvariantCulture);

                                    rowCounter ++;
                                    dataTable.Rows.Add(new Object[]
                                    {
                                        packetNumber,
                                        sampleTimeFine,
                                        //quat_W,
                                        //quat_X,
                                        //quat_Y,
                                        //quat_Z,
                                        //freeAcc_X,
                                        //freeAcc_Y,
                                        //freeAcc_Z,
                                        status,
                                        new Quaternion((float)quat_X, (float)quat_Y, (float)quat_Z, (float)quat_W),
                                        new Vector3((float)freeAcc_X, (float)freeAcc_Y, (float)freeAcc_Z)
                                    });

                                    // first time stamp gets printed on the UI for indication of whether or not data is synced
                                    if (rowCounter == 1)
                                    {
                                        csvFeedbackDetails.FirstTimeStamp = long.Parse(extractedRow[1], CultureInfo.InvariantCulture);
                                    }
                                }
                                // Show updates on the progressbar
                                SetProgressBarValue(progressBar, csvReader.BaseStream.Position * 100 / csvReader.BaseStream.Length);
                            }
                            dataTable.AcceptChanges();

                            csvFeedbackDetails.NumberOfRows = rowCounter;
                            NotifyUser($"File parsing completed with {rowCounter} rows", ErrorTypes.Info);
                        }
                    }
                    else
                    {
                        NotifyUser("No file was selected.", ErrorTypes.Warning);
                    }
                });
            }
            catch (NullReferenceException e) // to be expanded to other specific exceptions as the program is tested.
            {
                NotifyUser(e.Message, ErrorTypes.Exception);
            }
            catch (FileLoadException e)
            {
                NotifyUser(e.Message, ErrorTypes.Exception);
            }
            finally
            {
                openPicker = null;
            }

            return csvFeedbackDetails;
        }

        public void NotifyUser(string strMessage, ErrorTypes errorType)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatus(strMessage, errorType);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, errorType));
            }
        }

        private void UpdateStatus(string strMessage, ErrorTypes errorType)
        {
            switch (errorType)
            {
                case ErrorTypes.Info:
                    MessageBox.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
                    MessageBox.Background = new SolidColorBrush(Windows.UI.Colors.GreenYellow);
                    consoleMessageThread = "[INFO] " + strMessage + "\n" + consoleMessageThread;
                    break;

                case ErrorTypes.Warning:
                    MessageBox.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
                    MessageBox.Background = new SolidColorBrush(Windows.UI.Colors.Yellow);
                    consoleMessageThread = "[WARNING] " + strMessage + "\n" + consoleMessageThread;
                    break;

                case ErrorTypes.Error:
                    MessageBox.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
                    MessageBox.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    consoleMessageThread = "[ERROR] " + strMessage + "\n" + consoleMessageThread;
                    break;

                case ErrorTypes.Exception:
                    MessageBox.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
                    MessageBox.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    consoleMessageThread = "[EXCEPTION] " + strMessage + "\n" + consoleMessageThread;
                    break;
            }
            MessageBox.Text = consoleMessageThread;
        }
    }


}
