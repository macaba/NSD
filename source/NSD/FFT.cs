using System.Numerics;

namespace NSD
{
    internal class FFT
    {
        private readonly int length;
        private readonly Complex[] complexBuffer;
        private readonly FftFlat.FastFourierTransform fft;

        public FFT(int length)
        {
            this.length = length;
            complexBuffer = new Complex[length];
            fft = new FftFlat.FastFourierTransform(length);
        }

        public void PSD(ReadOnlyMemory<double> inputData, Memory<double> outputPsd, ReadOnlyMemory<double> window, double sampleRate, double windowS2)
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
            for (int i = 0; i < length; i++)
            {
                outputPsd.Span[i] = (2.0 * Math.Pow(complexBuffer[i].Magnitude, 2)) / (sampleRate * windowS2);       //"The factor 2 originates from the fact that we presumably use an efficient FFT algorithm that does not compute the redundant results for negative frequencies"
                //outputPsd.Span[i] = (Math.Pow(Math.Abs(fourierData[i].Magnitude), 2)) / (sampleRate * s2);
                if (double.IsNaN(outputPsd.Span[i]) || outputPsd.Span[i] > 1000000000000)
                    throw new Exception();
            }
        }

        public void PS(ReadOnlyMemory<double> inputData, Memory<double> outputPs, ReadOnlyMemory<double> window, double windowS1)
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
            for (int i = 0; i < inputData.Length; i++)
            {
                outputPs.Span[i] = (2.0 * Math.Pow(complexBuffer[i].Magnitude, 2)) / (windowS1 * windowS1);       //"The factor 2 originates from the fact that we presumably use an efficient FFT algorithm that does not compute the redundant results for negative frequencies"
                //outputPsd.Span[i] = (Math.Pow(Math.Abs(fourierData[i].Magnitude), 2)) / (s1 * s1);
                if (double.IsNaN(outputPs.Span[i]) || outputPs.Span[i] > 1000000000000)
                    throw new Exception();
            }
        }
    }
}
