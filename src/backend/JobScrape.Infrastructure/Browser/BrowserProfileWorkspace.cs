using JobScrape.Application.Abstractions;
using JobScrape.Domain.Access;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// Adapts the browser session pool to the application's rename port.
    /// The pool owns the launch gate and the open contexts; this adapter
    /// holds no resources and is not disposable, so the pool is disposed once.
    /// </summary>
    public sealed class BrowserProfileWorkspace(BrowserSessionPool pool) : IBrowserProfileWorkspace
    {
        /// <summary>
        /// Moves the profile's user-data directory through the pool's gate.
        /// </summary>
        /// <param name="profile">The profile whose current name is the source directory.</param>
        /// <param name="newName">The new, already-validated directory name.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the gate.</param>
        /// <returns>The rename outcome.</returns>
        public Task<ProfileDirectoryRename> RenameAsync(
            BrowserProfile profile,
            string newName,
            CancellationToken cancellationToken
        )
        {
            return pool.RenameAsync(profile, newName, cancellationToken);
        }
    }
}
