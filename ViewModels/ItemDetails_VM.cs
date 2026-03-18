using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using TagExplorer.Models;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.ViewModels;

public partial class ItemDetails_VM : ObservableObject
{
    private readonly Dictionary<string, List<FilterTag>> _tagsByItemKey = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedItemName))]
    [NotifyPropertyChangedFor(nameof(SelectedItemIconString))]
    [NotifyPropertyChangedFor(nameof(ItemPath))]
    [NotifyPropertyChangedFor(nameof(ItemSize))]
    [NotifyPropertyChangedFor(nameof(ItemType))]
    [NotifyPropertyChangedFor(nameof(ItemCreated))]
    [NotifyPropertyChangedFor(nameof(ItemLastModified))]
    private ExplorerItem? _selectedItem;

    [ObservableProperty]
    private ObservableCollection<FilterTag> _selectedItemTags = [];

    public string SelectedItemName => SelectedItem?.Name ?? "No Item Selected";

    public string SelectedItemIconString => SelectedItem?.IconString ?? "FileOutline";

    public string ItemPath => SelectedItem switch
    {
        null => "-",
        File file => file.FullPath ?? file.Name,
        Folder folder => folder.Path,
        _ => "-"
    };

    public string ItemSize => SelectedItem switch
    {
        File file => GetFileSizeDisplay(file.FullPath),
        Folder => "-",
        _ => "-"
    };

    public string ItemType => SelectedItem switch
    {
        null => "-",
        Folder => "Folder",
        File file => string.IsNullOrWhiteSpace(file.Extension)
            ? "File"
            : FileTypes.ByExtension.TryGetValue(file.Extension, out var fileType)
                ? fileType.DisplayString
                : $"{file.Extension.ToUpperInvariant()} File",
        _ => "-"
    };

    public string ItemCreated => GetDateDisplay(isCreated: true);

    public string ItemLastModified => GetDateDisplay(isCreated: false);

    [RelayCommand]
    private void CopyPath()
    {
        if (SelectedItem is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(ItemPath) && ItemPath != "-")
        {
            Clipboard.SetText(ItemPath);
        }
    }

    public bool TryAddDroppedTag(object? dropData)
    {
        return dropData switch
        {
            FilterTag filterTag => AddTagToSelectedItem(filterTag),
            Tag_VM tagVm => AddTagToSelectedItem(new FilterTag(tagVm.Tag)),
            _ => false
        };
    }

    [RelayCommand]
    private void HandleTagListDropped(object? dropData)
    {
        TryAddDroppedTag(dropData);
    }

    [RelayCommand]
    private void HandleRemoveTag(object? tagData)
    {
        if (tagData is not FilterTag tag)
        {
            return;
        }

        var key = GetItemKey(SelectedItem);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!_tagsByItemKey.TryGetValue(key, out var storedTags))
        {
            return;
        }

        var removedFromStore = storedTags.RemoveAll(existing =>
            string.Equals(existing.Name, tag.Name, StringComparison.OrdinalIgnoreCase)) > 0;

        if (!removedFromStore)
        {
            return;
        }

        for (var i = SelectedItemTags.Count - 1; i >= 0; i--)
        {
            if (string.Equals(SelectedItemTags[i].Name, tag.Name, StringComparison.OrdinalIgnoreCase))
            {
                SelectedItemTags.RemoveAt(i);
            }
        }
    }

    private bool AddTagToSelectedItem(FilterTag tag)
    {
        var key = GetItemKey(SelectedItem);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!_tagsByItemKey.TryGetValue(key, out var storedTags))
        {
            storedTags = [];
            _tagsByItemKey[key] = storedTags;
        }

        if (storedTags.Any(existing => string.Equals(existing.Name, tag.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var tagCopy = new FilterTag
        {
            Name = tag.Name,
            Description = tag.Description,
            IconName = tag.IconName,
            Color = tag.Color,
            Aliases = tag.Aliases,
            IsSystemTag = tag.IsSystemTag,
            Parent = tag.Parent,
            Children = tag.Children,
            FilterType = FilterTypes.None
        };

        storedTags.Add(tagCopy);
        SelectedItemTags.Add(tagCopy);
        return true;
    }

    partial void OnSelectedItemChanged(ExplorerItem? value)
    {
        SelectedItemTags.Clear();

        var key = GetItemKey(value);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!_tagsByItemKey.TryGetValue(key, out var storedTags))
        {
            return;
        }

        foreach (var tag in storedTags)
        {
            SelectedItemTags.Add(tag);
        }
    }

    private static string? GetItemKey(ExplorerItem? item)
    {
        return item switch
        {
            null => null,
            File file => file.FullPath,
            Folder folder => folder.Path,
            _ => null
        };
    }

    private string GetDateDisplay(bool isCreated)
    {
        try
        {
            DateTime value = SelectedItem switch
            {
                File file when !string.IsNullOrWhiteSpace(file.FullPath) => isCreated
                    ? System.IO.File.GetCreationTime(file.FullPath)
                    : System.IO.File.GetLastWriteTime(file.FullPath),
                Folder folder => isCreated
                    ? Directory.GetCreationTime(folder.Path)
                    : Directory.GetLastWriteTime(folder.Path),
                _ => DateTime.MinValue
            };

            return value == DateTime.MinValue
                ? "-"
                : value.ToString("g", CultureInfo.CurrentCulture);
        }
        catch
        {
            return "-";
        }
    }

    private static string GetFileSizeDisplay(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return "-";
        }

        try
        {
            var length = new FileInfo(fullPath).Length;
            return FormatBytes(length);
        }
        catch
        {
            return "-";
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} {units[unit]}"
            : $"{size:0.##} {units[unit]}";
    }
}
