namespace DinoSplicer.Api.Models;

public readonly struct Resolution
{
    public int Width { get; }
    public int Height { get; }

    public Resolution(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
    }

    public override string ToString() => $"{Width}x{Height}";
}
