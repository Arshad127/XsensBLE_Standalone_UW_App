using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace XsensDOT_Offline_CSV_Processer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private string dotCsvPath1 = "";
        private string dotCsvPath2 = "";
        private FileOpenPicker openPicker = null;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void BrowseLoadDot1Csv_Click(object sender, RoutedEventArgs e)
        {
            dotCsvPath1 = await BrowseFilePath("Validate");
            Dot1CsvPath.Text = dotCsvPath1;
        }

        private async void BrowseLoadDot2Csv_Click(object sender, RoutedEventArgs e)
        {
            dotCsvPath2 = await BrowseFilePath("Validate");
            Dot2CsvPath.Text = dotCsvPath2;
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
                        ViewMode = PickerViewMode.Thumbnail,
                        SuggestedStartLocation = PickerLocationId.Downloads
                    };
                    openPicker.FileTypeFilter.Add(".csv");
                    openPicker.FileTypeFilter.Add(".txt");
                    openPicker.CommitButtonText = commitButtonText;

                    StorageFile file = await openPicker.PickSingleFileAsync();

                    if (file != null) { filePath = file.Path; }
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
    }
}
