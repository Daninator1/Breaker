using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Breaker.Logic;

public class TypeDetails
{
    public string Namespace { get; set; }
    public SyntaxToken Identifier { get; set; }
    public SyntaxToken Type { get; set; }
    public bool IsNullable { get; set; }
    public bool IsSimpleType => this.Type.Text is "int" or "string" or "bool" or "float" or "double" or "decimal";
    public TypeDetails BaseType { get; set; }
    public IEnumerable<TypeDetails> GenericTypes { get; set; }
    public IEnumerable<TypeDetails> PropertyTypes { get; set; }
    public IEnumerable<AttributeSyntax> Attributes { get; set; }
}