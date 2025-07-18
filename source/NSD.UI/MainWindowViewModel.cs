﻿using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Reflection;

namespace NSD.UI
{
    // https://docs.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/overview
    public partial class MainWindowViewModel : ObservableObject
    {
        // Loaded from settings file
        [ObservableProperty] string? processWorkingFolder;
        [ObservableProperty] string? acquisitionTime;

        [ObservableProperty] string status = "Status: Idle";
        [ObservableProperty] bool enabled = true;
        [ObservableProperty] ObservableCollection<string> inputFilePaths = new();
        [ObservableProperty] ObservableCollection<string> inputFileNames = new();
        [ObservableProperty] int selectedInputFileIndex = -1;
        [ObservableProperty] string outputFileName = "output.nsd";
        [ObservableProperty] bool sgFilterChecked = false;
        [ObservableProperty] bool markersChecked = false;
        [ObservableProperty] IBrush statusBackground = Brushes.WhiteSmoke;
        [ObservableProperty] string inputScaling = "1.0";
        [ObservableProperty] string logNsdPointsDecade = "10";
        [ObservableProperty] string logNsdPointsDecadeScaling = "2.0";
        [ObservableProperty] string logNsdMinAverages = "1";
        [ObservableProperty] string logNsdMinLength = "1024";

        public ComboBoxItem? SelectedAcquisitionTimebaseItem { get; set; }

        private ComboBoxItem? selectedNsdAlgorithm;
        public ComboBoxItem? SelectedNsdAlgorithm
        {
            get => selectedNsdAlgorithm; set
            {
                selectedNsdAlgorithm = value;
                switch ((string)selectedNsdAlgorithm.Content)
                {
                    case "Logarithmic":
                        AlgorithmLog = true;
                        AlgorithmLin = false;
                        AlgorithmLinStack = false;
                        break;
                    case "Linear":
                        AlgorithmLog = false;
                        AlgorithmLin = true;
                        AlgorithmLinStack = false;
                        break;
                    case "Linear dual":
                    case "Linear stacking":
                        AlgorithmLog = false;
                        AlgorithmLin = false;
                        AlgorithmLinStack = true;
                        break;
                }
            }
        }
        public ComboBoxItem? SelectedLinearLengthItem { get; set; }
        public ComboBoxItem? SelectedLinearStackingLengthItem { get; set; }
        public ComboBoxItem? SelectedLinearStackingMinLengthItem { get; set; }
        public ComboBoxItem? SelectedLogNsdMinLength { get; set; }
        [ObservableProperty] bool algorithmLog = true;          // Controls visibility of sub-stack panel
        [ObservableProperty] bool algorithmLin = false;         // Controls visibility of sub-stack panel
        [ObservableProperty] bool algorithmLinStack = false;    // Controls visibility of sub-stack panel

        public ComboBoxItem? SelectedFileFormatItem { get; set; }
        public string XMin { get; set; } = "0.1";
        public string XMax { get; set; } = "1k";
        public string YMin { get; set; } = "1n";
        public string YMax { get; set; } = "1m";
        public string WindowTitle { get { Version version = Assembly.GetExecutingAssembly().GetName().Version; return "NSD v" + version.Major + "." + version.Minor; } }
        [ObservableProperty] bool csvHasHeader = false;
        [ObservableProperty] int csvColumnIndex = 0;

        private Settings settings;


        public MainWindowViewModel(Settings settings, MainWindow window)
        {
            this.settings = settings;
            processWorkingFolder = settings.ProcessWorkingFolder;
            acquisitionTime = settings.AcquisitionTime;

            window.cbTime.SelectedIndex = settings.AcquisitionTimeUnit switch
            {
                "NPLC (50Hz)" => 0,
                "NPLC (60Hz)" => 1,
                "s" => 2,
                "ms" => 3,
                "μs" => 4,
                "ns" => 5,
                "SPS" => 6,
                "kSPS" => 7,
                "MSPS" => 8,
                //_ => throw new Exception("Invalid AcquisitionTimeUnit")
                _ => 0
            };
        }

        partial void OnProcessWorkingFolderChanged(string? value)
        {
            settings.ProcessWorkingFolder = value;
            settings.Save();
        }

        partial void OnAcquisitionTimeChanged(string? value)
        {
            settings.AcquisitionTime = value;
            settings.Save();
        }

        partial void OnStatusChanged(string value)
        {
            if (value.Contains("Error"))
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
