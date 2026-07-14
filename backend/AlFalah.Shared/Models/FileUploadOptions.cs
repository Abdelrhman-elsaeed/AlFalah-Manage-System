namespace AlFalah.Shared.Models;

/// <summary>
/// Bound from the <c>FileUploads</c> section of configuration. Centralizes
/// every size + content-type knob for file uploads so they can be tuned
/// per-environment via appsettings / env vars without code changes
/// (Phase 10 hardening).
/// </summary>
public class FileUploadOptions
{
    /// <summary>
    /// Maximum size (bytes) for a base64-encoded user signature data URL.
    /// Default 512 KiB (524288 bytes). The raw base64 string is roughly 4/3
    /// the size of the underlying PNG payload.
    /// </summary>
    public int SignatureMaxBytes { get; set; } = 512 * 1024;

    /// <summary>
    /// When true, every upload must pass magic-byte validation against its
    /// declared MIME type. Default true — production should keep this on.
    /// </summary>
    public bool ImageMagicBytesRequired { get; set; } = true;
}