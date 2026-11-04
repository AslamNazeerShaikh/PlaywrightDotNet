using JobScrape.Domain.Access;
using JobScrape.Infrastructure.Browser;
using JobScrape.Infrastructure.Configuration;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Browser
{
    [Trait("Category", "Browser")]
    public sealed class BrowserExtensionArgsTests : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"jobscrape-ubol-{Guid.NewGuid():N}"
        );

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Fact]
        public void UnsetModeLaunchesWithoutExtensionArgs()
        {
            var pool = new BrowserSessionPool(
                Settings(headless: false, withDir: true, mode: null)
            );

            Assert.Null(pool.ExtensionArgs(BrowserEngine.Chromium));
        }

        [Fact]
        public void HeadlessNeverLoadsTheExtension()
        {
            var pool = new BrowserSessionPool(Settings(headless: true, withDir: true));

            Assert.Null(pool.ExtensionArgs(BrowserEngine.Chromium));
        }

        [Fact]
        public void MissingDirLaunchesWithoutTheExtension()
        {
            var pool = new BrowserSessionPool(Settings(headless: false, withDir: false));

            Assert.Null(pool.ExtensionArgs(BrowserEngine.Chromium));
        }

        [Fact]
        public void HeadedChromiumWithExistingDirLoadsTheExtension()
        {
            var pool = new BrowserSessionPool(Settings(headless: false, withDir: true));

            var args = pool.ExtensionArgs(BrowserEngine.Chromium);

            Assert.Equal(
                [
                    $"--disable-extensions-except={Dir()}",
                    $"--load-extension={Dir()}",
                ],
                args ?? []
            );
        }

        [Fact]
        public void NonChromiumNeverLoadsTheExtension()
        {
            var pool = new BrowserSessionPool(Settings(headless: false, withDir: true));

            Assert.Null(pool.ExtensionArgs(BrowserEngine.Firefox));
            Assert.Null(pool.ExtensionArgs(BrowserEngine.WebKit));
        }

        private BrowserSessionSettings Settings(
            bool headless,
            bool withDir,
            string? mode = BrowserOptions.UbolMax
        )
        {
            var dir = Dir();
            if (withDir)
            {
                Directory.CreateDirectory(dir);
            }

            return new BrowserSessionSettings(
                _root,
                _root,
                headless,
                TimeSpan.FromSeconds(5),
                mode,
                dir
            );
        }

        private string Dir()
        {
            return Path.Combine(_root, "ubol-chrome-max");
        }
    }
}
