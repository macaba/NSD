using System.Numerics;

namespace NSD
{
    public class GoertzelFilter
    {
        private readonly double coeff;
        private readonly double sine;
        private readonly double cosine;

        public GoertzelFilter(double filterFreq, double sampleFreq)
        {
            double w = 2.0 * Math.PI * (filterFreq / sampleFreq);
            cosine = Math.Cos(w);
            sine = Math.Sin(w);
            coeff = 2.0 * cosine;
        }

        public Complex Process(Span<double> samples)
        {
            double Q0 = 0.0;
            double Q1 = 0.0;
            double Q2 = 0.0;

            for (int n = 0; n < samples.Length; n++)
            {
                Q0 = coeff * Q1 - Q2 + samples[n];
                Q2 = Q1;
                Q1 = Q0;
            }

            var real = Q1 * cosine - Q2;
            var imag = -Q1 * sine;
            return new Complex(real, imag);
        }
    }
}
