namespace CanXe.Domain.Services;

public readonly record struct ContentBoundsDip(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static ContentBoundsDip FromSize(double width, double height) =>
        new(0, 0, width, height);
}
