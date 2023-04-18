using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PersistenceFramework.Abstractions;
using System.Reflection;

namespace PersistenceFramework.Generators;

[Generator(LanguageNames.CSharp)]
public class IncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var mappingsSyntaxProcessor = new MappingsSyntaxProcessor();

        context.SyntaxProvider.CreateSyntaxProvider(
            mappingsSyntaxProcessor.IsMappingMethod,
            mappingsSyntaxProcessor.Transform);

        throw new NotImplementedException();
    }
}

public class MappingsSyntaxProcessor
{
    private static readonly MethodInfo[] mappingMethods = typeof(MappingBuilderExtensions).GetMethods().ToArray();

    private readonly MappingBuilder mappingBuilder = new();

    public bool IsMappingMethod(SyntaxNode node, CancellationToken _)
    {
        // check if the node is a method call of one of the mapping methods
        if (node is InvocationExpressionSyntax invocation 
            && invocation.Expression is MemberAccessExpressionSyntax method)
        {
            var methodName = method.Name.ToString();
            return mappingMethods.Any(m => m.Name == methodName);
        }
        return false;
    }

    public IMappingBuilder Transform(GeneratorSyntaxContext context, CancellationToken _)
    {
        // get the method call
        var invocation = (InvocationExpressionSyntax)context.Node;
        var method = (MemberAccessExpressionSyntax)invocation.Expression;

        // get the method name
        var methodName = method.Name.ToString();

        // get the method info
        var methodInfo = mappingMethods.First(m => m.Name == methodName);

        // get the method parameters
        var parameters = methodInfo.GetParameters();

        // get the arguments
        var arguments = invocation.ArgumentList.Arguments;

        // method parameters
        var invoketionParameters = new object[parameters.Length + 1];
        invoketionParameters[0] = mappingBuilder;
        Array.Copy(arguments.Select(a => a.ToString()).ToArray(), 0, invoketionParameters, 1, parameters.Length);

        // invoke the mapping method
        methodInfo.Invoke(null, invoketionParameters);

        // return the mapping builder
        return mappingBuilder;
    }
}