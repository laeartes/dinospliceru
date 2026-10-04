namespace DinoSplicer.Api.Models;

public class VideoUploadOptions
{
    public const string SectionName = "VideoUpload";

    public string StoragePath { get; set; } = "storage";

    public long MaxFileSizeBytes { get; set; }

    public int MetadataTimeoutSeconds { get; set; } = 30;

    public string[] AllowedExtensions { get; set; } = [];

    public string[] AllowedContentTypes { get; set; } = [];
}
