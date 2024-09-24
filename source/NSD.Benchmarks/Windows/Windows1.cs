namespace NSD
{
    public static class Windows1
    {
        /// <param name="optimumOverlap">Optimum overlap in ratio of width, used for Welch method</param>
        /// <param name="NENBW">Normalized Equivalent Noise BandWidth with unit of bins</param>
        public static Memory<double> FTNI(int width, out double optimumOverlap, out double NENBW)
        {
            // FTNI - https://holometer.fnal.gov/GH_FFT.pdf
            // wj = 0.2810639 − 0.5208972 cos(z) + 0.1980399 cos(2z).
            optimumOverlap = 0.656;
            NENBW = 2.9656;
            Memory<double> window = new double[width];
            int i = 0;
            unsafe
            {
                fixed (double* windowP = window.Span)
                {
                    double* dataPointer = windowP;
                    double* endPointer = windowP + window.Length;
                    while (dataPointer < endPointer)
                    {
                        double z = (2.0 * Math.PI * i) / width;
                        double wj = 0.2810639 - (0.5208972 * Math.Cos(z)) + (0.1980399 * Math.Cos(2 * z));
                        *dataPointer = wj;
                        dataPointer++;
                        i++;
                    }
                }
            }
            return window;
        }
    }
}
