using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reflection;

namespace NSD.UI
{
    // https://docs.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/overview
    public partial class MainWindowViewModel : ObservableObject
    {
        // Loaded from settings file
        [ObservableProperty] string? processWorkingFolder;
        [ObservableProperty] string? collateWorkingFolder;
        [ObservableProperty] string? sampleRate;

        [ObservableProperty] string status = "Status: Idle";
        [ObservableProperty] bool enabled = true;
        [ObservableProperty] ObservableCollection<string> inputFilePaths = new();
        [ObservableProperty] ObservableCollection<string> inputFileNames = new();
        [ObservableProperty] int selectedInputFileIndex = -1;
        [ObservableProperty] string outputFileName = "output.nsd";
        [ObservableProperty] bool sgFilterChecked = false;
        [ObservableProperty] IBrush statusBackground = Brushes.WhiteSmoke;
        public ComboBoxItem? SelectedFftWidthItem { get; set; }
        public ComboBoxItem? SelectedInputUnitItem { get; set; }
        public ComboBoxItem? SelectedFileFormatItem { get; set; }
        public bool FftStacking { get; set; } = false;
        public double XMin { get; set; } = 0.0001;
        public double XMax { get; set; } = 10;
        public double YMin { get; set; } = 0.1;
        public double YMax { get; set; } = 100;
        public string WindowTitle { get { Version version = Assembly.GetExecutingAssembly().GetName().Version; return "NSD v" + version.Major + "." + version.Minor; } }

        private Settings settings;

        public MainWindowViewModel(Settings settings)
        {
            this.settings = settings;
            processWorkingFolder = settings.ProcessWorkingFolder;
            //collateWorkingFolder = settings.CollateWorkingFolder;
            sampleRate = settings.SampleRate;
        }

        partial void OnProcessWorkingFolderChanged(string? value)
        {
            settings.ProcessWorkingFolder = value;
            settings.Save();
        }

        partial void OnCollateWorkingFolderChanged(string? value)
        {
            //settings.CollateWorkingFolder = value;
            //settings.Save();
        }

        partial void OnSampleRateChanged(string? value)
        {
            settings.SampleRate = value;
            settings.Save();
        }

        partial void OnStatusChanged(string value)
        {
            if(value.Contains("Error"))
            {
                StatusBackground = Brushes.Red;
            }
            else
            {
                StatusBackground = Brushes.WhiteSmoke;
            }
        }

        public string GetSelectedInputFilePath()
        {
            if (inputFilePaths.Count > 0 && selectedInputFileIndex < inputFilePaths.Count)
                return inputFilePaths[selectedInputFileIndex];
            else
                return "";
        }
    }
}
