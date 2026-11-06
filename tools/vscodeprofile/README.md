# VS Code profile

`PlaywrightDotNet.code-profile` is an exported VS Code profile: editor
settings plus the extension list. Import it to reproduce this machine's
setup.

## Import

1. Command Palette → Preferences: Import Profile.
2. Select `PlaywrightDotNet.code-profile`.
3. Restart VS Code when prompted.

Contents: `settings.json` (27 keys, sorted, commented) and
`extensions.json` (6 extensions: C#, C# DevKit, .NET Install Tool,
csharpier, indent-rainbow, material-icon-theme).

## After import, fix one machine-local path

The profile carries this machine's .NET runtime path:

```json
"dotnetAcquisitionExtension.existingDotnetPath": [
    { "extensionId": "ms-dotnettools.csharp", "path": "/Users/aslamshaikh/.dotnet/dotnet" },
    { "extensionId": "ms-dotnettools.csdevkit", "path": "/Users/aslamshaikh/.dotnet/dotnet" }
]
```

Point both entries at your own Microsoft-signed SDK (Homebrew's
adhoc-signed `libhostfxr` is rejected by the hardened VS Code host:
DevKit server exits 130, builds hang). SDK version must match
`src/backend/global.json`. Install with:

```sh
INST=$(mktemp /tmp/dotnet-install.XXXXXX.sh)
curl -sSL https://dot.net/v1/dotnet-install.sh -o "$INST" && chmod +x "$INST"
"$INST" --version 10.0.403 --install-dir ~/.dotnet; rm -f "$INST"
```
