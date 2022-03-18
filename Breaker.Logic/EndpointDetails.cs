using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Breaker.Logic;

public class EndpointDetails
{
    public string ProjectName { get; set; }
    public string Namespace { get; set; }
    public SyntaxToken Identifier { get; set; }
    public ClassDeclarationSyntax Controller { get; set; }

    public string FullName => $"{this.Namespace}.{this.Controller?.Identifier.ValueText}.{this.Identifier.ValueText}".Trim();
    public RouteDetails BaseRoute { get; set; }
    public IEnumerable<(AttributeSyntax, RouteDetails)> HttpMethodSpecificRoutes { get; set; }
    public AttributeSyntax BaseAuthorization { get; set; }
    public AttributeSyntax SpecificAuthorization { get; set; }
    public IEnumerable<TypeDetails> ReturnTypes { get; set; }
    public IEnumerable<TypeDetails> ParameterTypes { get; set; }
    public IEnumerable<AttributeSyntax> Attributes { get; set; }
}