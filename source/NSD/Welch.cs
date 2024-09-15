namespace NSD
{
    public class Welch
    {
        public static Spectrum NSD(ReadOnlyMemory<double> input, double sampleRate, int startEndTrim, int outputWidth = 2048)
        {
            var window = Windows.HFT90D(outputWidth, out double optimumOverlap);
            var fft = new FFT(outputWidth);
            int startIndex = 0;
            int endIndex = outputWidth;
            int overlap = (int)(outputWidth * (1.0 - optimumOverlap));
            int spectrumCount = 0;
            Memory<double> workingMemory = new double[outputWidth];
            Memory<double> lineFitOutput = new double[outputWidth];
            Memory<double> psdMemory = new double[outputWidth];
            while (endIndex < input.Length)
            {
                var lineFitInput = input.Slice(startIndex, outputWidth);
                fft.SubtractLineFitWithScaling(lineFitInput, lineFitOutput);
                fft.PSD(lineFitOutput, psdMemory, window, sampleRate);
                //spectrums.Add(psd);
                FFT.AddPSDToWorkingMemory(psdMemory, workingMemory); spectrumCount++;
                startIndex += overlap;
                endIndex += overlap;
            }

            //Memory<double> vsd = new double[outputWidth];
            //FFT.AverageVSDFromPSDCollection(spectrums, vsd);
            FFT.ConvertWorkingMemoryToAverageVSDInPlace(workingMemory, spectrumCount);
            var nsd = Spectrum.FromValues(workingMemory, sampleRate, spectrumCount);
            nsd.TrimStartEnd(startEndTrim);
            return nsd;
        }

        public static Task<Spectrum> NSD_Async(Memory<double> input, double sampleRate, int startEndTrim, int outputWidth = 2048)
        {
            return Task.Factory.StartNew(() => NSD(input, sampleRate, startEndTrim, outputWidth));
        }

        public static Spectrum StackedNSD(Memory<double> input, double sampleRate, int startEndTrim, int outputWidth = 2048, int minWidth = 64)
        {
            List<int> widths = new();
            Dictionary<int, int> startEndTrims = new();

            widths.Add(outputWidth);
            startEndTrims[outputWidth] = startEndTrim;
            
            int width = outputWidth;
            while (width > minWidth)
            {
                width /= 2;
                widths.Add(width);
                if (width == 64)
                    startEndTrims[width] = startEndTrim;
                else
                    startEndTrims[width] = startEndTrim * 2;    //Trim a bit more for the shorter widths
            }

            widths.Reverse();      // Smallest to largest
            startEndTrims.Reverse();

            //Run parallel NSDs
            var spectrums = new Dictionary<int, Spectrum>();
            Parallel.ForEach(widths, new ParallelOptions { MaxDegreeOfParallelism = 8 }, width =>
            {
                spectrums[width] = NSD(input, sampleRate, startEndTrims[width], width);
            });

            double lowestFrequency = double.MaxValue;
            List<double> outputFrequencies = new();
            List<double> outputValues = new();
            int averages = 0;
            foreach(var computedWidth in widths)
            {
                var nsd = spectrums[computedWidth];
                averages += nsd.Averages;
                for (int i = nsd.Frequencies.Length - 1; i >= 0; i--)
                {
                    if (nsd.Frequencies.Span[i] < lowestFrequency)
                    {
                        lowestFrequency = nsd.Frequencies.Span[i];
                        outputFrequencies.Add(nsd.Frequencies.Span[i]);
                        outputValues.Add(nsd.Values.Span[i]);
                    }
                }
            }
            outputFrequencies.Reverse();
            outputValues.Reverse();
            return new Spectrum() { Frequencies = outputFrequencies.ToArray(), Values = outputValues.ToArray(), Averages = averages, Stacking = widths.Count };
        }

        public static Task<Spectrum> StackedNSD_Async(Memory<double> input, double sampleRate, int startEndTrim, int outputWidth = 2048, int minWidth = 64)
        {
            return Task.Factory.StartNew(() => StackedNSD(input, sampleRate, startEndTrim, outputWidth, minWidth));
        }
    }
}
