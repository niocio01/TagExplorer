using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using TagExplorer.Models;
using TagExplorer.Models.Messages;
using TagExplorer.Services;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.ViewModels;

public partial class ItemDetails_VM : ObservableObject
{
    private readonly TagAssignmentService? _tagAssignmentService;
    private readonly DataCachingService? _dcs;

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
    private ObservableCollection<AppliedTag> _selectedItemTags = [];

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
        : this(
              App.AppHost?.Services.GetService<TagAssignmentService>(),
              App.AppHost?.Services.GetService<DataCachingService>())
    {
    }

    public ItemDetails_VM(TagAssignmentService? tagAssignmentService, DataCachingService dataCachingService)
    {
        _tagAssignmentService = tagAssignmentService;
        _dcs = dataCachingService;

        WeakReferenceMessenger.Default.Register<TagApplicationsChangedMessage>(this, static (recipient, message) =>
        {
            ((ItemDetails_VM)recipient).HandleTagApplicationsChanged(message);
        });
    }

    private void HandleTagApplicationsChanged(TagApplicationsChangedMessage message)
    {
        if (SelectedItem is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(message.AffectedPath)
            && !string.Equals(message.AffectedPath, SelectedItem.Path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(ReloadSelectedItemTags);
            return;
        }

        ReloadSelectedItemTags();
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
        var mode = AssignmentMode.OnlyThisItem;
        if (dropData is TagDropRequest request)
        {
            dropData = request.Payload;
            mode = request.Mode;
        }

        Tag? tag = dropData switch
        {
            FilterTag filterTag => filterTag.Tag,
            Tag droppedTag => droppedTag,
            Tag_VM tagVm => _dcs?.GetTag(tagVm.Tag.Id) ?? new Tag(tagVm.Tag),
            _ => null
        };

        return tag is not null && AddTagToSelectedItem(tag, mode);
    }

    [RelayCommand]
    private void HandleTagListDropped(object? dropData)
    {
        TryAddDroppedTag(dropData);
    }

    [RelayCommand]
    private void HandleRemoveTag(object? tagData)
    {
        if (tagData is not AppliedTag { CanRemove: true } appliedTag)
        {
            return;
        }

        // Removing an assignment that reaches other items changes them too, so ask first.
        RemoveAssignment(appliedTag, confirm: AssignmentModes.ReachesOtherItems(appliedTag.Assignment));
    }

    private void RemoveAssignment(AppliedTag appliedTag, bool confirm)
    {
        if (_tagAssignmentService is null || _dcs is null)
        {
            return;
        }

        if (confirm)
        {
            var result = MessageBox.Show(
                $"Remove the tag \"{appliedTag.Name}\" from \"{appliedTag.SourcePath}\"? All items that get it from there lose it too.",
                "Remove tag", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _tagAssignmentService.RemoveAssignment(appliedTag.Assignment);

        ReloadSelectedItemTags();
    }

    private void ChangeMode(AppliedTag appliedTag, AssignmentMode mode)
    {
        if (_tagAssignmentService is null)
        {
            return;
        }

        // Only direct tags and rules offer kinds, and both sit on the selected item.
        if (_tagAssignmentService.ChangeMode(appliedTag.Assignment, mode, SelectedItem))
        {
            ReloadSelectedItemTags();
        }
    }

    private bool AddTagToSelectedItem(Tag tag, AssignmentMode mode)
    {
        var added = _tagAssignmentService?.AssignTag(SelectedItem, tag, mode) == true;
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
        if (SelectedItem is null)
            return;

        SelectedItemTags.Clear();

        if (_tagAssignmentService is null)
        {
            return;
        }

        if (_dcs is null)
        {
            return;
        }

        var tagApplications = _dcs.GetTagApplicationsForItem(SelectedItem);

        // Direct tags first, then rules on this folder, then tags inherited from folders above.
        foreach (var application in tagApplications.Where(app => !app.IsVirtual))
        {
            SelectedItemTags.Add(CreateAppliedTag(application.Tag, application.Assignment, AppliedTagKind.Direct));
        }

        if (SelectedItem is Folder folder)
        {
            foreach (var rule in _dcs.GetRulesOnFolder(folder.Path))
            {
                if (AssignmentModes.DisplayTagId(rule) is int tagId && _dcs.GetTag(tagId) is Tag ruleTag)
                {
                    SelectedItemTags.Add(CreateAppliedTag(ruleTag, rule, AppliedTagKind.Rule));
                }
            }
        }

        foreach (var application in tagApplications.Where(app => app.IsVirtual).OrderBy(app => app.Tag.Name, StringComparer.OrdinalIgnoreCase))
        {
            SelectedItemTags.Add(CreateAppliedTag(application.Tag, application.Assignment, AppliedTagKind.Virtual));
        }
    }

    private AppliedTag CreateAppliedTag(Tag tag, TagAssignment assignment, AppliedTagKind kind)
    {
        AppliedTag? appliedTag = null;

        // Only assignments on folders have more than one kind to choose from.
        var modeOptions = kind != AppliedTagKind.Virtual && assignment.TargetType == TargetType.Folder
            ? AssignmentModes.All
                .Select(mode => new AssignmentModeOption(
                    AssignmentModes.DisplayName(mode),
                    AssignmentModes.Of(assignment) == mode,
                    new RelayCommand(() => ChangeMode(appliedTag!, mode))))
                .ToList()
            : [];

        appliedTag = new AppliedTag
        {
            Tag = tag,
            Assignment = assignment,
            Kind = kind,
            ModeOptions = modeOptions,
            GoToSourceCommand = new RelayCommand(() =>
                WeakReferenceMessenger.Default.Send(new NavigateToFolderMessage(assignment.TargetPath))),
            RemoveFromSourceCommand = new RelayCommand(() => RemoveAssignment(appliedTag!, confirm: true))
        };

        return appliedTag;
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
        string[] units = ["B", "kB", "MB", "GB", "TB"];
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
