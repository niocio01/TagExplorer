namespace TagExplorer.Models;

public sealed class CompactTagToken
{
    public required string Text { get; init; }
    public string? ColorHex { get; init; }
    public string? Tooltip { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsOverflow { get; init; }
}
