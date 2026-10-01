using System.Globalization;

namespace AlFalah.Api.Testing;

/// <summary>
/// E2E-only clock controlled by the local runner through a private file.
/// It is registered only when ASPNETCORE_ENVIRONMENT=E2E and exposes no HTTP surface.
/// </summary>
public sealed class E2EFileTimeProvider : TimeProvider
{
    private readonly string _clockFile;

    public E2EFileTimeProvider(string clockFile)
    {
        _clockFile = Path.GetFullPath(clockFile);
        if (!File.Exists(_clockFile))
            throw new InvalidOperationException($"The E2E clock file does not exist: {_clockFile}");
    }

    public override DateTimeOffset GetUtcNow()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var stream = new FileStream(
                    _clockFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                var value = reader.ReadToEnd().Trim();
                if (DateTimeOffset.TryParse(
                        value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var instant))
                {
                    return instant;
                }
            }
            catch (IOException) when (attempt < 4)
            {
                // The Playwright process atomically replaces this file on Windows.
            }

            if (attempt < 4) Thread.Sleep(5);
        }

        throw new InvalidOperationException("The E2E clock file must contain an ISO-8601 UTC instant.");
    }
}
