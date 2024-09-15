using System.Numerics;

namespace NSD
{
    internal class FFT
    {
        private readonly int length;
        private readonly Complex[] complexBuffer;
        private readonly double[] doubleBuffer;
        private readonly double[] doubleBuffer2;
        private readonly FftFlat.FastFourierTransform fft;

        public FFT(int length)
        {
            this.length = length;
            complexBuffer = new Complex[length];
            doubleBuffer = new double[length];
            doubleBuffer2 = new double[length];
            fft = new FftFlat.FastFourierTransform(length);
        }

        public void SubtractLineFitWithScaling(ReadOnlyMemory<double> data, Memory<double> lineFitOutput)
        {
            if (data.Length != length || lineFitOutput.Length != length)
                throw new ArgumentException("Array lengths don't match");

            var x = doubleBuffer;
            var y = doubleBuffer2;

            for (int i = 0; i < data.Length; i++)
                x[i] = i;
            
            data.Span.CopyTo(y);

            var (A, B) = MathNet.Numerics.Fit.Line(x, y);
            var lineFitOutputSpan = lineFitOutput.Span;
            var dataSpan = data.Span;
            for (int i = 0; i < data.Length; i++)
            {
                lineFitOutputSpan[i] = (dataSpan[i] - (A + B * i));
            }
        }

        public void PSD(ReadOnlyMemory<double> inputData, Memory<double> outputPsd, ReadOnlyMemory<double> window, double sampleRate)
        {
            if (inputData.Length != length || outputPsd.Length != length || window.Length != length)
                throw new ArgumentException("Array lengths don't match");

            // Apply window to data
            for (int i = 0; i < length; i++)
            {
                complexBuffer[i] = new Complex(inputData.Span[i] * window.Span[i], 0);
            }

            // Apply transform
            //MathNet.Numerics.IntegralTransforms.Fourier.Forward(fourierData, MathNet.Numerics.IntegralTransforms.FourierOptions.NoScaling);
            fft.Forward(complexBuffer);

            // Convert to magnitude spectrum
            double s2 = S2(window.Span);
            for (int i = 0; i < length; i++)
            {
                outputPsd.Span[i] = (2.0 * Math.Pow(complexBuffer[i].Magnitude, 2)) / (sampleRate * s2);       //"The factor 2 originates from the fact that we presumably use an efficient FFT algorithm that does not compute the redundant results for negative frequencies"
                //outputPsd.Span[i] = (Math.Pow(Math.Abs(fourierData[i].Magnitude), 2)) / (sampleRate * s2);
                if (double.IsNaN(outputPsd.Span[i]) || outputPsd.Span[i] > 1000000000000)
                    throw new Exception();
            }
        }

        public void PS(ReadOnlyMemory<double> inputData, Memory<double> outputPs, ReadOnlyMemory<double> window)
        {
            if (inputData.Length != length || outputPs.Length != length || window.Length != length)
                throw new ArgumentException("Array lengths don't match");

            // Apply window to data
            for (int i = 0; i < window.Length; i++)
            {
                complexBuffer[i] = new Complex(inputData.Span[i] * window.Span[i], 0);
            }

            // Apply transform
            //MathNet.Numerics.IntegralTransforms.Fourier.Forward(fourierData, MathNet.Numerics.IntegralTransforms.FourierOptions.NoScaling);
            fft.Forward(complexBuffer);

            // Convert to magnitude spectrum
            double s1 = S1(window.Span);
            for (int i = 0; i < inputData.Length; i++)
            {
                outputPs.Span[i] = (2.0 * Math.Pow(complexBuffer[i].Magnitude, 2)) / (s1 * s1);       //"The factor 2 originates from the fact that we presumably use an efficient FFT algorithm that does not compute the redundant results for negative frequencies"
                //outputPsd.Span[i] = (Math.Pow(Math.Abs(fourierData[i].Magnitude), 2)) / (s1 * s1);
                if (double.IsNaN(outputPs.Span[i]) || outputPs.Span[i] > 1000000000000)
                    throw new Exception();
            }
        }

        //public static void AverageVSDFromPSDCollection(List<Memory<double>> inputPSDs, Memory<double> outputAverageVSD)
        //{
        //    // https://holometer.fnal.gov/GH_FFT.pdf
        //    foreach (var psd in inputPSDs)
        //    {
        //        for (int i = 0; i < outputAverageVSD.Length; i++)
        //        {
        //            outputAverageVSD.Span[i] += psd.Span[i];
        //        }
        //    }
        //    double divisor = inputPSDs.Count;
        //    for (int i = 0; i < outputAverageVSD.Length; i++)
        //    {
        //        outputAverageVSD.Span[i] = outputAverageVSD.Span[i] / divisor;
        //    }

        //    // Convert to VSD
        //    for (int i = 0; i < outputAverageVSD.Length; i++)
        //    {
        //        outputAverageVSD.Span[i] = Math.Sqrt(outputAverageVSD.Span[i]);
        //    }
        //}

        public static void AddPSDToWorkingMemory(Memory<double> inputPSD, Memory<double> workingMemory)
        {
            for (int i = 0; i < inputPSD.Length; i++)
            {
                workingMemory.Span[i] += inputPSD.Span[i];
            }
        }

        public static void ConvertWorkingMemoryToAverageVSDInPlace(Memory<double> workingMemory, int count)
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

        private static double S2(ReadOnlySpan<double> window)
        {
            double sumSquared = 0;
            for (int i = 0; i < window.Length; i++)
            {
                sumSquared += Math.Pow(window[i], 2);
            }
            return sumSquared;
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
    }
}
