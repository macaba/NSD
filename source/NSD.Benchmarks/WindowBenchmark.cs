using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace NSD.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    [Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
    public class WindowBenchmark
    {
        [Benchmark(Baseline = true)]
        public void GoertzelFilterBaseline()
        {
            Windows.FTNI(100000, out _, out _);
        }

        [Benchmark]
        public void GoertzelFilter1()
        {
            Windows1.FTNI(100000, out _, out _);
        }
    }
}
