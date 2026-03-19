namespace TagExplorer.Models;

public sealed class CompactTagDefinition
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? ShortCode { get; init; }
    public string? IconName { get; init; }
    public string? ColorHex { get; init; }
}
