namespace AppManagerForJava;

/// <summary>Keeps source bitmaps alive until the image list no longer needs them.</summary>
internal sealed class OwnedImageList : IDisposable
{
    private readonly List<Bitmap> sources = new();
    private bool disposed;
    public ImageList Value { get; } = new() { ImageSize = new Size(72, 72), ColorDepth = ColorDepth.Depth32Bit };

    // Ownership transfers to this object, including disposal if adding fails.
    public void Add(string key, Bitmap bitmap)
    {
        if (disposed) { bitmap.Dispose(); throw new ObjectDisposedException(nameof(OwnedImageList)); }
        sources.Add(bitmap);
        try { Value.Images.Add(key, bitmap); }
        catch { sources.Remove(bitmap); bitmap.Dispose(); throw; }
    }
    public void Clear()
    {
        if (disposed) throw new ObjectDisposedException(nameof(OwnedImageList));
        // ImageList can hold the originals until its native handle is created.
        // Remove those references before releasing the GDI+ bitmap objects.
        Value.Images.Clear();
        foreach (var bitmap in sources) bitmap.Dispose();
        sources.Clear();
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { Value.Dispose(); }
        finally { foreach (var bitmap in sources) bitmap.Dispose(); sources.Clear(); }
    }
}
