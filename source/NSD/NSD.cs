using System.Data;
using System.Diagnostics;

namespace NSD
{
    public class NSD
    {
        public static Spectrum Linear(ReadOnlyMemory<double> input, double sampleRate, int outputWidth = 2048)
        {
            //var window = Windows.HFT90D(outputWidth, out double optimumOverlap, out double NENBW);
            var window = Windows.FTNI(outputWidth, out double optimumOverlap, out double NENBW);
            // Switched from HFT90D to FTNI
            // FTNI has the useful feature where Math.Ceiling(NENBW) is 1 less than most flaptop windows,
            // therefore showing one more usable frequency point at the low frequency end of the spectrum.
            var windowS2 = S2(window.Span);
            var fft = new FFT(outputWidth);
            int startIndex = 0;
            int endIndex = outputWidth;
            int overlap = (int)(outputWidth * (1.0 - optimumOverlap));
            int spectrumCount = 0;
            Memory<double> workBuffer = new double[outputWidth];
            Memory<double> workSpectrum = new double[outputWidth];
            Memory<double> lineFitOutput = new double[outputWidth];
            Memory<double> psdMemory = new double[outputWidth];
            while (endIndex < input.Length)
            {
                var lineFitInput = input.Slice(startIndex, outputWidth);
                SubtractLineFit(lineFitInput, lineFitOutput, workBuffer);
                fft.PSD(lineFitOutput, psdMemory, window, sampleRate, windowS2);
                AddPSDToWorkSpectrum(psdMemory, workSpectrum); spectrumCount++;
                startIndex += overlap;
                endIndex += overlap;
            }
            ConvertWorkSpectrumToAverageVSDInPlace(workSpectrum, spectrumCount);
            var nsd = Spectrum.FromValues(workSpectrum, sampleRate, spectrumCount);
            //nsd.TrimDC();     // Don't need to trim DC if trimming start/end
            nsd.TrimStartEnd((int)Math.Ceiling(NENBW));
            return nsd;
        }

        public static Spectrum StackedLinear(Memory<double> input, double sampleRate, int maxWidth = 2048, int minWidth = 64)
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
                spectrums[width] = Linear(input, sampleRate, width);
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

        public static Spectrum Log(Memory<double> input, double sampleRateHz, double freqMin, double freqMax, int pointsPerDecade, int minimumAverages)
        {
            if (freqMax <= freqMin)
                throw new ArgumentException("freqMax must be greater than freqMin");
            if (pointsPerDecade <= 0 || minimumAverages <= 0)
                throw new ArgumentException("pointsPerDecade, and minimumAverages must be positive");
            if (sampleRateHz <= 0)
                throw new ArgumentException("sampleRateHz must be positive");
            if (freqMin < sampleRateHz / input.Length)
                freqMin = sampleRateHz / input.Length;
            if (freqMax > sampleRateHz / 2)
                freqMax = sampleRateHz / 2;

            Windows.FTNI(1, out double optimumOverlap, out double NENBW);
            int firstUsableBinForWindow = (int)Math.Ceiling(NENBW);
            double decades = Math.Log10(freqMax / freqMin);
            int desiredNumberOfPoints = (int)(decades * pointsPerDecade) + 1;       // + 1 to get points on the decade grid lines

            double g = Math.Log(freqMax) - Math.Log(freqMin);
            double[] frequencies = Enumerable.Range(0, desiredNumberOfPoints).Select(j => freqMin * Math.Exp(j * g / (desiredNumberOfPoints - 1))).ToArray();
            double[] spectrumResolution = frequencies.Select(freq => freq / firstUsableBinForWindow).ToArray();
            // spectrumResolution contains the 'desired resolutions' for each frequency bin, given the rule that we want the first usuable bin in the flat-top'd data.

            int[] spectrumLength = spectrumResolution.Select(val => (int)Math.Round(sampleRateHz / val)).ToArray();     // Segment lengths
            //double[] actualSpectrumResolution = spectrumLength.Select(val => sampleRateHz / val).ToArray();             // Actual resolution
            //double[] binNumber = frequencies.Select((val, index) => val / actualSpectrumResolution[index]).ToArray();   // Fourier tranform bin number (maybe validate that it doesn't deviate beyond +/-10%?)
            int[] estimatedAverages = spectrumLength.Select(val => (int)((input.Length - val) / (val * (1.0 - optimumOverlap)))).ToArray();

            var spectrum = new Dictionary<double, double>();
            var indices = Enumerable.Range(0, desiredNumberOfPoints).ToArray();
            for(int i = 0; i < frequencies.Length; i++)
            {
                spectrum[frequencies[i]] = double.NaN;
            }
            object averageLock = new();
            int cumulativeAverage = 0;
            //for (int i = 0; i < desiredNumberOfPoints; i++)
            //foreach(var i in indices)
            Parallel.ForEach(indices, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                if (estimatedAverages[i] < minimumAverages)
                    return;
                var result = RunWelchGoertzel(input, spectrumLength[i], frequencies[i], sampleRateHz, out var actualAverages);
                if (estimatedAverages[i] != actualAverages)
                    Debug.WriteLine($"{estimatedAverages[i]} {actualAverages}");
                spectrum[frequencies[i]] = result;
                lock (averageLock)
                {
                    cumulativeAverage += actualAverages;
                }
            }
            );

