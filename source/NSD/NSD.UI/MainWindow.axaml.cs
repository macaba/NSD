using Avalonia.Controls;
using Avalonia.Interactivity;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NSD.UI
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel viewModel;
        private Spectrum spectrum;
        private Settings settings;

        public MainWindow()
        {
            InitializeComponent();
            settings = Settings.Load();
            viewModel = new(settings);
            DataContext = viewModel;
            InitNsdChart();
        }

        public async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(viewModel.ProcessWorkingFolder))
            {
                var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow("Folder not found", "Search folder not found");
                await messageBoxStandardWindow.Show();
                return;
            }

            var files = Directory.EnumerateFiles(viewModel.ProcessWorkingFolder, "*.csv");
            viewModel.InputFilePaths.Clear();
            viewModel.InputFileNames.Clear();
            foreach (var file in files)
            {
                viewModel.InputFilePaths.Add(file);
                viewModel.InputFileNames.Add(Path.GetFileName(file));
            }
            viewModel.SelectedInputFileIndex = 0;
        }

        public async void BtnCollateSearch_Click(object sender, RoutedEventArgs e)
        {

        }

        public async void BtnCollateAdd_Click(object sender, RoutedEventArgs e)
        {

        }
        
        public async void btnRun_Click(object sender, RoutedEventArgs e)
        {
            var path = viewModel.GetSelectedInputFilePath();
            if (!File.Exists(path))
            {
                await ShowError("File not found", "Input CSV file not found");
                viewModel.Enabled = true;
                return;
            }


            if (!double.TryParse(tbSampleRate.Text, out double sampleRate))
            {
                await ShowError("Invalid sample rate", "Invalid sample rate value");
                viewModel.Enabled = true;
                return;
            }
            var fftWidth = int.Parse((string)(viewModel.SelectedFftWidthItem).Content);
            var inputScaling = ((string)(viewModel.SelectedInputUnitItem).Content) switch
            {
                "V" => 1.0,
                "mV" => 1e-3,
                "uV" => 1e-6,
                "nV" => 1e-9,
                _ => 1.0
            };

            viewModel.Status = "Status: Loading CSV...";
            viewModel.Enabled = false;

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = await csv.GetRecordsAsync<double>().ToListAsync();
            if (records.Count == 0)
            {
                await ShowError("No CSV records", "No CSV records found");
                viewModel.Enabled = true;
                return;
            }
            if (fftWidth > records.Count)
            {
                await ShowError("FFT too long", "FFT width is longer than input data");
                viewModel.Enabled = true;
                return;
            }

            viewModel.Status = "Status: Calculating NSD...";

            for (int i = 0; i < records.Count; i++)
            {
                records[i] *= inputScaling;
            }
            // Trim ignoreBins from either end of the real spectrum
            int ignoreBins = 3;         //FTNI = 3, HFT90 = 3
            if (viewModel.FftStacking)
            {
                var nsd = await Welch.StackedNSD_Async(input: records.ToArray(), sampleRate, ignoreBins, outputWidth: fftWidth);
                spectrum = nsd;
            }
            else
            {
                //var sine = Signals.OneVoltRmsTestSignal();
                //await Welch.StackedNSD_Async(input: records.ToArray(), sampleRate, inputScale: 1e-3, outputWidth: fftWidth);
                //var nsd = Welch.NSD_SingleSeries(input: sine, sampleRate, inputScale: 1, outputWidth: fftWidth);
                var nsd = await Welch.NSD_Async(input: records.ToArray(), sampleRate, ignoreBins, outputWidth: fftWidth);
                spectrum = nsd;
            }

            Memory<double> yArray;
            if (cbFilter.IsChecked == true)
                yArray = new SavitzkyGolayFilter(5, 1).Process(spectrum.Values.Span);
            else
                yArray = spectrum.Values;

            UpdateNSDChart(spectrum.Frequencies, yArray);
            viewModel.Status = "Status: Processing complete";
            viewModel.Enabled = true;
        }

        public async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var outputFilePath = Path.Combine(viewModel.ProcessWorkingFolder, viewModel.OutputFileName);

            CsvConfiguration config = new(CultureInfo.InvariantCulture);
            config.Delimiter = ",";
            using var writer = new StreamWriter(outputFilePath);
            using var csvWriter = new CsvWriter(writer, config);
            dynamic header = new ExpandoObject();
            header.Frequency = "";
            header.Noise = "";
            csvWriter.WriteDynamicHeader(header);
            csvWriter.NextRecord();

            for (int i = 0; i < spectrum.Frequencies.Length; i++)
            {
                csvWriter.WriteField(spectrum.Frequencies.Span[i]);
                csvWriter.WriteField(spectrum.Values.Span[i]);
                csvWriter.NextRecord();
            }
        }

        private void btnSetAxis_Click(object sender, RoutedEventArgs e)
        {
            WpfPlot1.Plot.SetAxisLimits(Math.Log10(viewModel.XMin), Math.Log10(viewModel.XMax), Math.Log10(viewModel.YMin), Math.Log10(viewModel.YMax));
            WpfPlot1.Render();
        }

        public void UpdateNSDChart(Memory<double> x, Memory<double> y)
        {
            WpfPlot1.Configuration.DpiStretch = false;
            WpfPlot1.Configuration.Quality = ScottPlot.Control.QualityMode.High;
            WpfPlot1.Plot.Clear();
            double[] logXs = x.ToArray().Select(pt => Math.Log10(pt)).ToArray();
            double[] logYs = y.ToArray().Select(pt => Math.Log10(pt * 1E9)).ToArray();
            var scatter = WpfPlot1.Plot.AddScatterLines(logXs, logYs);
            static string logTickLabels(double y) => Math.Pow(10, y).ToString();    // "N0"
            WpfPlot1.Plot.XAxis.TickLabelFormat(logTickLabels);
            WpfPlot1.Plot.YAxis.TickLabelFormat(logTickLabels);
            WpfPlot1.Plot.XAxis.Label("Frequency (Hz)");
            WpfPlot1.Plot.YAxis.Label("Noise (nV/rHz)");
            WpfPlot1.Plot.XAxis.LabelStyle(fontSize: 18);
            WpfPlot1.Plot.XAxis.TickLabelStyle(fontSize: 14);
            WpfPlot1.Plot.YAxis.LabelStyle(fontSize: 18);
            WpfPlot1.Plot.YAxis.TickLabelStyle(fontSize: 14);
            WpfPlot1.Plot.Title("NSD estimation", bold: false, size: 18);
            WpfPlot1.Plot.YAxis.MinorLogScale(true, minorTickCount: 10);
            WpfPlot1.Plot.YAxis.MajorGrid(true, System.Drawing.Color.FromArgb(80, System.Drawing.Color.Black));
            WpfPlot1.Plot.YAxis.MinorGrid(true, System.Drawing.Color.FromArgb(20, System.Drawing.Color.Black));
            WpfPlot1.Plot.XAxis.MinorLogScale(true);
            WpfPlot1.Plot.XAxis.MajorGrid(true, System.Drawing.Color.FromArgb(80, System.Drawing.Color.Black));
            WpfPlot1.Plot.XAxis.MinorGrid(true, System.Drawing.Color.FromArgb(20, System.Drawing.Color.Black));
            WpfPlot1.Plot.SetAxisLimits(Math.Log10(viewModel.XMin), Math.Log10(viewModel.XMax), Math.Log10(viewModel.YMin), Math.Log10(viewModel.YMax));
            WpfPlot1.Render();
        }

        private void InitNsdChart()
        {
            //WpfPlot1.Configuration.DpiStretch = false;
            WpfPlot1.Configuration.Quality = ScottPlot.Control.QualityMode.High;
            WpfPlot1.Plot.Clear();
            static string logTickLabels(double y) => Math.Pow(10, y).ToString();    // "N0"
            WpfPlot1.Plot.XAxis.TickLabelFormat(logTickLabels);
            WpfPlot1.Plot.YAxis.TickLabelFormat(logTickLabels);
            WpfPlot1.Plot.XAxis.Label("Frequency (Hz)");
            WpfPlot1.Plot.YAxis.Label("Noise (nV/rHz)");
            WpfPlot1.Plot.XAxis.LabelStyle(fontSize: 18);
            WpfPlot1.Plot.XAxis.TickLabelStyle(fontSize: 14);
            WpfPlot1.Plot.YAxis.LabelStyle(fontSize: 18);
            WpfPlot1.Plot.YAxis.TickLabelStyle(fontSize: 14);
            WpfPlot1.Plot.Title("NSD estimation", bold: false, size: 18);
            WpfPlot1.Plot.YAxis.MinorLogScale(true, minorTickCount: 10);
            WpfPlot1.Plot.YAxis.MajorGrid(true, System.Drawing.Color.FromArgb(80, System.Drawing.Color.Black));
            WpfPlot1.Plot.YAxis.MinorGrid(true, System.Drawing.Color.FromArgb(20, System.Drawing.Color.Black));
            WpfPlot1.Plot.XAxis.MinorLogScale(true);
            WpfPlot1.Plot.XAxis.MajorGrid(true, System.Drawing.Color.FromArgb(80, System.Drawing.Color.Black));
            WpfPlot1.Plot.XAxis.MinorGrid(true, System.Drawing.Color.FromArgb(20, System.Drawing.Color.Black));
            WpfPlot1.Plot.SetAxisLimits(Math.Log10(viewModel.XMin), Math.Log10(viewModel.XMax), Math.Log10(viewModel.YMin), Math.Log10(viewModel.YMax));
            WpfPlot1.Render();
        }

        private async Task ShowError(string title, string message)
        {
            var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(title, message);
            await messageBoxStandardWindow.Show();
        }
    }
}
