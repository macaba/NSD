using Avalonia.Controls;
using Avalonia.Interactivity;
using CsvHelper.Configuration;
using MsBox.Avalonia;
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
        private Spectrum spectrum = new();
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
                    _ => throw new ApplicationException("Acquisition time combobox value not handled")
                };
                if (!double.TryParse(viewModel.DataRate, out double dataRateTime))
                {
                    viewModel.Status = "Error: Invalid data rate value";
                    return;
                }
                double dataRateTimeSeconds = (string)viewModel.SelectedDataRateUnitItem.Content switch
                {
                    "Samples per second" => 1.0 / dataRateTime,
                    "Seconds per sample" => dataRateTime,
                    _ => throw new ApplicationException("Data rate combobox value not handled")
                };
                var fftWidth = int.Parse((string)viewModel.SelectedFftWidthItem.Content);
                var stackingFftWidth = int.Parse((string)viewModel.SelectedStackingFftWidthItem.Content);
                if (stackingFftWidth >= fftWidth && viewModel.FftStacking)
                {
                    viewModel.Status = "Error: Invalid minimum stacking FFT width";
                    return;
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

                if (records.Count == 0)
                {
                    viewModel.Status = "Error: No CSV records found";
                    return;
                }
                if (fftWidth > records.Count)
                {
                    viewModel.Status = "Error: FFT width is longer than input data";
                    return;
                }

                //records = Signals.WhiteNoise(100000, sampleRate, 1e-9).ToArray().ToList();

                viewModel.Status = "Status: Calculating NSD...";

                for (int i = 0; i < records.Count; i++)
                {
                    records[i] *= inputScaling;
                }

                //double spectralValueCorrection = Math.Sqrt(dataRateTimeSeconds / acquisitionTimeSeconds);
                double spectralValueCorrection = 1.0;
                //double frequencyBinCorrection = Math.Sqrt(dataRateTimeSeconds / acquisitionTimeSeconds);
                double frequencyBinCorrection = 1.0;
                // Trim ignoreBins from either end of the real spectrum
                int ignoreBins = (int)(4 * spectralValueCorrection);         //FTNI = 4, HFT90 = 4
                if (viewModel.FftStacking)
                {
                    var nsd = await Welch.StackedNSD_Async(input: records.ToArray(), 1.0 / acquisitionTimeSeconds, ignoreBins, outputWidth: fftWidth, minWidth: stackingFftWidth);
                    spectrum = nsd;
                }
                else
                {
                    //var sine = Signals.OneVoltRmsTestSignal();
                    //await Welch.StackedNSD_Async(input: records.ToArray(), sampleRate, inputScale: 1e-3, outputWidth: fftWidth);
                    //var nsd = Welch.NSD_SingleSeries(input: sine, sampleRate, inputScale: 1, outputWidth: fftWidth);
                    var nsd = await Welch.NSD_Async(input: records.ToArray(), 1.0 / acquisitionTimeSeconds, ignoreBins, outputWidth: fftWidth);
                    spectrum = nsd;
                }

                Memory<double> yArray;
                if (viewModel.SgFilterChecked)
                    yArray = new SavitzkyGolayFilter(5, 1).Process(spectrum.Values.Span);
                else
                    yArray = spectrum.Values;


                for (int i = 0; i < yArray.Length; i++)
                {
                    yArray.Span[i] /= spectralValueCorrection;
                }


                for (int i = 0; i < spectrum.Frequencies.Length; i++)
                {
                    spectrum.Frequencies.Span[i] *= frequencyBinCorrection;
                }

                UpdateNSDChart(spectrum.Frequencies, yArray);
                if (spectrum.Stacking > 1)
                    viewModel.Status = $"Status: Processing complete, {records.Count} input points, averaged {spectrum.Averages} spectrums over {spectrum.Stacking} stacking FFT widths";
                else
                    viewModel.Status = $"Status: Processing complete, {records.Count} input points, averaged {spectrum.Averages} spectrums";
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

        public void btnRateToTime_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(viewModel.DataRate, out double dataRateTime))
            {
                viewModel.Status = "Error: Invalid data rate value";
                return;
            }
            double dataRateTimeSeconds = (string)viewModel.SelectedDataRateUnitItem.Content switch
            {
                "Samples per second" => 1.0 / dataRateTime,
                "Seconds per sample" => dataRateTime,
                _ => throw new ApplicationException("Data rate combobox value not handled")
            };

            viewModel.AcquisitionTime = dataRateTimeSeconds.ToString();
            cbTime.SelectedIndex = 2;
        }

        public void btnTimeToRate_Click(object sender, RoutedEventArgs e)
        {
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
                _ => throw new ApplicationException("Acquisition time combobox value not handled")
            };
            viewModel.DataRate = (1.0 / acquisitionTimeSeconds).ToString();
            cbRate.SelectedIndex = 0;
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
            var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandard(title, message);
            await messageBoxStandardWindow.ShowAsync();
        }
    }
}
