using JobScrape.Domain.Access;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// The on-disk side of a browser profile rename. The application layer
    /// changes metadata; this port moves the user-data directory under the
    /// pool's launch gate so an open context is never moved underneath.
    /// </summary>
    public interface IBrowserProfileWorkspace
    {
        /// <summary>
        /// Moves the profile's user-data directory to <paramref name="newName"/>.
        /// </summary>
        /// <param name="profile">The profile whose current name is the source directory.</param>
        /// <param name="newName">The new, already-validated directory name.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The outcome; see <see cref="ProfileDirectoryRename"/>.</returns>
        Task<ProfileDirectoryRename> RenameAsync(
            BrowserProfile profile,
            string newName,
            CancellationToken cancellationToken
        );
    }

    /// <summary>
    /// Outcome of a browser-profile directory rename.
    /// </summary>
    public enum ProfileDirectoryRename
    {
        /// <summary>The existing directory was moved to the new name.</summary>
        Moved,

        /// <summary>Neither directory existed, so only metadata changed.</summary>
        NothingToMove,

        /// <summary>The old directory is gone and the new one already exists.</summary>
        AlreadyRenamed,

        /// <summary>The profile's context is open, so the directory cannot move.</summary>
        ProfileInUse,

        /// <summary>Both the old and new directories exist.</summary>
        TargetExists,

        /// <summary>The filesystem refused the move (lock, permission, or race).</summary>
        Failed,
    }
}
