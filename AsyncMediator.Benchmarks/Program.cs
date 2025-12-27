using AsyncMediator.Benchmarks.Benchmarks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);

if (args.Length > 0 && args[0] == "--all")
{
    BenchmarkRunner.Run(
    [
        typeof(CommandHandlingBenchmark),
        typeof(EventDeferralBenchmark),
        typeof(QueryBenchmark),
        typeof(HandlerResolutionBenchmark),
        typeof(MemoryAllocationBenchmark)
    ], config);
}
else
{
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
}
