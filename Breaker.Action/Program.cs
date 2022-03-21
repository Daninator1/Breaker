using Breaker.Action;
using Breaker.Logic;
using CommandLine;
using static CommandLine.Parser;


var parser = Default.ParseArguments(() => new ActionInputs(), args);
parser.WithNotParsed(
    errors =>
    {
        foreach (var error in errors) Console.WriteLine($"::error::{error}");
        Environment.Exit(-1);
    });

parser.WithParsed(RunAnalysis);

static void RunAnalysis(ActionInputs inputs)
{
    var actualClasses = SourceCodeService.GetClassDeclarations(new DirectoryInfo(inputs.Actual));
    var actualEndpoints = SourceCodeService.GetEndpointDetails(actualClasses).ToList();

    var expectedClasses = SourceCodeService.GetClassDeclarations(new DirectoryInfo(inputs.Expected));
    var expectedEndpoints = SourceCodeService.GetEndpointDetails(expectedClasses).ToList();

    var endpointChanges = Comparer.CompareEndpoints(actualEndpoints, expectedEndpoints).ToList();

    if (endpointChanges.Count > 0)
    {
        foreach (var (location, message) in endpointChanges)
        {
            var file = location.SourceTree?.FilePath[inputs.Actual.Length..];
            var lineSpan = location.GetLineSpan();

            var error =
                $@"::error file={file},line={lineSpan.StartLinePosition.Line + 1},endLine={lineSpan.EndLinePosition.Line + 1},col={lineSpan.StartLinePosition.Character + 1},endColumn={lineSpan.EndLinePosition.Character + 1}::{message}";
            Console.WriteLine(error);
        }

        Environment.Exit(-1);
    }
    else
    {
        Console.WriteLine("::notice::No breaking changes found");
        Environment.Exit(0);
    }
}