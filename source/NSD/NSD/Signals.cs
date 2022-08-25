using System;

namespace NSD
{
    public class Signals
    {
        public static Memory<double> TenNanoVoltRmsTestSignal()
        {
            var sine = MathNet.Numerics.Generate.Sinusoidal(1000000, 50, 5.1, 1.41e-8);
            for (int i = 0; i < sine.Length; i++)
            {
                sine[i] = Math.Round(sine[i], 11);
            }
            return sine;
        }

        public static Memory<double> OneVoltRmsTestSignal()
        {
            var noiseSource = new MathNet.Filtering.DataSources.WhiteGaussianNoiseSource();
            var sine = MathNet.Numerics.Generate.Sinusoidal(1000000, 50, 5.1, 1.41);
            for (int i = 0; i < sine.Length; i++)
            {
                sine[i] = sine[i] + noiseSource.ReadNextSample();
            }
            return sine;
        }

        public static Memory<double> WhiteNoise(int samples, double sampleRate, double volt)
        {
            var noiseSource = new MathNet.Filtering.DataSources.WhiteGaussianNoiseSource(0, 1);
            Memory<double> data = new double[samples];
            for (int i = 0; i < data.Length; i++)
            {
                data.Span[i] = noiseSource.ReadNextSample() * volt * Math.Sqrt(sampleRate / 2);
            }
            return data;
        }
    }
}
