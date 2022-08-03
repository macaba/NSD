using Avalonia.Controls;
using Avalonia.Interactivity;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NSD.UI
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel viewModel = new();
        private double[] processingX;
        private double[] processingY;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = viewModel;
            InitNsdChart();
        }

        public void btnRun_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Status = "Status: Running NSD...";
            var path = tbPath.Text;
            if (!File.Exists(path))
            {
                var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager
                    .GetMessageBoxStandardWindow("File not found", "Input CSV file not found");
                messageBoxStandardWindow.Show();
                return;
            }
                

            if (!double.TryParse(tbSampleRate.Text, out double sampleRate))
            {
                var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager
                    .GetMessageBoxStandardWindow("Invalid sample rate", "Invalid sample rate value");
                messageBoxStandardWindow.Show();
                return;
            }
            var fftWidth = int.Parse((string)((ComboBoxItem)cbFftWidth.SelectedItem).Content);

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<double>().ToList();
            if (records.Count == 0)
                throw new NsdProcessingException("No CSV records found");

            //var sine = Signals.OneVoltRmsTestSignal();

            //var nsd = MultiTaper.NSD_SingleSeries(input: records.ToArray(), sampleRate: 50, inputScale: 1E3, dF: 0.2, nw: 8);
            var nsd = Welch.NSD_SingleSeries(input: records.ToArray(), sampleRate, inputScale: 1e-3, outputWidth: fftWidth);
            //var nsd = Welch.NSD_SingleSeries(input: sine, sampleRate, inputScale: 1, outputWidth: fftWidth);

            int ignoreBins = 3;         //FTNI = 3, HFT90 = 3
            int length = (nsd.Length / 2) - ignoreBins * 2;     // Trim ignoreBins from either end of the real spectrum
            processingX = new double[length];

            double dT = (sampleRate / nsd.Length);
            for (int i = ignoreBins; i < length + ignoreBins; i++)
            {
                processingX[i - ignoreBins] = i * dT;
            }

            processingY = nsd.Slice(ignoreBins, length).ToArray();

            double[] yArray;
            if (cbFilter.IsChecked == true)
                yArray = new SavitzkyGolayFilter(5, 1).Process(processingY);
            else
                yArray = processingY;

            UpdateNSDChart(processingX, yArray, 1e9);
            viewModel.Status = "Status: NSD complete";
        }

        private void btnSetAxis_Click(object sender, RoutedEventArgs e)
        {
            WpfPlot1.Plot.SetAxisLimits(Math.Log10((double)dblXMin.Value), Math.Log10((double)dblXMax.Value), Math.Log10((double)dblYMin.Value), Math.Log10((double)dblYMax.Value));
            WpfPlot1.Render();
        }

        public void UpdateNSDChart(Memory<double> x, Memory<double> y, double yScaling = 1)
        {
            WpfPlot1.Configuration.DpiStretch = false;
            WpfPlot1.Configuration.Quality = ScottPlot.Control.QualityMode.High;
            WpfPlot1.Plot.Clear();
            double[] logXs = x.ToArray().Select(pt => Math.Log10(pt)).ToArray();
            double[] logYs = y.ToArray().Select(pt => Math.Log10(pt * yScaling)).ToArray();
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
            WpfPlot1.Plot.SetAxisLimits(Math.Log10((double)dblXMin.Value), Math.Log10((double)dblXMax.Value), Math.Log10((double)dblYMin.Value), Math.Log10((double)dblYMax.Value));
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
            WpfPlot1.Plot.SetAxisLimits(Math.Log10((double)dblXMin.Value), Math.Log10((double)dblXMax.Value), Math.Log10((double)dblYMin.Value), Math.Log10((double)dblYMax.Value));
            WpfPlot1.Render();
        }
    }
}
