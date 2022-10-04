using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security;
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
using Windows.UI.Xaml.Navigation;
using Color = Windows.UI.Color;

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

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void BrowseLoadDot1Csv_Click(object sender, RoutedEventArgs e)
        {
            dotCsvPath1 = await BrowseFilePath("Select");
            Dot1CsvPath.Text = dotCsvPath1;

            pathsValid = CheckPaths(dotCsvPath1, dotCsvPath2); // check the if the paths are good enough
            if (pathsValid)
            {
                processedDotCsvPath = GenerateProcessedFilePath(dotCsvPath1, dotCsvPath2);
                SaveCsvPath.Text = processedDotCsvPath;
            }
        }

        private async void BrowseLoadDot2Csv_Click(object sender, RoutedEventArgs e)
        {
            dotCsvPath2 = await BrowseFilePath("Select");
            Dot2CsvPath.Text = dotCsvPath2;

            pathsValid = CheckPaths(dotCsvPath1, dotCsvPath2); // check the if the paths are good enough
            if (pathsValid)
            {
                processedDotCsvPath = GenerateProcessedFilePath(dotCsvPath1, dotCsvPath2);
                SaveCsvPath.Text = processedDotCsvPath;
            }
        }

        /// <summary>
        /// Check if the paths are good
        /// </summary>
        private bool CheckPaths(string path1, string path2)
        {
            bool arePathsValid = false;

            if (path1 == null || path2 == null || path1.Equals("") || path2.Equals(""))
            {
                return false;
            }

            if (path1.Equals(path2))
            {
                NotifyUser("Same file selected for both fields.", ErrorTypes.Warning);
                arePathsValid = false;
            }

            if (!Path.GetDirectoryName(path1).Equals(Path.GetDirectoryName(path2)))
            {
                NotifyUser("Files are from different directories.", ErrorTypes.Warning);
                arePathsValid = true;
            }

            if (Path.GetDirectoryName(path1).Equals(Path.GetDirectoryName(path2)))
            {
                NotifyUser("Files are from the same directories.", ErrorTypes.Info);
                arePathsValid = true;
            }

            return arePathsValid;
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

        private async Task<string> BrowseFilePath(string commitButtonText)
        {
            string filePath = "";

            try
            {
                if (openPicker == null) // allow only one instance for less mess
                {
                    openPicker = new FileOpenPicker
                    {
                        ViewMode = PickerViewMode.List,
                        SuggestedStartLocation = PickerLocationId.Downloads
                    };
                    openPicker.FileTypeFilter.Add(".csv");
                    openPicker.CommitButtonText = commitButtonText;

                    StorageFile file = await openPicker.PickSingleFileAsync();

                    if (file != null)
                    {
                        filePath = file.Path;
                        NotifyUser($"File '{Path.GetFileName(filePath)}' {Path.GetDirectoryName(filePath)} was selected.", ErrorTypes.Info);
                    }
                    else
                    {
                        NotifyUser("No file was selected.", ErrorTypes.Warning);
                    }
                }
            }
            catch (NullReferenceException e)
            {
                filePath = "";
            }
            finally
            {
                openPicker = null;
            }

            return filePath;
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
                    MessageBox.Background = new SolidColorBrush(Windows.UI.Colors.GreenYellow);
                    MessageBox.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
                    strMessage = "[INFO] " + strMessage;
                    break;

                case ErrorTypes.Warning:
                    MessageBox.Background = new SolidColorBrush(Windows.UI.Colors.Yellow);
                    MessageBox.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
                    strMessage = "[WARNING] " + strMessage;
                    break;

                case ErrorTypes.Error:
                    MessageBox.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    MessageBox.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
                    strMessage = "[ERROR] " + strMessage;
                    break;

                case ErrorTypes.Exception:
                    MessageBox.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    MessageBox.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
                    strMessage = "[EXCEPTION] " + strMessage;
                    break;

                default:
                    MessageBox.Background = new SolidColorBrush(Windows.UI.Colors.Transparent);
                    MessageBox.Foreground = new SolidColorBrush(Windows.UI.Colors.White);
                    break;
            }
            MessageBox.Text = strMessage;
        }
    }

    public enum ErrorTypes
    {
        Info,
        Warning,
        Error,
        Exception
    }
}
