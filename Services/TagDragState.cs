namespace TagExplorer.Services;

public static class TagDragState
{
    public static event EventHandler<bool>? IsDraggingChanged;

    public static bool IsDragging { get; private set; }

    public static void SetDragging(bool isDragging)
    {
        if (IsDragging == isDragging)
        {
            return;
        }

        IsDragging = isDragging;
        IsDraggingChanged?.Invoke(null, IsDragging);
    }
}
