namespace SaveHub.Core.Abstractions;

/// <summary>The outcome of a connectivity/authentication check against a storage backend.</summary>
public sealed class ConnectionTestResult
{
    /// <summary>True when the backend was reached, credentials are valid, and the target exists.</summary>
    public required bool Success { get; init; }

    /// <summary>Identity the credentials resolved to (e.g. GitHub login), when known.</summary>
    public string? AuthenticatedAs { get; init; }

    /// <summary>The target that was checked (e.g. "owner/repo"), when known.</summary>
    public string? Target { get; init; }

    /// <summary>Whether the authenticated user can write (push) to the target.</summary>
    public bool HasWriteAccess { get; init; }

    /// <summary>Whether auto-merge would actually take effect given access and settings.</summary>
    public bool AutoMergeEffective { get; init; }

    /// <summary>Human-readable status / error message.</summary>
    public required string Message { get; init; }
}
