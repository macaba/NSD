using MathNet.Numerics.IntegralTransforms;
using System;
using System.Numerics;

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

        // https://raw.githubusercontent.com/felixpatzelt/colorednoise/master/colorednoise.py
        // exponent - 0: gaussian, 1: pink (1/f), 2: brown (1/f^2), -1: blue, -2: violet
        public static Memory<double> PowerLawGaussian(int samples, double sampleRate, double exponent)
        {
            var frequencies = Fourier.FrequencyScale(samples, sampleRate).Take((samples / 2) + 1).ToArray();
            var scalingFactors = frequencies.ToArray();
            for (int i = 1; i < scalingFactors.Length; i++)
            {
                scalingFactors[i] = Math.Pow(frequencies[i], -exponent / 2.0);
            }
            scalingFactors[0] = scalingFactors[1];

            var noiseSource = new MathNet.Filtering.DataSources.WhiteGaussianNoiseSource();
            Complex[] fourierData = new Complex[scalingFactors.Length];
            for (int i = 0; i < fourierData.Length; i++)
            {
                fourierData[i] = new Complex(noiseSource.ReadNextSample() * scalingFactors[i], noiseSource.ReadNextSample() * scalingFactors[i]);
            }
            fourierData[0] = new Complex(fourierData[0].Real * Math.Sqrt(2), 0);

            Fourier.Inverse(fourierData);

            double[] data = new double[fourierData.Length];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = fourierData[i].Magnitude;
            }

            return data;
        }

        //public static Memory<double> WhiteWithSlopeNoise(int samples, double sampleRate, double volt, double slopeCornerFrequency, double slope)
        //{
        //    var white = WhiteNoise(samples, sampleRate, volt);
        //    //var slope = something;

        //    return white;
        //}
    }
}
