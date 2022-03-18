using System.IO;
using System.Linq;
using Breaker.Analyzer;
using Breaker.Logic;
using FluentAssertions;
using NUnit.Framework;

namespace Breaker.Tests;

public class Tests
{
    [Test]
    public void TestCloneSolution()
    {
        var result =
            GitService.GetOrUpdateSolution(new DirectoryInfo(@"C:\Users\danin\Desktop\AngularSPAWebAPI"), "0ca8f0dd5bdda7770822333b7369dd9704059433");

        result.FullName.Should().Be(Path.Combine(@"C:\Users\danin\Desktop\AngularSPAWebAPI", ".breaker", "AngularSPAWebAPI"));
    }

    [Test]
    public void TestGetClassDeclarations()
    {
        var result =
            SourceCodeService.GetClassDeclarations(
                new DirectoryInfo(Path.Combine(@"C:\Users\danin\Desktop\AngularSPAWebAPI", ".breaker", "AngularSPAWebAPI")));

        result.ToList().Should().NotBeNullOrEmpty();
    }

    [Test]
    public void TestGetEndpointDetails()
    {
        var classes =
            SourceCodeService.GetClassDeclarations(
                new DirectoryInfo(Path.Combine(@"C:\Users\danin\Desktop\AngularSPAWebAPI", ".breaker", "AngularSPAWebAPI")));
        var result = SourceCodeService.GetEndpointDetails(classes);

        result.ToList().Should().NotBeNullOrEmpty();
    }

    [Test]
    public void TestCompareEndpoints()
    {
        var expectedClasses =
            SourceCodeService.GetClassDeclarations(
                new DirectoryInfo(Path.Combine(@"C:\Users\danin\Desktop\AngularSPAWebAPI", ".breaker", "AngularSPAWebAPI")));
        var actualClasses =
            SourceCodeService.GetClassDeclarations(new DirectoryInfo(@"C:\Users\danin\Desktop\AngularSPAWebAPI"));
        var expectedEndpoints = SourceCodeService.GetEndpointDetails(expectedClasses).ToList();
        var actualEndpoints = SourceCodeService.GetEndpointDetails(actualClasses).ToList();

        var result = Comparer.CompareEndpoints(actualEndpoints, expectedEndpoints).ToList();

        result.Should().NotBeNullOrEmpty();
    }
}