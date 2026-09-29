namespace DinoSplicer.Api.Models;

public enum VideoFileError
{
    None,
    Empty,
    TooLarge,
    FileNameTooLong,
    ExtensionNotAllowed,
    ContentTypeNotAllowed
}