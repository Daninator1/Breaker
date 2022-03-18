using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Breaker.Logic;

public class RouteDetails
{
    public RouteDetails(AttributeArgumentSyntax routeAttribute, string controllerName, string endpointName, string version)
    {
        this.RouteAttribute = routeAttribute?.ToString() == "\"\"" ? null : routeAttribute;
        var lastIndexOfControllerWord = controllerName.ToLower().LastIndexOf("controller", StringComparison.Ordinal);
        this.RouteText = this.RouteAttribute?.ToString()
            .Replace("[controller]",
                controllerName?[..(lastIndexOfControllerWord == -1 ? controllerName.Length : lastIndexOfControllerWord)] ?? string.Empty)
            .Replace("[action]", endpointName ?? string.Empty)
            .Replace("{version:apiVersion}", version ?? string.Empty)
            .Replace("\"", "")
            .ToLower();
    }

    public AttributeArgumentSyntax RouteAttribute { get; }
    public string RouteText { get; }
}