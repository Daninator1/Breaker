using CommandLine;

namespace Breaker.Action;

public class ActionInputs
{
    [Option('a', "actual",
        Required = true,
        HelpText = "Path to the actual solution.")]
    public string Actual { get; set; } = null!;

    [Option('e', "expected",
        Required = true,
        HelpText = "Path to the expected solution.")]
    public string Expected { get; set; } = null!;
}