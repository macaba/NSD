namespace NSD
{
    public class Welch
    {
        public static Spectrum NSD(ReadOnlyMemory<double> input, double sampleRate, int outputWidth = 2048)
        {
            var window = Windows.HFT90D(outputWidth, out double optimumOverlap, out double NENBW);
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
                FFT.AddPSDToWorkingMemory(psdMemory, workingMemory); spectrumCount++;
                startIndex += overlap;
                endIndex += overlap;
            }
            FFT.ConvertWorkingMemoryToAverageVSDInPlace(workingMemory, spectrumCount);
            var nsd = Spectrum.FromValues(workingMemory, sampleRate, spectrumCount);
            nsd.TrimDC();
            nsd.TrimStartEnd((int)Math.Ceiling(NENBW));
            return nsd;
        }

        public static Spectrum StackedNSD(Memory<double> input, double sampleRate, int maxWidth = 2048, int minWidth = 64)
        {
            // Compute all the possible widths between maxWidth & minWidth
            List<int> widths = [maxWidth];
            int width = maxWidth;
            while (width > minWidth)
            {
                width /= 2;
                widths.Add(width);
            }
            // Order by smallest to largest
            widths.Reverse();      

            // Run parallel NSDs
            var spectrums = new Dictionary<int, Spectrum>();
            Parallel.ForEach(widths, new ParallelOptions { MaxDegreeOfParallelism = 8 }, width =>
            {
                spectrums[width] = NSD(input, sampleRate, width);
            });

            // Combine all the NSDs into one
            double lowestFrequency = double.MaxValue;
            var outputFrequencies = new List<double>();
            var outputValues = new List<double>();
            int averages = 0;
            foreach (var computedWidth in widths)
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

            // Order by frequencies smallest to largest
            outputFrequencies.Reverse();
            outputValues.Reverse();
            return new Spectrum() { Frequencies = outputFrequencies.ToArray(), Values = outputValues.ToArray(), Averages = averages, Stacking = widths.Count };
        }
    }
}
