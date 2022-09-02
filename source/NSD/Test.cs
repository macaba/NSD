//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using MathNet.Numerics.IntegralTransforms;
////using sqrt = numpy.sqrt;
////using newaxis = numpy.newaxis;
////using integer = numpy.integer;
////using irfft = numpy.fft.irfft;
////using rfftfreq = numpy.fft.rfftfreq;
////using default_rng = numpy.random.default_rng;
////using Generator = numpy.random.Generator;
////using RandomState = numpy.random.RandomState;
////using npsum = numpy.sum;
////using csv;

//namespace NSD
//{
//    public static class colorednoise
//    {
//        public static int powerlaw_psd_gaussian(int exponent, int size, int fmin = 0)
//        {
//            var frequencies = Fourier.FrequencyScale(size, 50);

//            // Validate / normalise fmin
//            //if (0 <= fmin && fmin <= 0.5)
//            //{
//            //    fmin = max(fmin, 1.0 / samples);
//            //}
//            //else
//            //{
//            //    throw new ValueError("fmin must be chosen between 0 and 0.5.");
//            //}
//            // Build scaling factors for all frequencies
//            for(int i = 0; i < frequencies.Length; i++)
//            {
//                frequencies[i] = Math.Pow(frequencies[i], -exponent / 2.0);
//            }




//            var s_scale = f;
//            var ix = npsum(s_scale < fmin);
//            if (ix && ix < s_scale.Count)
//            {
//                s_scale[::ix] = s_scale[ix];
//            }
//            s_scale = Math.Pow(s_scale, -exponent / 2.0);
//            // Calculate theoretical output standard deviation from scaling
//            var w = s_scale[1].copy();
//            w[-1] *= (1 + samples % 2) / 2.0;
//            var sigma = 2 * sqrt(npsum(Math.Pow(w, 2))) / samples;
//            // Adjust size to generate one Fourier component per frequency
//            size[-1] = f.Count;
//            // Add empty dimension(s) to broadcast s_scale along last
//            // dimension of generated random power + phase (below)
//            var dims_to_add = size.Count - 1;
//            s_scale = s_scale[ValueTuple.Create(newaxis) * dims_to_add + ValueTuple.Create(Ellipsis)];
//            // prepare random number generator
//            var normal_dist = _get_normal_distribution(random_state);
//            // Generate scaled random power + phase
//            var sr = normal_dist(scale: s_scale, size: size);
//            var si = normal_dist(scale: s_scale, size: size);
//            // If the signal length is even, frequencies +/- 0.5 are equal
//            // so the coefficient must be real.
//            if (!(samples % 2))
//            {
//                si[default, -1] = 0;
//                sr[default, -1] *= sqrt(2);
//            }
//            // Regardless of signal length, the DC component must be real
//            si[default, 0] = 0;
//            sr[default, 0] *= sqrt(2);
//            // Combine power + corrected phase to Fourier components
//            //<parser-error>
//            // Transform to real time series & scale to unit variance
//            var y = irfft(s, n: samples, axis: -1) / sigma;
//            return y;
//        }
//    }
//}
