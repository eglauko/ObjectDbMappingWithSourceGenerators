namespace PersistenceFramework.Generators.Tests.Tests;

[UsesVerify]
public class TableMetadataTests
{
    [Fact]
    public async Task Test1()
    {
        var source =
"""
namespace Tests
{
    public static class MyMapping
    {
        public static void Map(this IMappingBuilder builder)
        {
            builder.ToTable("MyTable");
        }
    }
}
""";

        await IncrementalGeneratorTestHelper.Verify(source);
    }
}
