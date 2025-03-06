using Avalonia.Controls;
using Avalonia.Interactivity;
using CsvHelper.Configuration;
using MsBox.Avalonia;
using ScottPlot.TickGenerators;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NSD.UI
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel viewModel;
        private Spectrum spectrum = new();
        private Settings settings;

        public MainWindow()
        {
            InitializeComponent();
            settings = Settings.Load();
            viewModel = new(settings, this);
            DataContext = viewModel;
            InitNsdChart();
        }

        public async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(viewModel.ProcessWorkingFolder))
            {
                var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandard("Folder not found", "Search folder not found");
                await messageBoxStandardWindow.ShowAsync();
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

        public async void btnRun_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Enabled = false;
            try
            {
                var path = viewModel.GetSelectedInputFilePath();
                if (!File.Exists(path))
                {
                    viewModel.Status = "Error: Input CSV file not found";
                    return;
                }
                if (!double.TryParse(viewModel.AcquisitionTime, out double acquisitionTime))
                {
                    viewModel.Status = "Error: Invalid acquisition time value";
                    return;
                }
                double acquisitionTimeSeconds = (string)(viewModel.SelectedAcquisitionTimebaseItem).Content switch
                {
                    "NPLC (50Hz)" => acquisitionTime * (1.0 / 50.0),
                    "NPLC (60Hz)" => acquisitionTime * (1.0 / 60.0),
                    "s" => acquisitionTime,
                    "ms" => acquisitionTime / 1e3,
                    "μs" => acquisitionTime / 1e6,
                    "ns" => acquisitionTime / 1e9,
                    "SPS" => 1.0 / acquisitionTime,
                    "kSPS" => 1.0 / (acquisitionTime * 1000.0),
                    "MSPS" => 1.0 / (acquisitionTime * 1000000.0),
                    _ => throw new ApplicationException("Acquisition time combobox value not handled")
                };
                //if (!double.TryParse(viewModel.DataRate, out double dataRateTime))
                //{
                //    viewModel.Status = "Error: Invalid data rate value";
                //    return;
                //}
                //double dataRateTimeSeconds = (string)viewModel.SelectedDataRateUnitItem.Content switch
                //{
                //    "Samples per second" => 1.0 / dataRateTime,
                //    "Seconds per sample" => dataRateTime,
                //    _ => throw new ApplicationException("Data rate combobox value not handled")
                //};

                switch ((string)viewModel.SelectedNsdAlgorithm.Content)
                {
                    case "Logarithmic":
                        break;
                    case "Linear":
                        break;
                    case "Linear stacking":
                        {
                            var fftWidth = int.Parse((string)viewModel.SelectedLinearStackingLengthItem.Content);
                            var stackingFftWidth = int.Parse((string)viewModel.SelectedLinearStackingMinLengthItem.Content);
                            if (stackingFftWidth >= fftWidth)
                            {
                                viewModel.Status = "Error: Invalid minimum stacking FFT width";
                                return;
                            }
                            break;
                        }
                }

                if (!double.TryParse(viewModel.InputScaling, out double inputScaling))
                {
                    viewModel.Status = "Error: Invalid input scaling value";
                    return;
                }

                viewModel.Status = "Status: Loading CSV...";

                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                //using var reader = new StreamReader(stream);
                //using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                //var records = await csv.GetRecordsAsync<double>().ToListAsync();
                List<double> records = new();
                DateTimeOffset fileParseStart = DateTimeOffset.UtcNow;
                await Task.Run(() =>
                {
                    using var streamReader = new StreamReader(stream);
                    var csvReader = new NReco.Csv.CsvReader(streamReader, ",");
                    if (viewModel.CsvHasHeader)
                        csvReader.Read();
                    int columnIndex = viewModel.CsvColumnIndex;
                    while (csvReader.Read())
                    {
                        var number = double.Parse(csvReader[columnIndex]);
                        if (number > 1e12)      // Catches the overrange samples from DMM6500
                            continue;
                        records.Add(number);
                    }
                });
                DateTimeOffset fileParseFinish = DateTimeOffset.UtcNow;
                if (records.Count == 0)
                {
                    viewModel.Status = "Error: No CSV records found";
                    return;
                }

                switch ((string)viewModel.SelectedNsdAlgorithm.Content)
                {
                    case "Logarithmic":
                        break;
                    case "Linear":
                        {
                            var fftWidth = int.Parse((string)viewModel.SelectedLinearLengthItem.Content);
                            if (fftWidth > records.Count)
                            {
                                viewModel.Status = "Error: FFT width is longer than input data";
                                return;
                            }
                            break;
                        }
                    case "Linear stacking":
                        {
                            var fftWidth = int.Parse((string)viewModel.SelectedLinearStackingLengthItem.Content);
                            if (fftWidth > records.Count)
                            {
                                viewModel.Status = "Error: FFT width is longer than input data";
                                return;
                            }
                            break;
                        }
                }


                //records = Signals.WhiteNoise(100000, sampleRate, 1e-9).ToArray().ToList();

                viewModel.Status = "Status: Calculating NSD...";

                for (int i = 0; i < records.Count; i++)
                {
                    records[i] *= inputScaling;
                }

                DateTimeOffset nsdComputeStart = DateTimeOffset.UtcNow;
                //double spectralValueCorrection = Math.Sqrt(dataRateTimeSeconds / acquisitionTimeSeconds);
                //double spectralValueCorrection = 1.0;
                //double frequencyBinCorrection = Math.Sqrt(dataRateTimeSeconds / acquisitionTimeSeconds);
                //double frequencyBinCorrection = 1.0;
                switch ((string)viewModel.SelectedNsdAlgorithm.Content)
                {
                    case "Logarithmic":
                        {
                            var pointsPerDecade = int.Parse(viewModel.LogNsdPointsDecade);
                            var pointsPerDecadeScaling = double.Parse(viewModel.LogNsdPointsDecadeScaling);
                            var minAverages = int.Parse(viewModel.LogNsdMinAverages);
                            //var minLength = int.Parse(viewModel.LogNsdMinLength);
                            var minLength = int.Parse((string)viewModel.SelectedLogNsdMinLength.Content);
                            var nsd = await Task.Factory.StartNew(() => NSD.Log(
                                input: records.ToArray(),
                                sampleRateHz: 1.0 / acquisitionTimeSeconds,
                                freqMin: viewModel.XMin,
                                freqMax: viewModel.XMax,
                                pointsPerDecade,
                                minAverages,
                                minLength,
                                pointsPerDecadeScaling));
                            spectrum = nsd;
                            break;
                        }
                    case "Linear":
                        {
                            var fftWidth = int.Parse((string)viewModel.SelectedLinearLengthItem.Content);
                            var nsd = await Task.Factory.StartNew(() => NSD.Linear(input: records.ToArray(), 1.0 / acquisitionTimeSeconds, outputWidth: fftWidth));
                            spectrum = nsd;
                            break;
                        }
                    case "Linear stacking":
                        {
                            var fftMaxWidth = int.Parse((string)viewModel.SelectedLinearStackingLengthItem.Content);
                            var fftMinWidth = int.Parse((string)viewModel.SelectedLinearStackingMinLengthItem.Content);
                            var nsd = await Task.Factory.StartNew(() => NSD.StackedLinear(input: records.ToArray(), 1.0 / acquisitionTimeSeconds, maxWidth: fftMaxWidth, minWidth: fftMinWidth));
                            spectrum = nsd;
                            break;
                        }
                }
                DateTimeOffset nsdComputeFinish = DateTimeOffset.UtcNow;
                Memory<double> yArray;
                if (viewModel.SgFilterChecked)
                    yArray = new SavitzkyGolayFilter(5, 1).Process(spectrum.Values.Span);
                else
                    yArray = spectrum.Values;

                //for (int i = 0; i < yArray.Length; i++)
                //{
                //    yArray.Span[i] /= spectralValueCorrection;
                //}

                //for (int i = 0; i < spectrum.Frequencies.Length; i++)
                //{
                //    spectrum.Frequencies.Span[i] *= frequencyBinCorrection;
                //}

                UpdateNSDChart(spectrum.Frequencies, yArray);
                var fileParseTimeSec = fileParseFinish.Subtract(fileParseStart).TotalSeconds;
                var nsdComputeTimeSec = nsdComputeFinish.Subtract(nsdComputeStart).TotalSeconds;
                if (spectrum.Stacking > 1)
                    viewModel.Status = $"Status: Processing complete, {records.Count} input points, averaged {spectrum.Averages} spectrums over {spectrum.Stacking} stacking FFT widths. File parse time: {fileParseTimeSec:F3}s, NSD compute time: {nsdComputeTimeSec:F3}s.";
                else
                    viewModel.Status = $"Status: Processing complete, {records.Count} input points, averaged {spectrum.Averages} spectrums. File parse time: {fileParseTimeSec:F3}s, NSD compute time: {nsdComputeTimeSec:F3}s.";
            }
            catch (Exception ex)
            {
                await ShowError("Exception", ex.Message);
            }
            finally
            {
                viewModel.Enabled = true;
            }
        }

        public class NsdSample
        {
            public double Frequency { get; set; }
            public double Noise { get; set; }
        }

        public async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var outputFilePath = Path.Combine(viewModel.ProcessWorkingFolder, viewModel.OutputFileName);

            CsvConfiguration config = new(CultureInfo.InvariantCulture);
            config.Delimiter = ",";
            using var writer = new StreamWriter(outputFilePath);
            using var csvWriter = new CsvHelper.CsvWriter(writer, config);
            csvWriter.WriteHeader<NsdSample>();
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
            if (spectrum != null)
            {
                Memory<double> yArray;
                if (viewModel.SgFilterChecked)
                    yArray = new SavitzkyGolayFilter(5, 1).Process(spectrum.Values.Span);
                else
                    yArray = spectrum.Values;
                UpdateNSDChart(spectrum.Frequencies, yArray);
            }
            SetChartLimitsAndRefresh();
        }

        public void UpdateNSDChart(Memory<double> x, Memory<double> y)
        {
            WpfPlot1.Plot.Clear();
            double[] logXs = x.ToArray().Select(pt => Math.Log10(pt)).ToArray();
            double[] logYs = y.ToArray().Select(pt => Math.Log10(pt * 1E9)).ToArray();
            var scatter = WpfPlot1.Plot.Add.ScatterLine(logXs, logYs);
            //var scatter = WpfPlot1.Plot.Add.Scatter(logXs, logYs);
            if (viewModel.MarkersChecked)
            {
                scatter.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledCircle;
                scatter.MarkerStyle.Size = 3;
                scatter.MarkerStyle.IsVisible = true;
            }
            CommonChartConfig();
        }

        private void InitNsdChart()
        {
            WpfPlot1.Plot.Clear();
            CommonChartConfig();
        }

        private void CommonChartConfig()
        {
            static string logTickLabels(double y) => Math.Pow(10, y).ToString();    // "N0"
            NumericAutomatic xTickGenerator = new()
            {
                LabelFormatter = logTickLabels,
                MinorTickGenerator = new LogMinorTickGenerator() { Divisions = 10 },
                IntegerTicksOnly = true,
                TargetTickCount = 10
            };
            NumericAutomatic yTickGenerator = new()
            {
                LabelFormatter = logTickLabels,
                MinorTickGenerator = new LogMinorTickGenerator() { Divisions = 10 },
                IntegerTicksOnly = true,
                TargetTickCount = 10
            };
            WpfPlot1.Plot.Axes.Bottom.TickGenerator = xTickGenerator;
            WpfPlot1.Plot.Axes.Left.TickGenerator = yTickGenerator;
            WpfPlot1.Plot.Axes.Hairline(true);
            WpfPlot1.Plot.XLabel("Frequency (Hz)", 14);
            WpfPlot1.Plot.YLabel("Noise (nV/rHz)", 14);
            WpfPlot1.Plot.Axes.Bottom.Label.Bold = false;
            WpfPlot1.Plot.Axes.Left.Label.Bold = false;
            WpfPlot1.Plot.Title("NSD estimation", size: 14);
            WpfPlot1.Plot.Axes.Title.Label.Bold = false;
            WpfPlot1.Plot.Grid.MinorLineWidth = 1;
            WpfPlot1.Plot.Grid.MinorLineColor = ScottPlot.Color.FromARGB(0x14000000);
            WpfPlot1.Plot.Grid.MajorLineColor = ScottPlot.Color.FromARGB(0x50000000);
            SetChartLimitsAndRefresh();
        }

        private void SetChartLimitsAndRefresh()
        {
            var top = Math.Log10(viewModel.YMax + (viewModel.YMax * 0.001));
            var bottom = Math.Log10(viewModel.YMin - (viewModel.YMin * 0.001));
            WpfPlot1.Plot.Axes.SetLimits(Math.Log10(viewModel.XMin), Math.Log10(viewModel.XMax), bottom, top);
            WpfPlot1.Refresh();
        }

        private async Task ShowError(string title, string message)
        {
            var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandard(title, message);
            await messageBoxStandardWindow.ShowAsync();
        }
    }
}
