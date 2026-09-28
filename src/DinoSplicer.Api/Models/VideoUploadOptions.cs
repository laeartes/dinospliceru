namespace DinoSplicer.Api.Models;

public class VideoUploadOptions
{
    public const string SectionName = "VideoUpload";

    public string StoragePath {get; set;} = "storage";

    public long MaxFileSizeBytes {get; set;}

    public string[] AllowedExtensions {get; set;} = [];
    
    public string[] AllowedContentTypes {get; set;} = [];
}