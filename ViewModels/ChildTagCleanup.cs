using System.Windows;
using TagExplorer.Models;
using TagExplorer.Services;

namespace TagExplorer.ViewModels;

/// <summary>
/// Asks what happens to the auto-created child tags of child-tag rules that are about to be removed or replaced.
/// </summary>
public static class ChildTagCleanup
{
    /// <summary>
    /// Returns the child tags to delete after the assignments are gone (empty if none or the user keeps them),
    /// or null if the user cancelled.
    /// </summary>
    public static IReadOnlyList<Tag>? AskWhichToDelete(DataCachingService dcs, IEnumerable<TagAssignment> removedAssignments)
    {
        var rules = removedAssignments.Where(a => a.Kind == AssignmentKind.AutoDirectChildrenAsChildTag).ToList();
        var childTags = rules
            .SelectMany(dcs.GetUnusedChildTagsOfRule)
            .DistinctBy(tag => tag.Id)
            .ToList();

        if (childTags.Count == 0)
        {
            return [];
        }

        var folderNames = string.Join(", ", rules.Select(rule => $"\"{System.IO.Path.GetFileName(rule.TargetPath)}\""));
        var result = MessageBox.Show(
            $"Also delete the {childTags.Count} tag(s) created for the subfolders of {folderNames}? Otherwise they stay as normal tags.",
            "Remove tag", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.No);

        return result switch
        {
            MessageBoxResult.Cancel => null,
            MessageBoxResult.Yes => childTags,
            _ => []
        };
    }

    public static void Delete(DataCachingService dcs, IEnumerable<Tag> childTags)
    {
        foreach (var childTag in childTags)
        {
            if (childTag.Id is int tagId)
            {
                dcs.DeleteTagWithChildren(tagId);
            }
        }
    }
}
