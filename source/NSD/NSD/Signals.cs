using System;

namespace NSD
{
    public class Signals
    {
        public static Memory<double> TenNanoVoltRmsTestSignal()
        {
            var sine = MathNet.Numerics.Generate.Sinusoidal(1000000, 50, 5.1, 1.41e-8);
            for(int i = 0; i < sine.Length; i++)
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
    }
}
