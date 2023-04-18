namespace PersistenceFramework.Abstractions;

public interface IMappingBuilder
{
    void AddMetadata(IMetadata metadata);
}

public class MappingBuilder : IMappingBuilder
{
    private readonly List<IMetadata> metadataCollection = new();

    public void AddMetadata(IMetadata metadata)
    {
        metadataCollection.Add(metadata);
    }
}

public static class MappingBuilderExtensions
{
    public static IMappingBuilder ToTable(this IMappingBuilder builder, string table)
    {
        builder.AddMetadata(new TableMetadata(table));
        return builder;
    }
}

public interface IMetadata { }

public sealed class TableMetadata : IMetadata
{
    public TableMetadata(string name)
    {
        Name = name;
    }

    public string Name { get; }
}