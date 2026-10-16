namespace DinoSplicer.Api.Services;

// A splice failure whose message is meant to be shown to the user
public class SpliceException(string message) : Exception(message);
