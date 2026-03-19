using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using TagExplorer.Models;
using TagExplorer.Services;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.ViewModels;

public partial class ItemDetails_VM : ObservableObject
{
    private readonly TagAssignmentService? _tagAssignmentService;

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

    public ItemDetails_VM()
        : this(App.AppHost?.Services.GetService<TagAssignmentService>())
    {
    }

    public ItemDetails_VM(TagAssignmentService? tagAssignmentService)
    {
        _tagAssignmentService = tagAssignmentService;
    }

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

        if (_tagAssignmentService?.RemoveManualTag(SelectedItem, tag) != true)
        {
            return;
        }

        ReloadSelectedItemTags();
    }

    private bool AddTagToSelectedItem(FilterTag tag)
    {
        var added = _tagAssignmentService?.TryAssignManualTag(SelectedItem, tag) == true;
        if (added)
        {
            ReloadSelectedItemTags();
        }

        return added;
    }

    partial void OnSelectedItemChanged(ExplorerItem? value)
    {
        ReloadSelectedItemTags();
    }

    private void ReloadSelectedItemTags()
    {
        SelectedItemTags.Clear();

        if (_tagAssignmentService is null)
        {
            return;
        }

        var tags = _tagAssignmentService.GetManualTagsForItem(SelectedItem);
        foreach (var tag in tags)
        {
            SelectedItemTags.Add(tag);
        }
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
