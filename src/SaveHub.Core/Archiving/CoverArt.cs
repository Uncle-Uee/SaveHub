namespace SaveHub.Core.Archiving;

/// <summary>Downloaded cover-art image content and its file extension (e.g. ".jpg").</summary>
public readonly record struct CoverArt(byte[] Content, string Extension);
