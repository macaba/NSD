﻿using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using NSD.Benchmarks;

DefaultConfig.Instance.WithOptions(ConfigOptions.JoinSummary);
_ = BenchmarkRunner.Run<ComplexFftBenchmark>();
_ = BenchmarkRunner.Run<RealFftBenchmark>();
Console.ReadKey();