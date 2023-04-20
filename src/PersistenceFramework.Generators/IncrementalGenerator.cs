using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PersistenceFramework.Abstractions;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace PersistenceFramework.Generators;

[Generator(LanguageNames.CSharp)]
public class IncrementalGenerator : IncrementalGeneratorBase<MappingsSyntaxProcessor> { }

public class IncrementalGeneratorBase<T> : IIncrementalGenerator
    where T: MappingsSyntaxProcessorBase, new()
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var mappingsSyntaxProcessor = new T();

        var properties = context.SyntaxProvider.CreateSyntaxProvider(
                mappingsSyntaxProcessor.IsMappingMethod,
                mappingsSyntaxProcessor.Transform)
            .Collect();

        var compilation = context.CompilationProvider.Combine(properties);

        context.RegisterSourceOutput(compilation,
                (spc, source) => mappingsSyntaxProcessor.Execute(source.Left, source.Right, spc));
    }
}

internal class GeneratorMappingBuilder : IMappingBuilder
{
    internal List<IMetadata> MetadataCollection { get; } = new();

    public void AddMetadata(IMetadata metadata)
    {
        MetadataCollection.Add(metadata);
    }
}

public abstract class MappingsSyntaxProcessorBase
{
    private readonly MethodInfo[] mappingMethods;

    protected MappingsSyntaxProcessorBase(Type extensionType)
    {
        // get all the mapping methods,
        // that is, static methods using this keywork on first parameter,
        // where the first parameter is of type IMappingBuilder
        mappingMethods = extensionType.GetMethods()
            .Where(m => m.IsStatic && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(IMappingBuilder))
            .ToArray();
    }

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

    public IEnumerable<IMetadata> Transform(GeneratorSyntaxContext context, CancellationToken _)
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

        // create a mapping builder to get the metadata
        var mappingBuilder = new GeneratorMappingBuilder();

        // method parameters
        var invoketionParameters = new object[parameters.Length];
        invoketionParameters[0] = mappingBuilder;

        if (parameters.Length > 1)
        {
            var argumentsArray = arguments.Select(ConvertArgumentToString).ToArray();
            var startIndex = parameters.Length == argumentsArray.Length ? 1 : 0;
            Array.Copy(argumentsArray, startIndex, invoketionParameters, 1, invoketionParameters.Length - 1);
        }

        // invoke the mapping method
        methodInfo.Invoke(null, invoketionParameters);

        // return the mapping builder
        return mappingBuilder.MetadataCollection;
    }

    private static string ConvertArgumentToString(ArgumentSyntax argument)
    {
        // get the argument value
        var value = argument.Expression.ToString();
        return value;
    }

    public abstract void Execute(
        Compilation compilation,
        ImmutableArray<IEnumerable<IMetadata>> properties,
        SourceProductionContext context);
}

public class MappingsSyntaxProcessor : MappingsSyntaxProcessorBase
{
    private static Dictionary<Type, MetadataGeneratorHandler> handlers = new()
    {
        { typeof(TableMetadata), new TableMetadataGeneratorHandler() }
    };

    public MappingsSyntaxProcessor() : base(typeof(MappingBuilderExtensions)) { }

    public override void Execute(
        Compilation compilation, 
        ImmutableArray<IEnumerable<IMetadata>> properties, 
        SourceProductionContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace PersistenceFramework.Generated");
        sb.AppendLine("{");
        sb.AppendLine("public static class GeneratedMappings");
        sb.AppendLine("{");

        foreach (var metadata in properties.SelectMany(p => p))
        {
            var handler = handlers[metadata.GetType()];
            handler.Generate(sb, metadata);
        }

        sb.AppendLine("}");
        sb.AppendLine("}");

        var source = SourceText.From(sb.ToString(), Encoding.UTF8);

        // write the source
        context.AddSource($"PersistenceFrameworkGeneratedMappings.g.cs", source);
    }
}

public abstract class MetadataGeneratorHandler
{
    public abstract void Generate(StringBuilder sb, IMetadata metadata);
}

public sealed class TableMetadataGeneratorHandler : MetadataGeneratorHandler
{
    public override void Generate(StringBuilder sb, IMetadata metadata)
    {
        var tableMetadata = (TableMetadata)metadata;

        sb.AppendLine($"public const string TableName_{tableMetadata.Name} = \"{tableMetadata.Name}\";");
    }
}