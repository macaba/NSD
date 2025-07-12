using System.Data;

namespace NSD
{
    public class NSD
    {
        public static Spectrum Linear(ReadOnlyMemory<double> input, double sampleRate, int outputWidth = 2048)
        {
            //var window = Windows.HFT90D(outputWidth, out double optimumOverlap, out double NENBW);
            var window = Windows.FTNI(outputWidth, out double optimumOverlap, out double NENBW);
            //var window = Windows.HFT95(outputWidth, out double optimumOverlap, out double NENBW);
            // Switched from HFT90D to FTNI
            // FTNI has the useful feature where Math.Ceiling(NENBW) is 1 less than most flaptop windows,
            // therefore showing one more usable frequency point at the low frequency end of the spectrum.
            var windowS1 = S1(window.Span);
            var windowS2 = S2(window.Span);
            var fft = new FFT(outputWidth);
            int startIndex = 0;
            int endIndex = outputWidth;
            int overlap = (int)(outputWidth * (1.0 - optimumOverlap));
            int spectrumCount = 0;
            Memory<double> workBuffer = new double[outputWidth];
            Memory<double> sumSpectrum = new double[outputWidth];
            Memory<double> lineFitOutput = new double[outputWidth];
            Memory<double> powerSpectrum = new double[outputWidth];
            while (endIndex < input.Length)
            {
                var lineFitInput = input.Slice(startIndex, outputWidth);
                SubtractLineFit(lineFitInput, lineFitOutput, workBuffer);
                fft.PSD(lineFitOutput, powerSpectrum, window, sampleRate, windowS2);
                //fft.PS(lineFitOutput, powerSpectrum, window, windowS1);
                AddPowerSpectrumToSumSpectrum(powerSpectrum, sumSpectrum); spectrumCount++;
                startIndex += overlap;
                endIndex += overlap;
            }
            ConvertSumPowerSpectrumToAverageLinearInPlace(sumSpectrum, spectrumCount);
            var nsd = Spectrum.FromValues(sumSpectrum, sampleRate, spectrumCount);
            //nsd.TrimDC();     // Don't need to trim DC if trimming start/end
            nsd.TrimStart((int)Math.Ceiling(NENBW * 2));
            return nsd;
        }

