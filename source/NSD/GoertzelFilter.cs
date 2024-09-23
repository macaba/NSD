using System.Numerics;

namespace NSD
{
    public class GoertzelFilter
    {
        public double coeff;
        public double sine;
        public double cosine;

        public GoertzelFilter(double filterFreq, double sampleFreq)
        {
            double w = 2 * Math.PI * (filterFreq / sampleFreq);
            double wr, wi;

            wr = Math.Cos(w);
            wi = Math.Sin(w);
            coeff = 2 * wr;
            cosine = wr;
            sine = wi;
        }

        public Complex Process(Span<double> samples)
        {
            double sprev = 0.0;
            double sprev2 = 0.0;
            double s, imag, real;
            int n;

            for (n = 0; n < samples.Length; n++)
            {
                s = samples[n] + coeff * sprev - sprev2;
                sprev2 = sprev;
                sprev = s;
            }

            real = sprev * cosine - sprev2;
            imag = -sprev * sine;

            return new Complex(real, imag);
        }
    }
}
