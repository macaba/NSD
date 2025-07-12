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
using System.Runtime.InteropServices;
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

            viewModel.InputFilePaths.Clear();
            viewModel.InputFileNames.Clear();
            var files = Directory.EnumerateFiles(viewModel.ProcessWorkingFolder, "*.csv");
            foreach (var file in files)
            {
                viewModel.InputFilePaths.Add(file);
                viewModel.InputFileNames.Add(Path.GetFileName(file));
            }
            files = Directory.EnumerateFiles(viewModel.ProcessWorkingFolder, "*.f32");      // Hidden functionality that supports F32 bin files
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
                    case "Linear dual":
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
                List<double> records = [];
                DateTimeOffset fileParseStart = DateTimeOffset.UtcNow;
                DateTimeOffset fileParseFinish = DateTimeOffset.UtcNow;

                switch (Path.GetExtension(path))
                {
                    case ".csv":
                        {
                            //using var reader = new StreamReader(stream);
                            //using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                            //var records = await csv.GetRecordsAsync<double>().ToListAsync();

                            fileParseStart = DateTimeOffset.UtcNow;
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
                            fileParseFinish = DateTimeOffset.UtcNow;
                            break;
                        }
                    case ".f32":
                        {
                            fileParseStart = DateTimeOffset.UtcNow;
                            await Task.Run(() =>
                            {
                                const int ChunkSizeBytes = 8 * 1024 * 1024; // 8 MiB
                                byte[] buffer = new byte[ChunkSizeBytes];
                                int bytesRead;

                                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    // Only process full float32 values (4 bytes each)
                                    int validBytes = bytesRead - (bytesRead % 4);
                                    if (validBytes == 0)
                                        continue;

                                    ReadOnlySpan<byte> byteSpan = buffer.AsSpan(0, validBytes);
                                    ReadOnlySpan<float> floatSpan = MemoryMarshal.Cast<byte, float>(byteSpan);

                                    foreach (var f32 in floatSpan)
                                    {
                                        records.Add(f32);
                                    }
                                }
                            });
                            fileParseFinish = DateTimeOffset.UtcNow;
                            break;
                        }
                }

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
                    case "Linear dual":
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
                                freqMin: ParseWithSIPrefix(viewModel.XMin),
                                freqMax: ParseWithSIPrefix(viewModel.XMax),
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
                    case "Linear dual":
                        {
                            var fftMaxWidth = int.Parse((string)viewModel.SelectedLinearStackingLengthItem.Content);
                            var fftMinWidth = int.Parse((string)viewModel.SelectedLinearStackingMinLengthItem.Content);
                            var nsd = await Task.Factory.StartNew(() => NSD.DualLinear(input: records.ToArray(), 1.0 / acquisitionTimeSeconds, maxWidth: fftMaxWidth, minWidth: fftMinWidth));
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
            double[] logYs = y.ToArray().Select(pt => Math.Log10(pt)).ToArray();
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

        private double ParseWithSIPrefix(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or empty.");

            input = input.Trim();

            var siMultipliers = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["p"] = 1e-12,
                ["n"] = 1e-9,
                ["u"] = 1e-6,
                ["m"] = 1e-3,
                [""] = 1,
                ["k"] = 1e3,
                ["M"] = 1e6,
                ["G"] = 1e9,
                ["T"] = 1e12
            };

            // Separate number part and suffix
            int i = input.Length - 1;

            // Scan backward to find the first character that's not part of the suffix
            while (i >= 0 && (char.IsLetter(input[i]) || input[i] == 'µ')) i--;

            string numberPart = input.Substring(0, i + 1);
            string suffix = input.Substring(i + 1);

            // Handle alternate micro symbol
            if (suffix == "µ") suffix = "u";

            if (!double.TryParse(numberPart, out double number))
                throw new FormatException($"Invalid number format in '{input}'.");

            if (!siMultipliers.TryGetValue(suffix, out double multiplier))
                throw new FormatException($"Unknown SI suffix '{suffix}' in '{input}'.");

            return number * multiplier;
        }

        private string logTickLabels(double y)
        {
            double value = Math.Pow(10, y);

            // Define SI prefixes
            var siPrefixes = new (double threshold, string suffix)[]
            {
        (1e12, "T"),
        (1e9,  "G"),
        (1e6,  "M"),
        (1e3,  "k"),
        (1,    ""),
        (1e-3, "m"),
        (1e-6, "μ"),
        (1e-9, "n"),
        (1e-12,"p"),
        (1e-15,"f"),
            };

            foreach (var (threshold, suffix) in siPrefixes)
            {
                if (value >= threshold)
                {
                    return (value / threshold).ToString("G4") + suffix;
                }
            }

            return value.ToString("G4");  // fallback for very small values
        }

        private void CommonChartConfig()
        {
            //static string logTickLabels(double y) => Math.Pow(10, y).ToString();    // "N0"
            NumericAutomatic xTickGenerator = new()
            {
                LabelFormatter = logTickLabels,
                MinorTickGenerator = new LogDecadeMinorTickGenerator() { TicksPerDecade = 10 },
                IntegerTicksOnly = true,
                TargetTickCount = 10
            };
            NumericAutomatic yTickGenerator = new()
            {
                LabelFormatter = logTickLabels,
                MinorTickGenerator = new LogDecadeMinorTickGenerator() { TicksPerDecade = 10 },
                IntegerTicksOnly = true,
                TargetTickCount = 10
            };
            WpfPlot1.Plot.Axes.Bottom.TickGenerator = xTickGenerator;
            WpfPlot1.Plot.Axes.Left.TickGenerator = yTickGenerator;
            WpfPlot1.Plot.Axes.Hairline(true);
            WpfPlot1.Plot.XLabel("Frequency (Hz)", 14);
            WpfPlot1.Plot.YLabel("Noise (V/rHz)", 14);
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
            double fudgeFactor = 0.001;
            double xMin = ParseWithSIPrefix(viewModel.XMin);
            double xMax = ParseWithSIPrefix(viewModel.XMax);
            double yMin = ParseWithSIPrefix(viewModel.YMin);
            double yMax = ParseWithSIPrefix(viewModel.YMax);
            var left = Math.Log10(xMin - (xMin * fudgeFactor));
            var right = Math.Log10(xMax + (xMax * fudgeFactor));
            var bottom = Math.Log10(yMin - (yMin * fudgeFactor));
            var top = Math.Log10(yMax + (yMax * fudgeFactor));
            WpfPlot1.Plot.Axes.SetLimits(left, right, bottom, top);
            WpfPlot1.Refresh();
        }

        private async Task ShowError(string title, string message)
        {
            var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandard(title, message);
            await messageBoxStandardWindow.ShowAsync();
        }
    }
}
