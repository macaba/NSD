using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;

namespace NSD.UI
{
    // https://www.reactiveui.net/docs/handbook/view-models/
    public class MainWindowViewModel : ReactiveObject
    {
        public MainWindowViewModel()
        {
            //DoTheThing = ReactiveCommand.Create(RunTheThing);
        }

        private string status = "Status: Idle";
        public string Status { get => status; set => this.RaiseAndSetIfChanged(ref status, value); }

        private bool enabled = true;
        public bool Enabled { get => enabled; set => this.RaiseAndSetIfChanged(ref enabled, value); }

        private string searchFilePath = @"C:\GitHub\Notebooks\Solution\NSD.WPF\10 ohm data";
        public string WorkingFolder { get => searchFilePath; set => this.RaiseAndSetIfChanged(ref searchFilePath, value); }

        private ObservableCollection<string> inputFilePaths = new();
        public ObservableCollection<string> InputFilePaths { get => inputFilePaths; set => this.RaiseAndSetIfChanged(ref inputFilePaths, value); }

        private ObservableCollection<string> inputFilesNames = new();
        public ObservableCollection<string> InputFileNames { get => inputFilesNames; set => this.RaiseAndSetIfChanged(ref inputFilesNames, value); }

        private int selectedInputFileIndex = -1;
        public int SelectedInputFileIndex { get => selectedInputFileIndex; set => this.RaiseAndSetIfChanged(ref selectedInputFileIndex, value); }

        private string outputFileName = "output.nsd";
        public string OutputFileName { get => outputFileName; set => this.RaiseAndSetIfChanged(ref outputFileName, value); }

        public ComboBoxItem SelectedFftWidthItem { get; set; }
        public ComboBoxItem SelectedInputUnitItem { get; set; }    
        public bool FftStacking { get; set; } = false;
        public double XMin { get; set; } = 0.001;
        public double XMax { get; set; } = 10;
        public double YMin { get; set; } = 1;
        public double YMax { get; set; } = 100;

        public string GetSelectedInputFilePath()
        {
            if (inputFilePaths.Count > 0 && selectedInputFileIndex < inputFilePaths.Count)
                return inputFilePaths[selectedInputFileIndex];
            else
                return "";
        }

        //public ReactiveCommand<Unit, Unit> DoTheThing { get; }

        //private void RunTheThing() { }
    }
}
