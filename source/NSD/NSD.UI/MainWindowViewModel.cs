using Avalonia.Controls;
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
        [ObservableProperty] string status = "Status: Idle";
        [ObservableProperty] bool enabled = true;
        [ObservableProperty] string? processWorkingFolder;
        [ObservableProperty] string? collateWorkingFolder;
        [ObservableProperty] ObservableCollection<string> inputFilePaths = new();
        [ObservableProperty] ObservableCollection<string> inputFileNames = new();
        [ObservableProperty] int selectedInputFileIndex = -1;
        [ObservableProperty] string outputFileName = "output.nsd";
        public ComboBoxItem? SelectedFftWidthItem { get; set; }
        public ComboBoxItem? SelectedInputUnitItem { get; set; }
        public bool FftStacking { get; set; } = false;
        public double XMin { get; set; } = 0.0001;
        public double XMax { get; set; } = 10;
        public double YMin { get; set; } = 1;
        public double YMax { get; set; } = 1000;
        public string WindowTitle { get { Version version = Assembly.GetExecutingAssembly().GetName().Version; return "NSD v" + version.Major + "." + version.Minor; } }

        private Settings settings;

        public MainWindowViewModel(Settings settings)
        {
            this.settings = settings;
            processWorkingFolder = settings.ProcessWorkingFolder;
            collateWorkingFolder = settings.CollateWorkingFolder;
        }

        partial void OnProcessWorkingFolderChanged(string? value)
        {
            settings.ProcessWorkingFolder = value;
            settings.Save();
        }

        partial void OnCollateWorkingFolderChanged(string? value)
        {
            settings.CollateWorkingFolder = value;
            settings.Save();
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
