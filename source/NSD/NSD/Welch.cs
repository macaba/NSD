using System;

namespace NSD
{
    public class Welch
    {
        public static Memory<double> NSD_SingleSeries(Memory<double> input, double sampleRate, double inputScale = 1, int outputWidth = 2048)
        {
            var window = GetHFT90DWindow(outputWidth);
            //FFT.InplaceSubtractLineFitWithScaling(input, inputScale);

            // Overlap
            // "Optimum" 76%
            // Amplitude flatness nearly 1.0 = 80%
            // Power flatness nearly 1.0 = 85%
            // Aim for 80% overlap

            int startIndex = 0;
            int endIndex = outputWidth;
            int overlap = (int)(outputWidth * (1.0-0.76));
            List<Memory<double>> spectrums = new();
            while (endIndex < input.Length)
            {
                var newSlice = input.Slice(startIndex, outputWidth);
                var data = FFT.SubtractLineFitWithScaling(newSlice, inputScale);
                Memory<double> psd = new double[outputWidth];
                FFT.PSD(data, psd, window, sampleRate);
                spectrums.Add(psd);             
                startIndex += overlap;
                endIndex += overlap;
            }

            Memory<double> vsd = new double[outputWidth];
            FFT.AverageVSDFromPSDCollection(spectrums, vsd);
            return vsd;
        }

        public static Memory<double> GetHFT95Window(int width)
        {
            // HFT95 - https://holometer.fnal.gov/GH_FFT.pdf
            // wj = 1 − 1.9383379 cos(z) + 1.3045202 cos(2z) − 0.4028270 cos(3z) + 0.0350665 cos(4z).
            Memory<double> window = new double[width];
            for (int i = 0; i < width; i++)
            {
                double z = (2.0 * Math.PI * i) / width;
                double wj = 1 - (1.9383379 * Math.Cos(z)) + (1.3045202 * Math.Cos(2 * z)) - (0.4028270 * Math.Cos(3 * z)) + (0.0350665 * Math.Cos(4 * z));
                window.Span[i] = wj;
            }
            return window;
        }

        public static Memory<double> GetHFT90DWindow(int width)
        {
            // HFT90D - https://holometer.fnal.gov/GH_FFT.pdf
            // wj = 1 − 1.942604 cos(z) + 1.340318 cos(2z) − 0.440811 cos(3z) + 0.043097 cos(4z).
            Memory<double> window = new double[width];
            for (int i = 0; i < width; i++)
            {
                double z = (2.0 * Math.PI * i) / width;
                double wj = 1 - (1.942604 * Math.Cos(z)) + (1.340318 * Math.Cos(2 * z)) - (0.440811 * Math.Cos(3 * z)) + (0.043097 * Math.Cos(4 * z));
                window.Span[i] = wj;
            }
            return window;
        }

        public static Memory<double> GetFTNIWindow(int width)
        {
            Memory<double> window = new double[width];
            for (int i = 0; i < width; i++)
            {
                double z = (2.0 * Math.PI * i) / width;
                double wj = 0.2810639 - (0.5208972 * Math.Cos(z)) + (0.1980399 * Math.Cos(2 * z));
                window.Span[i] = wj;
            }
            return window;
        }
    }
}
