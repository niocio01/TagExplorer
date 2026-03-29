namespace TagExplorer.Models.Messages;

public sealed class TagApplicationsChangedMessage
{
    public TagApplicationsChangedMessage(string? affectedPath = null)
    {
        AffectedPath = affectedPath;
    }

    public string? AffectedPath { get; }
}
