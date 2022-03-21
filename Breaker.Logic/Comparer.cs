using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Breaker.Logic;

public static class Comparer
{
    private static readonly (string name, bool removeAllowed)[] CheckedAttributes =
    {
        ("Required", true),
        ("MinLength", true),
        ("MaxLength", true),
        ("Range", true),
        ("RegularExpression", true),
        ("StringLength", true),
        ("Compare", true),
        ("FromQuery", false),
        ("FromBody", false),
        ("FromForm", false),
        ("FromHeader", false),
        ("FromRoute", false),
        ("FromServices", false)
    };

    private static EndpointDetails GetActualEndpoint(EndpointDetails expectedEndpoint, IReadOnlyCollection<EndpointDetails> actualEndpoints)
    {
        var possibleActualEndpoints = actualEndpoints.Where(a
            => IsOneHttpMethodRouteEqual(a.HttpMethodSpecificRoutes, expectedEndpoint.HttpMethodSpecificRoutes)).ToList();

        return possibleActualEndpoints.Count > 1
            ? possibleActualEndpoints.SingleOrDefault(a => a.Identifier.ValueText == expectedEndpoint.Identifier.ValueText)
            : possibleActualEndpoints.FirstOrDefault();

        static bool IsOneHttpMethodRouteEqual(IEnumerable<(AttributeSyntax, RouteDetails)> actualHttpMethodRoutes,
            IEnumerable<(AttributeSyntax, RouteDetails)> expectedHttpMethodRoutes)
            => actualHttpMethodRoutes.Any(a
                => expectedHttpMethodRoutes.Any(e
                    => e.Item1?.ToString() == a.Item1?.ToString() && e.Item2?.RouteText == a.Item2?.RouteText));
    }

    private static (string Namespace, string BaseRoute, ClassDeclarationSyntax Controller, string ProjectName)
        GetActualEndpointsGroup(
            (string Namespace, string BaseRoute, ClassDeclarationSyntax Controller, string ProjectName)
                expectedEndpointsGroup,
            IReadOnlyCollection<(string Namespace, string BaseRoute, ClassDeclarationSyntax Controller, string ProjectName
                )> actualEndpointsGroups)
    {
        var possibleActualEndpointsGroups = actualEndpointsGroups.Where(ag
            => expectedEndpointsGroup.BaseRoute is not null && expectedEndpointsGroup.BaseRoute == ag.BaseRoute).ToList();

        if (possibleActualEndpointsGroups.Count == 0)
            possibleActualEndpointsGroups = actualEndpointsGroups.Where(ag
                => expectedEndpointsGroup.Controller.Identifier.ValueText == ag.Controller.Identifier.ValueText).ToList();

        if (possibleActualEndpointsGroups.Count > 1)
            possibleActualEndpointsGroups =
                possibleActualEndpointsGroups.Where(ag
                    => expectedEndpointsGroup.Namespace == ag.Namespace ||
                       expectedEndpointsGroup.ProjectName == ag.ProjectName).ToList();

        return possibleActualEndpointsGroups.Count == 1 ? possibleActualEndpointsGroups.First() : default;
    }

    public static IEnumerable<(Location, string)> CompareEndpoints(
        IReadOnlyCollection<EndpointDetails> actualEndpoints,
        IReadOnlyCollection<EndpointDetails> expectedEndpoints)
    {
        if (!actualEndpoints.Any()) yield break;

        var expectedEndpointsGroups = expectedEndpoints
            .GroupBy(x => (x.Namespace, x.BaseRoute.RouteText, x.Controller, x.ProjectName)).ToList();

        var actualEndpointsGroups = actualEndpoints
            .GroupBy(x => (x.Namespace, x.BaseRoute.RouteText, x.Controller, x.ProjectName)).ToList();

        foreach (var expectedEndpointsGroup in expectedEndpointsGroups)
        {
            var actualEndpointsGroupKey =
                GetActualEndpointsGroup(expectedEndpointsGroup.Key, actualEndpointsGroups.Select(x => x.Key).ToList());

            var actualEndpointsGroup = actualEndpointsGroups.SingleOrDefault(ag => ag.Key == actualEndpointsGroupKey);

            foreach (var expectedEndpoint in expectedEndpointsGroup)
            {
                var actualEndpoint = actualEndpointsGroup is null
                    ? null
                    : GetActualEndpoint(expectedEndpoint,
                        actualEndpointsGroup.Select(x => x).ToList());

                if (actualEndpoint is null)
                {
                    var location = actualEndpointsGroup?.Any() ?? false
                        ? actualEndpointsGroup.First().Identifier.GetLocation()
                        : actualEndpointsGroups.First().First().Class.Identifier
                            .GetLocation();

                    yield return (
                        location,
                        $"{expectedEndpoint.Class.Identifier.ValueText}: endpoint {expectedEndpoint.Identifier.ValueText} does no longer exist");
                }
                else
                {
                    foreach (var endpointChange in CompareEndpoints(actualEndpoint, expectedEndpoint))
                        yield return endpointChange;
                }
            }
        }
    }

