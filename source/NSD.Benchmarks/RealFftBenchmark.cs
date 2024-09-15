using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace NSD.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    [Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
    public class RealFftBenchmark
    {
        private double[] values_FftFlat;
        private double[] values_FftSharp;
        private double[] values_MathNet;
        private double[] values_AelianFft;
        private double[] values_NWaves;
        private double[] values_NWaves_Real;
        private double[] values_NWaves_Img;

        private FftFlat.FastFourierTransform fftFlat;
        private FftFlat.RealFourierTransform fftFlatReal;
        private NWaves.Transforms.RealFft64 nWavesFft;

        private StreamWriter log;

        //[Params(256, 512, 1024, 2048, 4096, 8192)]
        [Params(8192)]
        public int Length;

        [GlobalSetup]
        public void Setup()
        {
            values_FftFlat = FftTestData.CreateDouble(Length).Append(0.0).Append(0.0).ToArray();
            values_FftSharp = FftTestData.CreateDouble(Length).ToArray();
            values_MathNet = FftTestData.CreateDouble(Length).Append(0.0).Append(0.0).ToArray();
            values_AelianFft = FftTestData.CreateDouble(Length).ToArray();
            values_NWaves = FftTestData.CreateDouble(Length).ToArray();
            values_NWaves_Real = new double[Length];
            values_NWaves_Img = new double[Length];

            fftFlatReal = new FftFlat.RealFourierTransform(Length);

            Aelian.FFT.FastFourierTransform.Initialize();

            nWavesFft = new NWaves.Transforms.RealFft64(Length);

            var logPath = Path.Combine("log" + Length + ".txt");
            log = new StreamWriter(logPath);
            log.WriteLine("=== BEFORE ===");
            log.WriteLine("FftFlatReal: " + GetMaxValue(values_FftFlat));
            log.WriteLine("FftSharpReal: " + GetMaxValue(values_FftSharp));
            log.WriteLine("MathNetReal: " + GetMaxValue(values_MathNet));
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            log.WriteLine("=== AFTER ===");
            log.WriteLine("FftFlatReal: " + GetMaxValue(values_FftFlat));
            log.WriteLine("FftSharpReal: " + GetMaxValue(values_FftSharp));
            log.WriteLine("MathNetReal: " + GetMaxValue(values_MathNet));
            log.Dispose();
        }

        private static double GetMaxValue(double[] data)
        {
            return data.Select(x => Math.Abs(x)).Max();
        }

        [Benchmark(Baseline = true)]
        public void FftFlat()
        {
            var spectrum = fftFlatReal.Forward(values_FftFlat);
            fftFlatReal.Inverse(spectrum);
        }

        [Benchmark]
        public void FftSharp()
        {
            var spectrum = global::FftSharp.FFT.ForwardReal(values_FftSharp);
            values_FftSharp = global::FftSharp.FFT.InverseReal(spectrum);
        }

        [Benchmark]
        public void MathNet()
        {
            global::MathNet.Numerics.IntegralTransforms.Fourier.ForwardReal(values_MathNet, Length, global::MathNet.Numerics.IntegralTransforms.FourierOptions.AsymmetricScaling);
            global::MathNet.Numerics.IntegralTransforms.Fourier.InverseReal(values_MathNet, Length, global::MathNet.Numerics.IntegralTransforms.FourierOptions.AsymmetricScaling);
        }

        [Benchmark]
        public void AelianFft()
        {
            Aelian.FFT.FastFourierTransform.RealFFT(values_AelianFft, true);
            Aelian.FFT.FastFourierTransform.RealFFT(values_AelianFft, false);
        }

        [Benchmark]
        public void NWaves()
        {
            nWavesFft.Direct(values_NWaves, values_NWaves_Real, values_NWaves_Img);
            nWavesFft.Inverse(values_NWaves, values_NWaves_Real, values_NWaves_Img);
        }
    }
}
