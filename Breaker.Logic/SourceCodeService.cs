﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Breaker.Logic;

public static class SourceCodeService
{
    public static IEnumerable<EndpointDetails> GetEndpointDetails(
        IDictionary<string, IReadOnlyCollection<ClassDeclarationSyntax>> classes)
    {
        var allClasses = classes.Values.SelectMany(x => x).ToList();

        foreach (var (projectName, classDeclarations) in classes)
        {
            foreach (var classDeclaration in classDeclarations)
            {
                var classAttributes = classDeclaration.AttributeLists.SelectMany(al => al.Attributes).ToList();

                var hasApiControllerAttribute = classAttributes.Any(a => a.Name.ToString() == "ApiController");

                var inheritsFromController = InheritsFromController(classDeclaration, allClasses);

                if (!hasApiControllerAttribute && !inheritsFromController) continue;

                var routeAttribute = classAttributes.SingleOrDefault(a => a.Name.ToString() == "Route");

                var baseRoute = routeAttribute?.ArgumentList?.Arguments.Single();

                var authorizeAttribute = classAttributes.SingleOrDefault(a => a.Name.ToString() == "Authorize");

                var publicMethods = classDeclaration.Members.OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Modifiers.Any(s => s.ValueText == "public"));

                foreach (var publicMethod in publicMethods)
                {
                    yield return publicMethod.ToEndpointDetails(projectName, baseRoute, authorizeAttribute, allClasses);
                }
            }
        }
    }

    private static bool InheritsFromController(ClassDeclarationSyntax classDeclaration,
        IReadOnlyCollection<ClassDeclarationSyntax> classDeclarations)
    {
        var baseClass = classDeclaration;

        while (baseClass is not null)
        {
            var baseTypes = baseClass.BaseList?.Types;

            if (baseTypes?.Any(b => b.Type.ToString() is "Controller" or "ControllerBase") ?? false) return true;

            var baseType = baseTypes?.SingleOrDefault(b => classDeclarations.Any(c => c.Identifier.Text == b.Type.ToString()));

            baseClass = classDeclarations.FirstOrDefault(c => c.Identifier.Text == baseType?.Type.ToString());
        }

        return false;
    }

    public static IEnumerable<ClassDeclarationSyntax> MergePartialClasses(
        this IEnumerable<ClassDeclarationSyntax> classDeclarations)
    {
        var classDeclarationsList = classDeclarations.ToList();
        
        var partialClasses = classDeclarationsList
            .Where(c => c.Modifiers.Any(m => m.ValueText == "partial"))
            .ToList();

        if (!partialClasses.Any()) return classDeclarationsList;

        var partialClassesGroups = partialClasses.GroupBy(x => x.Identifier.ToString());

        var mergedClasses = new List<ClassDeclarationSyntax>();

        foreach (var partialClassesGroup in partialClassesGroups)
        {
            var attributeLists = new SyntaxList<AttributeListSyntax>();
            var modifiers = new SyntaxTokenList();
            var keyword = partialClassesGroup.First().Keyword;
            var identifier = partialClassesGroup.First().Identifier;
            var typeParameterList = partialClassesGroup.SingleOrDefault(x => x.TypeParameterList is not null)?.TypeParameterList;
            var baseList = partialClassesGroup.SingleOrDefault(x => x.BaseList is not null)?.BaseList;
            var constraintClauses = new SyntaxList<TypeParameterConstraintClauseSyntax>();
            var openBraceToken = partialClassesGroup.First().OpenBraceToken;
            var members = new SyntaxList<MemberDeclarationSyntax>();
            var closeBraceToken = partialClassesGroup.First().CloseBraceToken;
            var semicolonToken = partialClassesGroup.First().SemicolonToken;

            foreach (var partialClass in partialClassesGroup)
            {
                attributeLists = attributeLists.AddRange(partialClass.AttributeLists);
                modifiers = modifiers.AddRange(partialClass.Modifiers);
                constraintClauses = constraintClauses.AddRange(partialClass.ConstraintClauses);
                members = members.AddRange(partialClass.Members);
            }

            var mergedClass = SyntaxFactory.ClassDeclaration(attributeLists, modifiers, keyword, identifier, typeParameterList, baseList,
                constraintClauses, openBraceToken, members, closeBraceToken, semicolonToken);

            mergedClasses.Add(mergedClass);
        }

        return classDeclarationsList
            .Where(c => c.Modifiers.All(m => m.ValueText != "partial"))
            .Concat(mergedClasses);
    }

    public static IDictionary<string, IReadOnlyCollection<ClassDeclarationSyntax>> GetClassDeclarations(
        DirectoryInfo solutionDir)
    {
        var projectDirs = GetProjectDirectories(solutionDir);

        var result = new Dictionary<string, IReadOnlyCollection<ClassDeclarationSyntax>>();

        foreach (var (projectName, projectDir) in projectDirs)
        {
            var sourceFiles = GetSourceFiles(projectDir);
            var classDeclarations = sourceFiles
                .SelectMany(GetClassDeclarations)
                .MergePartialClasses();

            result.Add(projectName, classDeclarations.ToList());
        }

        return result;
    }

    private static IDictionary<string, DirectoryInfo> GetProjectDirectories(DirectoryInfo solutionDir)
    {
        var csProjInfos = solutionDir
            .GetFiles("*.csproj", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(solutionDir.FullName, f.FullName).StartsWith(".breaker"));
        var xProjInfos = solutionDir
            .GetFiles("*.xproj", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(solutionDir.FullName, f.FullName).StartsWith(".breaker"));
        ;
        var projectInfos = csProjInfos.Concat(xProjInfos).ToList();
        return projectInfos.ToDictionary(projectInfo => Path.GetFileNameWithoutExtension(projectInfo.FullName),
            projectInfo => projectInfo.Directory);
    }

    private static IEnumerable<FileInfo> GetSourceFiles(DirectoryInfo projectDir)
    {
        var sourceInfos = projectDir
            .GetFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(projectDir.FullName, f.FullName).StartsWith(".breaker"));

        foreach (var sourceInfo in sourceInfos) yield return sourceInfo;
    }

    private static IEnumerable<ClassDeclarationSyntax> GetClassDeclarations(FileSystemInfo sourceInfo)
    {
        var sourceCode = File.ReadAllText(sourceInfo.FullName);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, CSharpParseOptions.Default, sourceInfo.FullName);

        return syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
    }
}