    private static IEnumerable<(Location, string)> CompareEndpoints(EndpointDetails actual,
        EndpointDetails expected)
    {
        foreach (var httpMethodChange in CompareHttpMethods(actual, expected))
            yield return httpMethodChange;

        if (actual.BaseRoute.RouteText != expected.BaseRoute.RouteText)
            yield return (actual.Identifier.GetLocation(),
                $"{actual.Identifier.ValueText}: base route {actual.BaseRoute.RouteText} is different");

        var endpointLocation = actual.Identifier.GetLocation();

        foreach (var authorizationChange in CompareAuthorization(actual, expected, endpointLocation))
            yield return authorizationChange;


        var endpointName = actual.Identifier.ValueText;

        foreach (var returnTypeChange in CompareTypes(actual.ReturnTypes.ToList(), expected.ReturnTypes.ToList(),
                     endpointLocation,
                     endpointName, true))
            yield return returnTypeChange;

        foreach (var parameterChange in CompareTypes(actual.ParameterTypes?.ToList(),
                     expected.ParameterTypes?.ToList(), endpointLocation, endpointName))
            yield return parameterChange;

        foreach (var attributeChange in CompareAttributes(actual.Attributes?.ToList(),
                     expected.Attributes?.ToList(),
                     endpointLocation, endpointName, false))
            yield return attributeChange;
    }

    private static IEnumerable<(Location, string)> CompareHttpMethods(EndpointDetails actual,
        EndpointDetails expected)
    {
        if (actual.HttpMethodSpecificRoutes.Count() == 1 && expected.HttpMethodSpecificRoutes.Count() == 1 &&
            actual.HttpMethodSpecificRoutes.All(a
                => a.Item1?.Name.ToString() != expected.HttpMethodSpecificRoutes.First().Item1?.Name.ToString()))
        {
            yield return (actual.HttpMethodSpecificRoutes.First().Item1?.GetLocation(),
                $"{actual.Identifier.ValueText}: http method {actual.HttpMethodSpecificRoutes.First().Item1?.Name} is different");
        }
        else
        {
            var missingHttpMethods =
                expected.HttpMethodSpecificRoutes.Where(e
                    => actual.HttpMethodSpecificRoutes.All(a => a.Item1?.Name.ToString() != e.Item1?.Name.ToString()));

            foreach (var missingHttpMethod in missingHttpMethods)
                yield return (actual.Identifier.GetLocation(),
                    $"{actual.Identifier.ValueText}: http method {missingHttpMethod.Item1?.Name} is missing");
        }
    }

    private static IEnumerable<(Location, string)> CompareAuthorization(EndpointDetails actual,
        EndpointDetails expected, Location fallbackLocation)
    {
        var actualAuthorization = actual.SpecificAuthorization ?? actual.BaseAuthorization;
        if (actualAuthorization is null) yield break;

        var expectedAuthorization = expected.SpecificAuthorization ?? expected.BaseAuthorization;

        foreach (var authorizationChange in CompareAttributes(actualAuthorization,
                     expectedAuthorization, fallbackLocation, actual.Identifier.Text, true))
            yield return authorizationChange;
    }

