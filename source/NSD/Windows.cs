using System;

namespace NSD
{
    public static class Windows
    {
        public static Memory<double> HFT95(int width)
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

        public static Memory<double> HFT90D(int width, out double optimumOverlap)
        {
            optimumOverlap = 0.76;
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

        public static Memory<double> FTNI(int width)
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
