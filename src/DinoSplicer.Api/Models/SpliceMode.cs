namespace DinoSplicer.Api.Models;

public enum SpliceMode
{
    // Starts at 1 so an unset (default 0) mode fails validation instead of silently meaning ByLength
    ByLength = 1,
    BySize = 2
}
