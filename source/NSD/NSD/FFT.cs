using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System;
using System.Numerics;

namespace NSD
{
    internal class FFT
    {
        public static void InplaceSubtractLineFitWithScaling(Memory<double> data, double inputScale = 1)
        {
            double[] x = new double[data.Length];
            for (int i = 0; i < data.Length; i++)
                x[i] = i;
            var (A, B) = Fit.Line(x, data.Span.ToArray());
            // Calculate average
            //double sum = 0;
            //for (int i = 0; i < data.Length; i++)
            //{
            //    sum += data.Span[i];
            //}
            //double average = sum / data.Length;

            // Subtract average
            for (int i = 0; i < data.Length; i++)
            {
                data.Span[i] = (data.Span[i] - (A + B*i)) * inputScale;
                //data.Span[i] = (data.Span[i] - average) * inputScale;
            }
        }

        public static Memory<double> SubtractLineFitWithScaling(Memory<double> data, double inputScale = 1)
        {
            double[] x = new double[data.Length];
            Memory<double> result = new double[data.Length];
            for (int i = 0; i < data.Length; i++)
                x[i] = i;
            var (A, B) = Fit.Line(x, data.Span.ToArray());
            for (int i = 0; i < data.Length; i++)
            {
                result.Span[i] = (data.Span[i] - (A + B * i)) * inputScale;
            }
            return result;
        }

        public static void PSD(ReadOnlyMemory<double> inputData, Memory<double> outputPsd, ReadOnlyMemory<double> window, double sampleRate)
        {
            if (inputData.Length != outputPsd.Length || outputPsd.Length != window.Length)
                throw new ArgumentException("Array lengths don't match");

            // Apply window to data
            Complex[] fourierData = new Complex[inputData.Length];
            for (int i = 0; i < window.Length; i++)
            {
                fourierData[i] = new Complex(inputData.Span[i] * window.Span[i], 0);
            }

            // Apply transform
            Fourier.Forward(fourierData, FourierOptions.NoScaling);

            // Convert to magnitude spectrum
            double s2 = S2(window.Span);
            for (int i = 0; i < inputData.Length; i++)
            {
                outputPsd.Span[i] = (2.0 * Math.Pow(fourierData[i].Magnitude, 2)) / (sampleRate * s2);       //"The factor 2 originates from the fact that we presumably use an efficient FFT algorithm that does not compute the redundant results for negative frequencies"
                //outputPsd.Span[i] = (Math.Pow(Math.Abs(fourierData[i].Magnitude), 2)) / (sampleRate * s2);
                if (double.IsNaN(outputPsd.Span[i]) || outputPsd.Span[i] > 1000000000000)
                    throw new Exception();
            }
        }

        public static void PS(ReadOnlyMemory<double> inputData, Memory<double> outputPsd, ReadOnlyMemory<double> window, double sampleRate)
        {
            if (inputData.Length != outputPsd.Length || outputPsd.Length != window.Length)
                throw new ArgumentException("Array lengths don't match");

            // Apply window to data
            Complex[] fourierData = new Complex[inputData.Length];
            for (int i = 0; i < window.Length; i++)
            {
                fourierData[i] = new Complex(inputData.Span[i] * window.Span[i], 0);
            }

            // Apply transform
            Fourier.Forward(fourierData, FourierOptions.NoScaling);

            // Convert to magnitude spectrum
            double s1 = S1(window.Span);
            for (int i = 0; i < inputData.Length; i++)
            {
                outputPsd.Span[i] = (2.0 * Math.Pow(fourierData[i].Magnitude, 2)) / (s1 * s1);       //"The factor 2 originates from the fact that we presumably use an efficient FFT algorithm that does not compute the redundant results for negative frequencies"
                //outputPsd.Span[i] = (Math.Pow(Math.Abs(fourierData[i].Magnitude), 2)) / (s1 * s1);
                if (double.IsNaN(outputPsd.Span[i]) || outputPsd.Span[i] > 1000000000000)
                    throw new Exception();
            }
        }

        public static void AverageVSDFromPSDCollection(List<Memory<double>> inputPSDs, Memory<double> outputAverageVSD)
        {
            // https://holometer.fnal.gov/GH_FFT.pdf
            foreach (var psd in inputPSDs)
            {
                for (int i = 0; i < outputAverageVSD.Length; i++)
                {
                    outputAverageVSD.Span[i] += psd.Span[i];
                }
            }
            double divisor = inputPSDs.Count;
            for (int i = 0; i < outputAverageVSD.Length; i++)
            {
                outputAverageVSD.Span[i] = outputAverageVSD.Span[i] / divisor;
            }

            // Convert to VSD
            for (int i = 0; i < outputAverageVSD.Length; i++)
            {
                outputAverageVSD.Span[i] = Math.Sqrt(outputAverageVSD.Span[i]);
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
