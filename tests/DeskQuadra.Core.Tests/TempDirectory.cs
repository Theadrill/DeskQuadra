namespace DeskQuadra.Core.Tests;

/// <summary>
/// Fixture de diretório temporário isolado por teste (GUID único).
/// Cleanup best-effort: nunca deve falhar a suite.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory(string prefix = "DeskQuadraTests_")
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // Best-effort: temp de teste nunca deve falhar a suite.
        }
    }
}