        public static Spectrum DualLinear(Memory<double> input, double sampleRate, int maxWidth = 2048, int minWidth = 64)
        {
            // Compute all the possible widths between maxWidth & minWidth
            List<int> widths = [minWidth, maxWidth];

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

        private record WelchGoertzelJob(double Frequency, int SpectrumLength, int CalculatedAverages);
        public static Spectrum Log(Memory<double> input, double sampleRateHz, double freqMin, double freqMax, int pointsPerDecade, int minimumAverages, int minimumFourierLength, double pointsPerDecadeScaling)
        {
            if (freqMax <= freqMin)
                throw new ArgumentException("freqMax must be greater than freqMin");
            if (pointsPerDecade <= 0 || minimumAverages <= 0 || minimumFourierLength <= 0)
                throw new ArgumentException("pointsPerDecade, minimumAverages, and minimumFourierLength must be positive");
            if (sampleRateHz <= 0)
                throw new ArgumentException("sampleRateHz must be positive");

            Windows.FTNI(1, out double optimumOverlap, out double NENBW);
            int firstUsableBinForWindow = (int)Math.Ceiling(NENBW);

            // To do:
            // For the purposes of the frequencies calculation, round freqMax/freqMin to nearest major decade line.
            // This ensures consistency of X-coordinate over various view widths.
            double decadeMin = RoundToDecade(freqMin, RoundingMode.Down);
            double decadeMax = RoundToDecade(freqMax, RoundingMode.Up);
            int decadeMinExponent = (int)Math.Log10(decadeMin);
            int decadeMaxExponent = (int)Math.Log10(decadeMax);

            List<double> frequencyList = [];
            int pointsPerDecadeScaled = pointsPerDecade;
            for (int decadeExponent = decadeMinExponent; decadeExponent < decadeMaxExponent; decadeExponent++)
            {
                double currentDecadeMin = Math.Pow(10, decadeExponent);
                double currentDecadeMax = Math.Pow(10, decadeExponent + 1);
                double multiple = Math.Log(currentDecadeMax) - Math.Log(currentDecadeMin);
                var decadeFrequencies = Enumerable.Range(0, pointsPerDecadeScaled - 1).Select(i => currentDecadeMin * Math.Exp(i * multiple / (pointsPerDecadeScaled - 1))).ToArray();
                frequencyList.AddRange(decadeFrequencies);
                pointsPerDecadeScaled = (int)(pointsPerDecadeScaled * pointsPerDecadeScaling);
            }

            double g = Math.Log(decadeMax) - Math.Log(decadeMin);
            double[] frequencies = frequencyList.ToArray();
            double[] spectrumResolution = frequencies.Select(freq => freq / firstUsableBinForWindow).ToArray();
            // spectrumResolution contains the 'desired resolutions' for each frequency bin, respecting the rule that we want the first usuable bin for the given window.
            int[] spectrumLengths = spectrumResolution.Select(resolution => (int)Math.Round(sampleRateHz / resolution)).ToArray();

            // Create a job list of valid points to calculate
            double nyquistMax = sampleRateHz / 2;
            List<WelchGoertzelJob> jobs = [];
            for (int i = 0; i < frequencies.Length; i++)
            {
                if (frequencies[i] > nyquistMax)
                    continue;
                if (TryCalculateAverages(input.Length, spectrumLengths[i], optimumOverlap, out var averages))
                {
                    if (averages >= minimumAverages)
                    {
                        // Increase spectrum length until minimumLength is met, or averages drops below minimumAverages.
                        // This increases the spectral resolution at the top end of the chart, allowing 50Hz spikes (& similar) to be more visible
                        var spectrumLength = spectrumLengths[i];
                        bool continueLoop = true;
                        while (continueLoop)
                        {
                            if (spectrumLength < minimumFourierLength && averages > minimumAverages)
                            {
                                var success = TryCalculateAverages(input.Length, spectrumLength * 2, optimumOverlap, out var newAverages);
                                if (!success)
                                    break;
                                if (averages > minimumAverages)
                                {
                                    spectrumLength *= 2;
                                    averages = newAverages;
                                    continueLoop = true;
                                }
                                else
                                {
                                    continueLoop = false;
                                    break;
                                }
                            }
                            else
                            {
                                continueLoop = false;
                            }
                        }
                        jobs.Add(new WelchGoertzelJob(frequencies[i], spectrumLength, averages));
                    }
                }
            }

            var spectrum = new Dictionary<double, double>();
            for (int i = 0; i < jobs.Count; i++)
            {
                spectrum[jobs[i].Frequency] = double.NaN;
            }
            object averageLock = new();
            int cumulativeAverage = 0;
            //foreach(var job in jobs)
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, job =>
            {
                var result = RunWelchGoertzel(input, job.SpectrumLength, job.Frequency, sampleRateHz, out var actualAverages);
                if (job.CalculatedAverages != actualAverages)
                    throw new Exception("Actual averages does not match calculated averages");
                spectrum[job.Frequency] = result;
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

        private static void AddPowerSpectrumToSumSpectrum(Memory<double> input, Memory<double> workingMemory)
        {
            for (int i = 0; i < input.Length; i++)
            {
                workingMemory.Span[i] += input.Span[i];
            }
        }

        private static void ConvertSumPowerSpectrumToAverageLinearInPlace(Memory<double> workingMemory, int count)
        {
            double divisor = count;
            for (int i = 0; i < workingMemory.Length; i++)
            {
                workingMemory.Span[i] = workingMemory.Span[i] / divisor;
            }

            // Convert to LSD
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

        enum RoundingMode { Nearest, Up, Down }
        private static double RoundToDecade(double value, RoundingMode mode)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Value must be positive.");

            double log10 = Math.Log10(value);
            double exponent = mode switch
            {
                RoundingMode.Nearest => Math.Round(log10),
                RoundingMode.Up => Math.Ceiling(log10),
                RoundingMode.Down => Math.Floor(log10),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), "Invalid rounding mode.")
            };

            return Math.Pow(10, exponent);
        }

        private static bool TryCalculateAverages(int dataLength, int spectrumLength, double optimumOverlap, out int averages)
        {
            averages = 0;
            int overlap = (int)(spectrumLength * (1.0 - optimumOverlap));
            if (overlap < 1)
                return false;
            int endIndex = spectrumLength;
            while (endIndex < dataLength)
            {
                averages++;
                endIndex += overlap;
            }
            return true;
        }
    }
}

