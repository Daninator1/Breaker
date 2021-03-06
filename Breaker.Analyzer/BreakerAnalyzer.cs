using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Breaker.Logic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Breaker.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class BreakerAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "Breaker";

    private const string Category = "Naming";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle),
        Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableString MessageFormat =
        new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager,
            typeof(Resources));

    private static readonly LocalizableString Description =
        new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager,
            typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat,
        Category, DiagnosticSeverity.Warning, true, Description);

    private static IReadOnlyCollection<EndpointDetails> expectedEndpoints = new List<EndpointDetails>();
    private static readonly Dictionary<string, IEnumerable<ClassDeclarationSyntax>> CurrentClassesDictionary = new();
    private static bool getOrUpdateSolution = true;

    private static SimpleFileLogger logger;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
        context.RegisterSyntaxNodeAction(OnSyntaxNodeAction, SyntaxKind.ClassDeclaration);
        context.RegisterCompilationAction(OnCompilationAction);
    }

    private static void OnSyntaxNodeAction(SyntaxNodeAnalysisContext context)
        => Analyze(context.Options, context.Compilation, context.ReportDiagnostic);

    private static void OnCompilationAction(CompilationAnalysisContext context)
        => Analyze(context.Options, context.Compilation, context.ReportDiagnostic);

    private static void Analyze(AnalyzerOptions options, Compilation compilation, Action<Diagnostic> reportDiagnostic)
    {
        try
        {
            var solutionDirectoryInfo =
                new DirectoryInfo(Path.GetDirectoryName(options.GetSolutionPath()) ?? string.Empty);

            logger = new SimpleFileLogger(Path.Combine(solutionDirectoryInfo.FullName, ".breaker", "breaker.log"));

            if (getOrUpdateSolution)
            {
                var settingsPath = Path.Combine(solutionDirectoryInfo.FullName, "breaker.json");

                string gitRef = null;

                if (File.Exists(settingsPath))
                {
                    var settings = BreakerSettings.Parse(settingsPath);
                    gitRef = settings.GitRef;
                }

                var clonedRepoInfo = GitService.GetOrUpdateSolution(solutionDirectoryInfo, gitRef);

                if (clonedRepoInfo is null)
                {
                    logger.Log("Cloned repo was null, aborting.");
                    return;
                }

                var clonedRepoClasses =
                    SourceCodeService.GetClassDeclarations(clonedRepoInfo);

                expectedEndpoints = SourceCodeService.GetEndpointDetails(clonedRepoClasses).ToList();
                getOrUpdateSolution = false;
            }

            var assemblyClasses = compilation.SyntaxTrees.SelectMany(x
                => x.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>());

            if (CurrentClassesDictionary.ContainsKey(compilation.AssemblyName!))
                CurrentClassesDictionary[compilation.AssemblyName] = assemblyClasses;
            else
                CurrentClassesDictionary.Add(compilation.AssemblyName, assemblyClasses);

            var currentClasses = CurrentClassesDictionary.Values.SelectMany(x => x).ToList();

            var currentEndpoints =
                SourceCodeService.GetEndpointDetails(new Dictionary<string, IReadOnlyCollection<ClassDeclarationSyntax>>
                    { { compilation.AssemblyName, currentClasses } });

            var endpointChanges = Comparer.CompareEndpoints(currentEndpoints.ToList(),
                expectedEndpoints.Where(e => CurrentClassesDictionary.ContainsKey(e.ProjectName)).ToList());
            
            foreach (var (location, message) in endpointChanges)
            {
                try
                {
                    var diagnostic = Diagnostic.Create(Rule, location, message);
                    reportDiagnostic(diagnostic);
                }
                catch (Exception e)
                {
#if DEBUG
                    logger.Log(e.Message);
#endif
                }
            }
        }
        catch (Exception e)
        {
            logger.Log(e.Message);
            logger.Log(e.StackTrace);
        }
    }
}