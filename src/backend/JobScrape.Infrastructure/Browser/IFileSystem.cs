using System.Text;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// The filesystem calls the browser layer needs: combining paths,
    /// creating, probing, and moving directories, and writing captures. A
    /// port so the session pool and the artifact store are testable without
    /// touching disk.
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// Gets the platform's directory separator.
        /// </summary>
        char DirectorySeparatorChar { get; }

        /// <summary>
        /// Combines path segments.
        /// </summary>
        /// <param name="paths">The segments. Must not be null.</param>
        /// <returns>The combined path.</returns>
        string Combine(params string[] paths);

        /// <summary>
        /// Creates a directory and any missing parents. A no-op when it exists.
        /// </summary>
        /// <param name="path">The directory. Must not be null.</param>
        void CreateDirectory(string path);

        /// <summary>
        /// Checks a directory exists. Null or missing reads as false.
        /// </summary>
        /// <param name="path">The directory.</param>
        /// <returns>True when the directory exists.</returns>
        bool DirectoryExists(string? path);

        /// <summary>
        /// Moves a directory.
        /// </summary>
        /// <param name="source">The directory to move. Must not be null.</param>
        /// <param name="destination">The new location. Must not be null.</param>
        void MoveDirectory(string source, string destination);

        /// <summary>
        /// Writes text to a file, creating or overwriting it.
        /// </summary>
        /// <param name="path">The file. Must not be null.</param>
        /// <param name="contents">The text. Must not be null.</param>
        /// <param name="encoding">The encoding. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the write finishes.</returns>
        Task WriteAllTextAsync(
            string path,
            string contents,
            Encoding encoding,
            CancellationToken cancellationToken
        );
    }
}
