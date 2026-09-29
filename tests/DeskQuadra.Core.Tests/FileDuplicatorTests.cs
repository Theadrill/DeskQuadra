using DeskQuadra.Core.FileDuplication;

namespace DeskQuadra.Core.Tests;

/// <summary>
/// Nome único + fallback do Desktop da duplicação física (antes zero cobertura).
/// Usa diretórios temporários reais e costuras de I/O do <see cref="FileDuplicator"/>
/// para simular diretório protegido sem tocar no Desktop real.
/// </summary>
public sealed class FileDuplicatorTests : IDisposable
{
    private const string Suffix = " - Cópia";
    private const string IndexedFormat = " - Cópia ({0})";

    private readonly TempDirectory _temp = new("DeskQuadra_FileDuplicator_");
    private string _root => _temp.Path;

    public void Dispose() => _temp.Dispose();

    private string NewDir(string name)
    {
        string dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string BuildFileName(string stem, string ext, int i) =>
        i == 1 ? $"{stem}{Suffix}{ext}" : $"{stem}{string.Format(IndexedFormat, i)}{ext}";

    [Fact]
    public void GetUniquePath_File_ReturnsFirstCopyWhenFree()
    {
        string dir = NewDir("free");
        int index = 1;
        string unique = FileDuplicator.GetUniquePath(dir, i => BuildFileName("a", ".txt", i), File.Exists, ref index);
        Assert.Equal(Path.Combine(dir, "a - Cópia.txt"), unique);
        Assert.Equal(1, index);
    }

    [Fact]
    public void GetUniquePath_File_SkipsExistingCopies()
    {
        string dir = NewDir("taken");
        File.WriteAllText(Path.Combine(dir, "a - Cópia.txt"), "x");
        File.WriteAllText(Path.Combine(dir, "a - Cópia (2).txt"), "x");

        int index = 1;
        string unique = FileDuplicator.GetUniquePath(dir, i => BuildFileName("a", ".txt", i), File.Exists, ref index);
        Assert.Equal(Path.Combine(dir, "a - Cópia (3).txt"), unique);
        Assert.Equal(3, index);
    }

    [Fact]
    public void GetUniquePath_Directory_SkipsExistingCopies()
    {
        string parent = NewDir("dirs");
        Directory.CreateDirectory(Path.Combine(parent, "Pasta - Cópia"));

        int index = 1;
        string unique = FileDuplicator.GetUniquePath(
            parent,
            i => i == 1 ? $"Pasta{Suffix}" : $"Pasta{string.Format(IndexedFormat, i)}",
            Directory.Exists,
            ref index);
        Assert.Equal(Path.Combine(parent, "Pasta - Cópia (2)"), unique);
        Assert.Equal(2, index);
    }

    [Fact]
    public void GetUniquePath_FallbackContinuesIndexFromPrimary()
    {
        // Reproduz a continuação do copyIndex entre tentativa primária e fallback:
        // primário ocupou " - Cópia" e " - Cópia (2)", logo o fallback começa em (3).
        string primary = NewDir("primary");
        string desktop = NewDir("desktop");
        File.WriteAllText(Path.Combine(primary, "a - Cópia.txt"), "x");
        File.WriteAllText(Path.Combine(primary, "a - Cópia (2).txt"), "x");
        File.WriteAllText(Path.Combine(desktop, "a - Cópia (3).txt"), "x");

        int index = 1;
        _ = FileDuplicator.GetUniquePath(primary, i => BuildFileName("a", ".txt", i), File.Exists, ref index);
        Assert.Equal(3, index);
        string fallback = FileDuplicator.GetUniquePath(desktop, i => BuildFileName("a", ".txt", i), File.Exists, ref index);
        Assert.Equal(Path.Combine(desktop, "a - Cópia (4).txt"), fallback);
    }

    [Fact]
    public void Duplicate_File_CopiesWithCopySuffix()
    {
        string dir = NewDir("file");
        string source = Path.Combine(dir, "doc.txt");
        File.WriteAllText(source, "conteúdo");
        string desktop = NewDir("desk1");

        string duplicated = FileDuplicator.Duplicate(source, Suffix, IndexedFormat, desktop);

        Assert.Equal(Path.Combine(dir, "doc - Cópia.txt"), duplicated);
        Assert.Equal("conteúdo", File.ReadAllText(duplicated));
    }

    [Fact]
    public void Duplicate_File_IncrementsWhenCopyExists()
    {
        string dir = NewDir("file2");
        string source = Path.Combine(dir, "doc.txt");
        File.WriteAllText(source, "a");
        File.WriteAllText(Path.Combine(dir, "doc - Cópia.txt"), "ocupado");
        string desktop = NewDir("desk2");

        string duplicated = FileDuplicator.Duplicate(source, Suffix, IndexedFormat, desktop);

        Assert.Equal(Path.Combine(dir, "doc - Cópia (2).txt"), duplicated);
    }

    [Fact]
    public void Duplicate_File_FallsBackToDesktopWhenPrimaryCopyFails()
    {
        string dir = NewDir("protected");
        string source = Path.Combine(dir, "doc.txt");
        File.WriteAllText(source, "a");
        string desktop = NewDir("desk3");

        int calls = 0;
        string duplicated = FileDuplicator.Duplicate(
            source, Suffix, IndexedFormat, desktop,
            copyFile: (s, d) =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new UnauthorizedAccessException("protegido");
                }

                File.Copy(s, d);
            });

