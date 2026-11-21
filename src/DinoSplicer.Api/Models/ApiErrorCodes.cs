namespace DinoSplicer.Api.Models;

public static class ApiErrorCodes
{
    public const string VideoNotFound = "VIDEO_NOT_FOUND";
    public const string VideoNotReady = "VIDEO_NOT_READY";
    public const string VideoProcessing = "VIDEO_PROCESSING";
    public const string VideoSplicing = "VIDEO_SPLICING";
    public const string VideoCutting = "VIDEO_CUTTING";
    public const string VideoCompressing = "VIDEO_COMPRESSING";
    public const string SpliceJobNotFound = "SPLICE_JOB_NOT_FOUND";
    public const string SpliceJobProcessing = "SPLICE_JOB_PROCESSING";
    public const string SpliceJobNotReady = "SPLICE_JOB_NOT_READY";
    public const string SpliceOutputNotFound = "SPLICE_OUTPUT_NOT_FOUND";
    public const string SpliceOutputFileMissing = "SPLICE_OUTPUT_FILE_MISSING";
    public const string CutJobNotFound = "CUT_JOB_NOT_FOUND";
    public const string CutJobProcessing = "CUT_JOB_PROCESSING";
    public const string CutJobNotReady = "CUT_JOB_NOT_READY";
    public const string CutOutputFileMissing = "CUT_OUTPUT_FILE_MISSING";
    public const string InvalidUpload = "INVALID_UPLOAD";
    public const string InvalidFileCount = "INVALID_FILE_COUNT";
    public const string FileEmpty = "FILE_EMPTY";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string FileNameTooLong = "FILE_NAME_TOO_LONG";
    public const string UnsupportedFileExtension = "UNSUPPORTED_FILE_EXTENSION";
    public const string UnsupportedContentType = "UNSUPPORTED_CONTENT_TYPE";
    public const string InvalidSpliceValue = "INVALID_SPLICE_VALUE";
    public const string InvalidCutRange = "INVALID_CUT_RANGE";
    public const string InvalidCutMode = "INVALID_CUT_MODE";
    public const string InvalidCompressionFormat = "INVALID_COMPRESSION_FORMAT";
    public const string InvalidCompressionCodec = "INVALID_COMPRESSION_CODEC";
    public const string InvalidCompressionPreset = "INVALID_COMPRESSION_PRESET";
    public const string IncompatibleContainerCodec = "INCOMPATIBLE_CONTAINER_CODEC";
    public const string InvalidCompressionCrf = "INVALID_COMPRESSION_CRF";
    public const string InvalidCompressionResolution = "INVALID_COMPRESSION_RESOLUTION";
    public const string InvalidCompressionAudioBitrate = "INVALID_COMPRESSION_AUDIO_BITRATE";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string MediaProcessingFailed = "MEDIA_PROCESSING_FAILED";
    public const string DatabaseError = "DATABASE_ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string RequestFailed = "REQUEST_FAILED";
    public const string InternalError = "INTERNAL_ERROR";

    public static string ForVideoFileError(VideoFileError reason)
    {
        return reason switch
        {
            VideoFileError.Empty => FileEmpty,
            VideoFileError.TooLarge => FileTooLarge,
            VideoFileError.FileNameTooLong => FileNameTooLong,
            VideoFileError.ExtensionNotAllowed => UnsupportedFileExtension,
            VideoFileError.ContentTypeNotAllowed => UnsupportedContentType,
            _ => InvalidUpload,
        };
    }
}