            var output = new Spectrum
            {
                Frequencies = spectrum.Keys.ToArray(),
                Values = spectrum.Values.ToArray(),
                Averages = cumulativeAverage
            };
            return output;
        }

        private static void AddPSDToWorkSpectrum(Memory<double> inputPSD, Memory<double> workingMemory)
        {
            for (int i = 0; i < inputPSD.Length; i++)
            {
                workingMemory.Span[i] += inputPSD.Span[i];
            }
        }

        private static void ConvertWorkSpectrumToAverageVSDInPlace(Memory<double> workingMemory, int count)
        {
            double divisor = count;
            for (int i = 0; i < workingMemory.Length; i++)
            {
                workingMemory.Span[i] = workingMemory.Span[i] / divisor;
            }

            // Convert to VSD
            for (int i = 0; i < workingMemory.Length; i++)
            {
                workingMemory.Span[i] = Math.Sqrt(workingMemory.Span[i]);
            }
        }

        /// <summary>
        /// Least-Squares fitting the points (x,y) to a line y : x -> a+b*x, returning its best fitting parameters as (a, b) tuple, where a is the intercept and b the slope.
        /// </summary>
        private static (double A, double B) LineFit(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
        {
            if (x.Length != y.Length)
            {
                throw new ArgumentException($"All sample vectors must have the same length.");
            }

            if (x.Length <= 1)
            {
                throw new ArgumentException($"A regression of the requested order requires at least {2} samples. Only {x.Length} samples have been provided.");
            }

            // First Pass: Mean (Less robust but faster than ArrayStatistics.Mean)
            double mx = 0.0;
            double my = 0.0;
            for (int i = 0; i < x.Length; i++)
            {
                mx += x[i];
                my += y[i];
            }

            mx /= x.Length;
            my /= y.Length;

            // Second Pass: Covariance/Variance
            double covariance = 0.0;
            double variance = 0.0;
            for (int i = 0; i < x.Length; i++)
            {
                double diff = x[i] - mx;
                covariance += diff * (y[i] - my);
                variance += diff * diff;
            }

            var b = covariance / variance;
            return (my - b * mx, b);
        }

        /// <summary>
        /// Calculate Least-squares line fit and subtract from input, storing in output. Buffer is temporary variable memory.
        /// </summary>
        private static void SubtractLineFit(ReadOnlyMemory<double> input, Memory<double> output, Memory<double> buffer)
        {
            if (input.Length != output.Length || input.Length != buffer.Length)
                throw new ArgumentException("Lengths don't match");

            var x = buffer.Span;
            var y = input.Span;

            for (int i = 0; i < input.Length; i++)
                x[i] = i;

            var (A, B) = LineFit(x, y);
            var outputSpan = output.Span;
            var inputSpan = input.Span;
            for (int i = 0; i < input.Length; i++)
            {
                outputSpan[i] = (inputSpan[i] - (A + B * i));
            }
        }

        private static double RunWelchGoertzel(Memory<double> input, int runLength, double frequency, double sampleRateHz, out int spectrumCount2)
        {
            var window = Windows.FTNI(runLength, out double optimumOverlap, out double NENBW);
            double s2 = S2(window.Span);
            int startIndex = 0;
            int endIndex = runLength;
            int overlap = (int)(runLength * (1.0 - optimumOverlap));
            int spectrumCount = 0;
            double average = 0;
            Memory<double> waveformBuffer = new double[runLength];
            Memory<double> workBuffer = new double[runLength];

            while (endIndex < input.Length)
            {
                var lineFitInput = input.Slice(startIndex, runLength);
                SubtractLineFit(lineFitInput, waveformBuffer, workBuffer);
                for (int i = 0; i < runLength; i++)
                {
                    waveformBuffer.Span[i] = waveformBuffer.Span[i] * window.Span[i];
                }

                var filter = new GoertzelFilter(frequency, sampleRateHz);       // Specific form of 1 bin DFT
                var power = filter.Process(waveformBuffer.Span);
                average += 2.0 * Math.Pow(power.Magnitude, 2) / (sampleRateHz * s2);
                spectrumCount++;
                startIndex += overlap;
                endIndex += overlap;
            }

            spectrumCount2 = spectrumCount;
            return Math.Sqrt(average / spectrumCount);
        }

        private static double S1(ReadOnlySpan<double> window)
        {
            double sum = 0;
            for (int i = 0; i < window.Length; i++)
            {
                sum += window[i];
            }
            return sum;
        }

        private static double S2(ReadOnlySpan<double> window)
        {
            double sumSquared = 0;
            for (int i = 0; i < window.Length; i++)
            {
                sumSquared += Math.Pow(window[i], 2);
            }
            return sumSquared;
        }
    }
}