    private static IEnumerable<(Location, string)> CompareTypes(IReadOnlyCollection<TypeDetails> actualTypes,
        IReadOnlyCollection<TypeDetails> expectedTypes, Location fallbackLocation,
        string endpointName, bool ignoreTasks = false)
    {
        if (expectedTypes is not null)
        {
            if (actualTypes is not null)
            {
                if (ignoreTasks)
                {
                    actualTypes = actualTypes.Select(RemoveTask).ToList();
                    expectedTypes = expectedTypes.Select(RemoveTask).ToList();
                }

                var additionalTypes = actualTypes
                    .Where(a
                        => expectedTypes.All(e
                            => (e.PropertyTypes is null && a.Type.Text != e.Type.Text) || a.IsNullable != e.IsNullable ||
                               a.Identifier.ValueText != e.Identifier.ValueText))
                    .ToList();

                foreach (var expectedType in expectedTypes)
                {
                    var actualType = actualTypes.SingleOrDefault(p
                        => ((p.PropertyTypes is not null && expectedType.PropertyTypes is not null) ||
                            p.Type.Text == expectedType.Type.Text) && p.IsNullable == expectedType.IsNullable &&
                           p.Identifier.ValueText == expectedType.Identifier.ValueText);

                    if (actualType is not null)
                    {
                        foreach (var typeChange in CompareTypes(actualType, expectedType,
                                     fallbackLocation, endpointName))
                            yield return typeChange;

                        foreach (var attributeChange in CompareAttributes(actualType.Attributes?.ToList(),
                                     expectedType.Attributes?.ToList(),
                                     actualType.Identifier.GetLocation(), endpointName, actualType.IsSimpleType))
                            yield return attributeChange;
                    }
                    else
                    {
                        var potentialAdds = additionalTypes.Where(t
                            => t.Identifier.ValueText == expectedType.Identifier.ValueText).ToList();

                        if (potentialAdds.Any())
                            foreach (var potentialAdd in potentialAdds.Where(p => !p.IsNullable))
                            {
                                yield return (potentialAdd.Type.GetLocation(),
                                    $"{endpointName}: {expectedType.Identifier.ValueText} changed from {expectedType.Type.Text}{(expectedType.IsNullable ? "?" : "")} to {potentialAdd.Type.Text}");

                                additionalTypes.Remove(potentialAdd);

                                foreach (var attributeChange in CompareAttributes(potentialAdd.Attributes?.ToList(),
                                             expectedType.Attributes?.ToList(),
                                             potentialAdd.Identifier.GetLocation(), endpointName, potentialAdd.IsSimpleType))
                                    yield return attributeChange;
                            }
                        else
                            yield return (fallbackLocation,
                                $"{endpointName}: {expectedType.Type.Text} {expectedType.Identifier.ValueText} was removed");
                    }
                }

                foreach (var additionalType in additionalTypes.Where(a => !a.IsNullable))
                    yield return (additionalType.Identifier.GetLocation(),
                        $"{endpointName}: {additionalType.Type.Text} {additionalType.Identifier.ValueText} was added");
            }
            else
            {
                foreach (var expectedType in expectedTypes)
                    yield return (fallbackLocation,
                        $"{endpointName}: {expectedType.Type.Text} {expectedType.Identifier.ValueText} was removed");
            }
        }
        else if (actualTypes is not null)
        {
            foreach (var actualType in actualTypes)
                yield return (actualType.Type.GetLocation(),
                    $"{endpointName}: {actualType.Type.Text} {actualType.Identifier.ValueText} was added");
        }

        static TypeDetails RemoveTask(TypeDetails type)
        {
            if (type.Type.Text == "Task" && type.GenericTypes.Count() == 1) return type.GenericTypes.Single();

            return type;
        }
    }

