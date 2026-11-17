using System.Text;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// The real filesystem.
    /// </summary>
    public sealed class PhysicalFileSystem : IFileSystem
    {
        /// <inheritdoc />
        public char DirectorySeparatorChar => Path.DirectorySeparatorChar;

        /// <inheritdoc />
        public string Combine(params string[] paths)
        {
            return Path.Combine(paths);
        }

        /// <inheritdoc />
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        /// <inheritdoc />
        public bool DirectoryExists(string? path)
        {
            return Directory.Exists(path);
        }

        /// <inheritdoc />
        public void MoveDirectory(string source, string destination)
        {
            Directory.Move(source, destination);
        }

        /// <inheritdoc />
        public Task WriteAllTextAsync(
            string path,
            string contents,
            Encoding encoding,
            CancellationToken cancellationToken
        )
        {
            return File.WriteAllTextAsync(path, contents, encoding, cancellationToken);
        }

        /// <inheritdoc />
        public long GetAvailableFreeBytes(string path)
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            return string.IsNullOrEmpty(root) ? 0 : new DriveInfo(root).AvailableFreeSpace;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> EnumerateDirectories(string path)
        {
            return Directory.Exists(path) ? Directory.EnumerateDirectories(path).ToList() : [];
        }

        /// <inheritdoc />
        public DateTime GetLastWriteTimeUtc(string path)
        {
            return Directory.GetLastWriteTimeUtc(path);
        }

        /// <inheritdoc />
        public void DeleteDirectory(string path, bool recursive)
        {
            Directory.Delete(path, recursive);
        }
    }
}
