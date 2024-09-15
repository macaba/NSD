using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Numerics;

namespace NSD.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    [Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
    public class ComplexFftBenchmark
    {
        private Complex[] values_FftFlat;
        private Complex[] values_FftSharp;
        private Complex[] values_MathNet;
        private Complex[] values_AelianFft;
        private Complex[] values_NWaveFft;

        private FftFlat.FastFourierTransform fftFlat;
        private NWaves.Transforms.Fft64 nWavesFft;

        private double[] values_NWavesFft_Real;
        private double[] values_NWavesFft_Img;

        private StreamWriter log;

        //[Params(256, 512, 1024, 2048, 4096, 8192)]
        [Params(8192)]
        public int Length;

        [GlobalSetup]
        public void Setup()
        {
            values_FftFlat = FftTestData.CreateComplex(Length);
            values_FftSharp = FftTestData.CreateComplex(Length);
            values_MathNet = FftTestData.CreateComplex(Length);
            values_AelianFft = FftTestData.CreateComplex(Length);
            values_NWaveFft = FftTestData.CreateComplex(Length);
            values_NWavesFft_Real = values_NWaveFft.Select(x => x.Real).ToArray();
            values_NWavesFft_Img = values_NWaveFft.Select(x => x.Imaginary).ToArray();

            fftFlat = new FftFlat.FastFourierTransform(Length);

            Aelian.FFT.FastFourierTransform.Initialize();

            nWavesFft = new NWaves.Transforms.Fft64(Length);

            var logPath = Path.Combine("log" + Length + ".txt");
            log = new StreamWriter(logPath);
            log.WriteLine("=== BEFORE ===");
            log.WriteLine("FftFlat: " + GetMaxValue(values_FftFlat));
            log.WriteLine("FftSharp: " + GetMaxValue(values_FftSharp));
            log.WriteLine("MathNet: " + GetMaxValue(values_MathNet));
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            log.WriteLine("=== AFTER ===");
            log.WriteLine("FftFlat: " + GetMaxValue(values_FftFlat));
            log.WriteLine("FftSharp: " + GetMaxValue(values_FftSharp));
            log.WriteLine("MathNet: " + GetMaxValue(values_MathNet));
            log.Dispose();
        }

        private static double GetMaxValue(Complex[] data)
        {
            return data.Select(x => Math.Max(Math.Abs(x.Real), Math.Abs(x.Imaginary))).Max();
        }

        [Benchmark(Baseline = true)]
        public void FftFlat()
        {
            fftFlat.Forward(values_FftFlat);
            fftFlat.Inverse(values_FftFlat);
        }

        [Benchmark]
        public void FftSharp()
        {
            global::FftSharp.FFT.Forward(values_FftSharp);
            global::FftSharp.FFT.Inverse(values_FftSharp);
        }

        [Benchmark]
        public void MathNet()
        {
            global::MathNet.Numerics.IntegralTransforms.Fourier.Forward(values_MathNet, global::MathNet.Numerics.IntegralTransforms.FourierOptions.AsymmetricScaling);
            global::MathNet.Numerics.IntegralTransforms.Fourier.Inverse(values_MathNet, global::MathNet.Numerics.IntegralTransforms.FourierOptions.AsymmetricScaling);
        }

        [Benchmark]
        public void AelianFft()
        {
            Aelian.FFT.FastFourierTransform.FFT(values_AelianFft, true);
            Aelian.FFT.FastFourierTransform.FFT(values_AelianFft, false);
        }

        [Benchmark]
        public void NWaves()
        {
            nWavesFft.Direct(values_NWavesFft_Real, values_NWavesFft_Img);
            nWavesFft.Inverse(values_NWavesFft_Real, values_NWavesFft_Img);
        }
    }
}
