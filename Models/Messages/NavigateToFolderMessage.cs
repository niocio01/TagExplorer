namespace TagExplorer.Models.Messages;

/// <summary>
/// Asks the explorer to open the folder at the path.
/// </summary>
public sealed class NavigateToFolderMessage
{
    public NavigateToFolderMessage(string folderPath)
    {
        FolderPath = folderPath;
    }

    public string FolderPath { get; }
}
