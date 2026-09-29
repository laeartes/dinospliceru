namespace DinoSplicer.Api.Models;

public record VideoUploadResponse(Guid Id, string FileName, long SizeBytes, string Status);
