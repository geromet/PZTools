namespace DataInput.Serialization;

/// <summary>
/// Abstraction over file I/O for testability.
/// Mirrors the ILuaLoader pattern used on the read side.
/// </summary>
public interface IFileWriter
{
    void WriteAllText(string path, string content);
    void Backup(string path);
}

/// <summary>
/// Default implementation that writes to disk and creates .bak backups.
/// </summary>
public sealed class DiskFileWriter : IFileWriter
{
    public void WriteAllText(string path, string content)
    {
        File.WriteAllText(path, content);
    }

    public void Backup(string path)
    {
        if (File.Exists(path))
            File.Copy(path, path + ".bak", overwrite: true);
    }
}