        Assert.Equal(2, calls);
        Assert.Equal(Path.Combine(desktop, "doc - Cópia.txt"), duplicated);
        Assert.Equal("a", File.ReadAllText(duplicated));
    }

    [Fact]
    public void Duplicate_File_ReturnsOriginalWhenFallbackAlsoFails()
    {
        string dir = NewDir("denied");
        string source = Path.Combine(dir, "doc.txt");
        File.WriteAllText(source, "a");
        string desktop = NewDir("desk4");

        string duplicated = FileDuplicator.Duplicate(
            source, Suffix, IndexedFormat, desktop,
            copyFile: static (s, d) => throw new UnauthorizedAccessException());

        Assert.Equal(source, duplicated);
    }

    [Fact]
    public void Duplicate_Directory_CopiesRecursively()
    {
        string parent = NewDir("parent");
        string source = Path.Combine(parent, "Pasta");
        Directory.CreateDirectory(Path.Combine(source, "Sub"));
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        File.WriteAllText(Path.Combine(source, "Sub", "b.txt"), "b");
        string desktop = NewDir("desk5");

        string duplicated = FileDuplicator.Duplicate(source, Suffix, IndexedFormat, desktop);

        Assert.Equal(Path.Combine(parent, "Pasta - Cópia"), duplicated);
        Assert.Equal("a", File.ReadAllText(Path.Combine(duplicated, "a.txt")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(duplicated, "Sub", "b.txt")));
    }

    [Fact]
    public void Duplicate_Directory_FallsBackToDesktopWhenPrimaryCopyFails()
    {
        string parent = NewDir("parent6");
        string source = Path.Combine(parent, "Pasta");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        string desktop = NewDir("desk6");

        int calls = 0;
        string duplicated = FileDuplicator.Duplicate(
            source, Suffix, IndexedFormat, desktop,
            copyDirectory: (s, d) =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new UnauthorizedAccessException("protegido");
                }

                FileDuplicator.CopyDirectoryRecursively(s, d);
            });

        Assert.Equal(2, calls);
        Assert.Equal(Path.Combine(desktop, "Pasta - Cópia"), duplicated);
        Assert.Equal("a", File.ReadAllText(Path.Combine(duplicated, "a.txt")));
    }

    [Fact]
    public void Duplicate_MissingPath_ReturnsOriginal()
    {
        string missing = Path.Combine(NewDir("missing"), "nao-existe.txt");
        string desktop = NewDir("desk7");

        Assert.Equal(missing, FileDuplicator.Duplicate(missing, Suffix, IndexedFormat, desktop));
    }

    [Fact]
    public void CopyDirectoryRecursively_CopiesNestedStructure()
    {
        string parent = NewDir("recur");
        string source = Path.Combine(parent, "src");
        Directory.CreateDirectory(Path.Combine(source, "Sub"));
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        string target = Path.Combine(parent, "dst");

        FileDuplicator.CopyDirectoryRecursively(source, target);

        Assert.Equal("a", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.True(Directory.Exists(Path.Combine(target, "Sub")));
    }
}
