using System;

namespace NSD
{
    public class Welch
    {
        public static Spectrum NSD(Memory<double> input, double sampleRate, int startEndTrim, int outputWidth = 2048)
        {
            var window = Windows.HFT90D(outputWidth, out double optimumOverlap);
            int startIndex = 0;
            int endIndex = outputWidth;
            int overlap = (int)(outputWidth * (1.0 - optimumOverlap));
            List<Memory<double>> spectrums = new();
            while (endIndex < input.Length)
            {
                var newSlice = input.Slice(startIndex, outputWidth);
                var data = FFT.SubtractLineFitWithScaling(newSlice);
                Memory<double> psd = new double[outputWidth];
                FFT.PSD(data, psd, window, sampleRate);
                spectrums.Add(psd);
                startIndex += overlap;
                endIndex += overlap;
            }

            Memory<double> vsd = new double[outputWidth];
            FFT.AverageVSDFromPSDCollection(spectrums, vsd);
            var nsd = Spectrum.FromValues(vsd, sampleRate, spectrums.Count);
            nsd.TrimStartEnd(startEndTrim);
            return nsd;
        }

        public static Task<Spectrum> NSD_Async(Memory<double> input, double sampleRate, int startEndTrim, int outputWidth = 2048)
        {
            return Task.Factory.StartNew(() => NSD(input, sampleRate, startEndTrim, outputWidth));
        }

        public static Spectrum StackedNSD(Memory<double> input, double sampleRate, int startEndTrim, int outputWidth = 2048)
        {
            List<int> widths = new();
            List<int> startEndTrims = new();
            widths.Add(outputWidth);
            startEndTrims.Add(startEndTrim);
            int temp = outputWidth;
            while (temp > 64)
            {
                temp /= 2;
                widths.Add(temp);
                if (temp == 64)
                    startEndTrims.Add(startEndTrim);
                else
                    startEndTrims.Add(startEndTrim * 2);    //Trim a bit more for the shorter widths
            }
            widths.Reverse();      // Smallest to largest
            startEndTrims.Reverse();

            double lowestFrequency = double.MaxValue;
            List<double> outputFrequencies = new();
            List<double> outputValues = new();
            int averages = 0;
            for (int n = 0; n < widths.Count; n++)
            {
                var nsd = NSD(input, sampleRate, startEndTrims[n], widths[n]);
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

        public static Task<Spectrum> StackedNSD_Async(Memory<double> input, double sampleRate, int startEndTrim, int outputWidth = 2048)
        {
            return Task.Factory.StartNew(() => StackedNSD(input, sampleRate, startEndTrim, outputWidth));
        }
    }
}
