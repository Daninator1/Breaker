using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host;

namespace Breaker.Analyzer;

public static class Extensions
{
    public static string GetSolutionPath(this AnalyzerOptions options)
        => ((HostWorkspaceServices)options
                .GetType()
                .GetRuntimeProperty("Services")
                .GetValue(options))
            .Workspace
            .CurrentSolution
            .FilePath;
}