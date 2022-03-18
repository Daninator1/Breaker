using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Breaker.Logic;

public static class Extensions
{
    private static readonly string[] SuccessStatusCodeEnums = new[]
    {
        "HttpStatusCode.OK",
        "HttpStatusCode.Created",
        "HttpStatusCode.Accepted",
        "HttpStatusCode.NonAuthoritativeInformation",
        "HttpStatusCode.NoContent",
        "HttpStatusCode.ResetContent",
        "HttpStatusCode.PartialContent",
        "HttpStatusCode.MultiStatus",
        "HttpStatusCode.AlreadyReported",
        "HttpStatusCode.IMUsed"
    };

    private static string GetNamespace(this SyntaxNode node,
        IReadOnlyCollection<ClassDeclarationSyntax> classDeclarations)
    {
        if (node is PredefinedTypeSyntax) return null;
        if (node is ClassDeclarationSyntax)
            return node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();

        if (node is QualifiedNameSyntax qn)
        {
            var namespacePrefix = qn.Left.ToString();
            var aliasNamespace = node.Ancestors().OfType<CompilationUnitSyntax>().Single().Usings
                .SingleOrDefault(u => u.Alias?.Name.ToString() == namespacePrefix);

            return aliasNamespace is null ? namespacePrefix : aliasNamespace.Name.ToString();
        }

        var nodeIdentifier = (node as TypeDeclarationSyntax)?.Identifier.ValueText ??
                             (node as GenericNameSyntax)?.Identifier.ValueText ??
                             (node as IdentifierNameSyntax)?.Identifier.ValueText;

        if (nodeIdentifier is null)
            return node?.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().SingleOrDefault()?.Name.ToString();

        var possibleClasses = classDeclarations.Where(c => c.Identifier.ValueText == nodeIdentifier).ToList();

        if (possibleClasses.Count > 1)
        {
            var classNamespace = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().SingleOrDefault();

            var classNamespaceText = classNamespace?.Name.ToString();

            var possibleClassesAndNamespaces = possibleClasses.Select(pc => (possibleClass: pc,
                possibleNamespace: pc.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().SingleOrDefault()?.Name
                    .ToString())).ToList();

            string possibleNamespace = null;

            // Check classes in parent namespaces
            while (classNamespaceText is not null && string.IsNullOrWhiteSpace(possibleNamespace))
            {
                possibleNamespace = possibleClassesAndNamespaces
                    .SingleOrDefault(x => x.possibleNamespace == classNamespaceText).possibleNamespace;
                classNamespaceText = classNamespaceText.Contains('.') ? classNamespaceText[..classNamespaceText.LastIndexOf('.')] : null;
            }

            // Check usings
            if (possibleNamespace is null)
            {
                var compilationUsings = node.Ancestors().OfType<CompilationUnitSyntax>().Single().Usings
                    .Select(u => u.Name.ToString());

                var usings = compilationUsings.Concat(classNamespace.Usings.Select(u => u.Name.ToString()));

                return possibleClassesAndNamespaces.Single(x => usings.Contains(x.possibleNamespace)).possibleNamespace;
            }

            return possibleNamespace;
        }
        else
        {
            var classDeclaration = possibleClasses.SingleOrDefault();

            return classDeclaration?.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().SingleOrDefault()?.Name
                .ToString();
        }
    }

    public static EndpointDetails ToEndpointDetails(this MethodDeclarationSyntax method,
        string projectName,
        AttributeArgumentSyntax baseRoute,
        AttributeSyntax baseAuthorization,
        IReadOnlyCollection<ClassDeclarationSyntax> classDeclarations)
    {
        var httpMethods = GetHttpMethods(method).ToList();

        var controller = method.Ancestors().OfType<ClassDeclarationSyntax>().SingleOrDefault();

        if (controller is null) return null;
        
        var version = controller.AttributeLists.SelectMany(al => al.Attributes)
            .SingleOrDefault(a => a.Name.ToString() == "ApiVersion")?.ArgumentList?.Arguments.SingleOrDefault()?.Expression.GetFirstToken().ValueText;

        var httpMethodSpecificRoutes = GetHttpMethodSpecificRoutes(httpMethods, controller, method, version);
        var specificAuthorization = GetSpecificAuthorization(method);

        var successResponseTypes = GetSuccessResponseTypes(method).ToList();
        var returnTypes = successResponseTypes.Any() ? successResponseTypes : new List<TypeSyntax> { method.ReturnType };

        var result = new EndpointDetails
        {
            ProjectName = projectName,
            Namespace = method.GetNamespace(classDeclarations),
            Identifier = method.Identifier,
            Controller = controller,
            BaseRoute = new RouteDetails(baseRoute, controller?.Identifier.ValueText, method.Identifier.ValueText, version),
            HttpMethodSpecificRoutes = httpMethodSpecificRoutes,
            BaseAuthorization = baseAuthorization,
            SpecificAuthorization = specificAuthorization,
            ReturnTypes = returnTypes.Select(rt => rt is GenericNameSyntax x
                ? x.GetTypeDetails(classDeclarations)
                : rt.GetTypeDetails(classDeclarations)),
            Attributes = method.AttributeLists.SelectMany(al => al.Attributes)
        };

        if (!method.ParameterList.Parameters.Any()) return result;

        var parameters = new List<TypeDetails>();

        foreach (var parameter in method.ParameterList.Parameters)
            if (parameter.Type is GenericNameSyntax p)
                parameters.Add(p.GetTypeDetails(classDeclarations));
            else
                parameters.Add(parameter.Type.GetTypeDetails(classDeclarations));

        result.ParameterTypes = parameters;

        return result;
    }

