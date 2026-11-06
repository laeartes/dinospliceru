namespace DinoSplicer.Api.Models;

public enum CutMode
{
    // Starts at 1 so an unset (default 0) mode fails validation instead of silently meaning FastCopy
    FastCopy = 1,
    FrameAccurate = 2
}
