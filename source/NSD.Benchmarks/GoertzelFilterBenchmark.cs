using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace NSD.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    [Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
    public class GoertzelFilterBenchmark
    {
        private double[] samples;

        [GlobalSetup]
        public void Setup()
        {
            samples = new double[10000000];
        }

        [Benchmark(Baseline = true)]
        public void GoertzelFilterBaseline()
        {
            var filter = new GoertzelFilter(1000, 10000000);
            filter.Process(samples);
        }

        [Benchmark]
        public void GoertzelFilter1()
        {
            var filter = new GoertzelFilter1(1000, 10000000);
            filter.Process(samples);
        }
    }
}
