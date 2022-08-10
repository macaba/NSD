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
        [ObservableProperty] string workingFolder = @"C:\GitHub\Nuts\data\nsd";
        [ObservableProperty] ObservableCollection<string> inputFilePaths = new();
        [ObservableProperty] ObservableCollection<string> inputFileNames = new();
        [ObservableProperty] int selectedInputFileIndex = -1;
        [ObservableProperty] string outputFileName = "output.nsd";
        public ComboBoxItem? SelectedFftWidthItem { get; set; }
        public ComboBoxItem? SelectedInputUnitItem { get; set; }
        public bool FftStacking { get; set; } = false;
        public double XMin { get; set; } = 0.001;
        public double XMax { get; set; } = 10;
        public double YMin { get; set; } = 1;
        public double YMax { get; set; } = 100;
        public string WindowTitle { get { Version version = Assembly.GetExecutingAssembly().GetName().Version; return "NSD v" + version.Major + "." + version.Minor; } }

        public string GetSelectedInputFilePath()
        {
            if (inputFilePaths.Count > 0 && selectedInputFileIndex < inputFilePaths.Count)
                return inputFilePaths[selectedInputFileIndex];
            else
                return "";
        }
    }
}