    private static IEnumerable<TypeSyntax> GetSuccessResponseTypes(MethodDeclarationSyntax method)
        => method.AttributeLists
            .SelectMany(a => a.Attributes)
            .Where(a => a.Name.ToString() == "ProducesResponseType")
            .Select(a => (a.ArgumentList?.Arguments.FirstOrDefault()?.Expression, a.ArgumentList?.Arguments.LastOrDefault()?.Expression))
            .Where(tuple => tuple.Item1 is TypeOfExpressionSyntax && tuple.Item2 is ExpressionSyntax)
            .Where(tuple =>
                (SuccessStatusCodeEnums.Any(code => tuple.Item2.ToString().Contains(code)) ||
                 int.TryParse(tuple.Item2.ToString(), out var actualCode) &&
                 actualCode is >= 200 and <= 299))
            .Select(tuple => ((TypeOfExpressionSyntax)tuple.Item1).Type);

    private static TypeDetails GetTypeDetails(this SyntaxNode node,
        IReadOnlyCollection<ClassDeclarationSyntax> classDeclarations)
    {
        var result = new TypeDetails
        {
            Namespace = node.GetNamespace(classDeclarations),
            Type = node switch
            {
                GenericNameSyntax gn => gn.Identifier,
                QualifiedNameSyntax qn => qn.Right.Identifier,
                _ => node.GetFirstToken()
            },
            IsNullable = node is NullableTypeSyntax
        };
        
        switch (node.Parent)
        {
            case PropertyDeclarationSyntax propertyParent:
                result.Attributes = propertyParent.AttributeLists.SelectMany(al => al.Attributes);
                result.Identifier = propertyParent.Identifier;
                break;
            case ParameterSyntax parameterParent:
                result.Attributes = parameterParent.AttributeLists.SelectMany(al => al.Attributes);
                result.Identifier = parameterParent.Identifier;
                break;
        }
        
        var identifierOverrideArgument = result.Attributes?
            .Where(a => a.Name.ToString().StartsWith("From") && a.ArgumentList is not null)
            .SelectMany(a => a.ArgumentList.Arguments)
            .SingleOrDefault(a
                => a.NameEquals is not null)
            ?.Expression.GetFirstToken();

        if (identifierOverrideArgument is not null) result.Identifier = identifierOverrideArgument.Value;

        var classDeclaration = classDeclarations
            .SingleOrDefault(
                c => $"{c.GetNamespace(classDeclarations)}.{c.Identifier.ValueText}" == $"{result.Namespace}.{result.Type.Text}");

        var propertyTypes = classDeclaration?
            .DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .Where(p => p.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            .Select(p => p.Type)
            .ToList() ?? new List<TypeSyntax>();

        if (propertyTypes.Any())
            result.PropertyTypes =
                propertyTypes.Select(propertyType => propertyType.GetTypeDetails(classDeclarations));

        var baseTypes = classDeclaration?.BaseList?.Types.ToList();
        result.BaseType = baseTypes?.Select(bt => bt.GetTypeDetails(classDeclarations))
            .SingleOrDefault(bt => bt.PropertyTypes != null);

        if (!(node is GenericNameSyntax genericType) || !genericType.TypeArgumentList.Arguments.Any())
            return result;

        result.GenericTypes =
            genericType.TypeArgumentList.Arguments.Select(innerType => innerType.GetTypeDetails(classDeclarations));

        return result;
    }

    private static IEnumerable<AttributeSyntax> GetHttpMethods(MethodDeclarationSyntax method)
    {
        return method.AttributeLists.SelectMany(a => a.Attributes)
            .Where(a =>
                a.Name.ToString() == "HttpGet" || a.Name.ToString() == "HttpPost" ||
                a.Name.ToString() == "HttpPut" || a.Name.ToString() == "HttpDelete" ||
                a.Name.ToString() == "HttpPatch" || a.Name.ToString() == "HttpOptions" ||
                a.Name.ToString() == "HttpHead");
    }

    private static IEnumerable<(AttributeSyntax, RouteDetails)> GetHttpMethodSpecificRoutes(
        IReadOnlyCollection<AttributeSyntax> httpMethods,
        ClassDeclarationSyntax controller,
        MethodDeclarationSyntax method,
        string version)
    {
        var routeAttribute = method.AttributeLists.SelectMany(al => al.Attributes)
            .SingleOrDefault(a => a.Name.ToString() == "Route")?.ArgumentList?.Arguments.FirstOrDefault();

        if (!httpMethods.Any()) yield return (null, new RouteDetails(routeAttribute, controller.Identifier.ValueText, method.Identifier.ValueText, version));

        foreach (var httpMethod in httpMethods)
        {
            var specificRoute = httpMethod.ArgumentList?.Arguments.FirstOrDefault(a
                => a.NameEquals is null || a.NameEquals.ToString() == "template") ?? routeAttribute;

            yield return (httpMethod, new RouteDetails(specificRoute, controller.Identifier.ValueText, method.Identifier.ValueText, version));
        }
    }

    private static AttributeSyntax GetSpecificAuthorization(MethodDeclarationSyntax method)
    {
        var methodAttributes = method.AttributeLists.SelectMany(al => al.Attributes);
        var authorizeAttribute = methodAttributes.SingleOrDefault(a => a.Name.ToString() == "Authorize");
        return authorizeAttribute;
    }
}