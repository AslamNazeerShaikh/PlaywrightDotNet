# VS Code setup on macOS

This folder holds `PlaywrightDotNet.code-profile` and the notes below.
The notes record every macOS specific fix from the November 2026
debugging session. Follow them in order on a new Mac.

## Why macOS needs this

The Homebrew .NET SDK is adhoc signed. The hardened VS Code host
rejects its `libhostfxr`, so the C# DevKit server dies at spawn with
exit 130. Every build then queues against a dead server. The DevKit log
showed two `Starting: Solution Build Action` lines and zero
completions. The fix is a Microsoft signed SDK plus three settings.
Nothing in the repo code was at fault. The `dotnet` CLI built the
solution clean while DevKit hung.

## Set up a machine

Do these steps once per Mac. Open the repo root in VS Code, not
`src/backend`, so the solution pin below resolves.

1. Install a Microsoft signed SDK next to Homebrew. Keep Homebrew for
   the terminal. The version must match `src/backend/global.json`.

   ```sh
   INST=$(mktemp /tmp/dotnet-install.XXXXXX.sh)
   curl -sSL https://dot.net/v1/dotnet-install.sh -o "$INST" && chmod +x "$INST"
   "$INST" --version 10.0.403 --install-dir ~/.dotnet; rm -f "$INST"
   ~/.dotnet/dotnet --list-sdks
   ```

2. Point both C# extensions at that SDK in your User `settings.json`.
   Add one entry per extension. Use the absolute home path. The
   acquisition extension reads the path literally and does not expand
   `${env:HOME}`.

   ```json
   "dotnetAcquisitionExtension.existingDotnetPath": [
       { "extensionId": "ms-dotnettools.csharp", "path": "/Users/you/.dotnet/dotnet" },
       { "extensionId": "ms-dotnettools.csdevkit", "path": "/Users/you/.dotnet/dotnet" }
   ]
   ```

3. Keep the repo settings as committed. `.vscode/settings.json` pins
   `"dotnet.defaultSolution": "src/backend/JobScrape.slnx"` because the
   repo has one `.slnx` file and no legacy `.sln`. Without the pin,
   DevKit scans `src/frontend` too and Solution Explorer stays empty.
   `.vscode/extensions.json` recommends the C#, DevKit, and csharpier
   extensions.

4. Reload VS Code. The first restore runs through the CLI on `PATH`
   and takes about two seconds.

## Verify the fix

Read the DevKit log at this path. Pick the newest dated folder.

```text
~/Library/Application Support/Code/logs/<date>/window1/exthost/ms-dotnettools.csdevkit/C# Dev Kit.log
```

Expect these lines and none of the failure lines.

```text
Default solution: src/backend/JobScrape.slnx
Using existing .NET runtime at "/Users/you/.dotnet/dotnet" to load C# Dev Kit processes
Completed: Spawn .NET server
Completed: Opening a solution
Completed: Restoring NuGet packages
```

`Projects.log` in the same folder must report 12 projects loaded and
0 failed. `exthost.log` and the C# log must show zero errors. The one
benign line is a 401 from `IdeBenefitsSource`. It means no Visual
Studio subscription is attached. Ignore it.

Rebuild from Solution Explorer. The log must show one
`Starting: Solution Build Action` followed by
`Completed: Solution Build Action` and `No problems found in the
build`. Two starts with no completion means the server is dead again.
Recheck step 2.

## Debug HTTPS

`src/backend/JobScrape.Api/Properties/launchSettings.json` defines an
`http` profile and an `https` profile. Both bind the same ports. The
HTTPS URL is `https://localhost:5208` and the HTTP URL is
`http://localhost:5207`. Pick the profile in the Run and Debug
dropdown. The development certificate is trusted on this machine and
valid to May 2027. A breakpoint on the opening brace of
`HealthController.GetAsync` (`Controllers/HealthController.cs` line
39) binds on method entry. Step-through works from there.

## Import the profile

`PlaywrightDotNet.code-profile` bundles User `settings.json` (27
keys, sorted, commented) and the extension list (6 extensions: C#,
C# DevKit, .NET Install Tool, csharpier, indent-rainbow,
material-icon-theme).

1. Open the Command Palette and pick Preferences: Import Profile.
2. Select `PlaywrightDotNet.code-profile`.
3. Restart VS Code when prompted.
4. Apply step 2 above with your own home path. The profile carries
   the path from the machine that exported it.

## Reference

| # | Item | Value at time of writing |
| --- | --- | --- |
| 1 | Repo SDK pin, `src/backend/global.json` | `10.0.403` |
| 2 | Microsoft SDK install dir | `~/.dotnet` |
| 3 | Homebrew SDK (terminal only) | `/opt/homebrew/opt/dotnet/bin/dotnet` |
| 4 | C# extension | `ms-dotnettools.csharp 2.160.4` |
| 5 | C# DevKit extension | `ms-dotnettools.csdevkit 3.40.215` |
| 6 | Solution file | `src/backend/JobScrape.slnx` |
| 7 | Rebuild time after fix | about 7 seconds, 0 warnings, 0 errors |