    private static IEnumerable<(Location, string)> CompareAttributes(
        IReadOnlyCollection<AttributeSyntax> actualAttributes,
        IReadOnlyCollection<AttributeSyntax> expectedAttributes,
        Location fallbackLocation, string endpointName, bool isSimpleType)
    {
        var checkedAttributesFiltered = isSimpleType
            ? CheckedAttributes.Where(c => c.name != "FromQuery").ToArray()
            : CheckedAttributes;

        if (expectedAttributes is not null)
        {
            var expectedAttributesFiltered = checkedAttributesFiltered
                .Where(c => expectedAttributes.Any(e => e.Name.ToString() == c.name))
                .Select(c =>
                {
                    var expected = expectedAttributes.Single(e => e.Name.ToString() == c.name);
                    return (expected, c.removeAllowed);
                })
                .ToList();

            if (actualAttributes is not null)
            {
                var actualAttributesFiltered = actualAttributes
                    .Where(a => checkedAttributesFiltered.Any(c => c.name == a.Name.ToString())).ToList();

                foreach (var expectedAttribute in expectedAttributesFiltered)
                {
                    var actualAttribute = actualAttributesFiltered.SingleOrDefault(a
                        => a.Name.ToString() == expectedAttribute.expected.Name.ToString());

                    foreach (var attributeChange in CompareAttributes(actualAttribute, expectedAttribute.expected, fallbackLocation,
                                 endpointName, expectedAttribute.removeAllowed))
                        yield return attributeChange;
                }

                var additionalAttributesFiltered = actualAttributesFiltered.Where(a
                    => expectedAttributesFiltered.All(e => e.expected.Name.ToString() != a.Name.ToString()));

                foreach (var additionalAttribute in additionalAttributesFiltered)
                    yield return (additionalAttribute.GetLocation(),
                        $"{endpointName}: {additionalAttribute.Name} attribute was added");
            }
            else
            {
                foreach (var expectedAttribute in expectedAttributesFiltered.Where(e => !e.removeAllowed))
                    yield return (fallbackLocation,
                        $"{endpointName}: {expectedAttribute.expected.Name} attribute was removed");
            }
        }
        else if (actualAttributes is not null)
        {
            var actualAttributesFiltered = actualAttributes
                .Where(a => checkedAttributesFiltered.Any(c => c.name == a.Name.ToString())).ToList();

            foreach (var actualAttribute in actualAttributesFiltered)
                yield return (actualAttribute.GetLocation(),
                    $"{endpointName}: {actualAttribute.Name} attribute was added");
        }
    }

    private static IEnumerable<(Location, string)> CompareAttributes(AttributeSyntax actualAttribute,
        AttributeSyntax expectedAttribute, Location fallbackLocation, string endpointName, bool removeAllowed)
    {
        if (actualAttribute is null && !removeAllowed)
        {
            yield return (fallbackLocation,
                $"{endpointName}: {expectedAttribute.Name} attribute was removed");
            yield break;
        }

        if (expectedAttribute is null)
        {
            yield return (actualAttribute.GetLocation(),
                $"{endpointName}: {actualAttribute.Name} attribute was added");
            yield break;
        }

        if ((actualAttribute.ArgumentList?.Arguments.Count ?? 0) !=
            (expectedAttribute.ArgumentList?.Arguments.Count ?? 0))
            yield return (actualAttribute.GetLocation(),
                $"{endpointName}: {actualAttribute.Name} attribute arguments have changed");
        else
            for (var i = 0; i < actualAttribute.ArgumentList?.Arguments.Count; i++)
            {
                var actualArg = actualAttribute.ArgumentList?.Arguments[i].Expression
                    .GetFirstToken()
                    .ValueText;
                var expectedArg = expectedAttribute.ArgumentList?.Arguments[i].Expression
                    .GetFirstToken()
                    .ValueText;

                if (actualArg != expectedArg)
                    yield return (actualAttribute.GetLocation(),
                        $"{endpointName}: {actualAttribute.Name} attribute arguments have changed");
            }
    }

    private static IEnumerable<(Location, string)> CompareTypes(TypeDetails actual, TypeDetails expected,
        Location fallbackLocation, string endpointName)
    {
        if (actual.PropertyTypes is null && expected.PropertyTypes is null && actual.Type.Text != expected.Type.Text)
            yield return (actual.Type.GetLocation(),
                $"{endpointName}: {expected.Identifier.ValueText} changed from {expected.Type.Text} to {actual.Type.Text}");

        foreach (var propertyChange in CompareTypes(actual.PropertyTypes?.ToList(),
                     expected.PropertyTypes?.ToList(), fallbackLocation, endpointName))
            yield return propertyChange;

        foreach (var genericChange in CompareTypes(actual.GenericTypes?.ToList(), expected.GenericTypes?.ToList(),
                     fallbackLocation, endpointName))
            yield return genericChange;
    }
}