# Purple Pen Next

Purple Pen Next is a cross-platform continuation of the active Avalonia port of
[Purple Pen](https://github.com/petergolde/PurplePen), the course-setting
program for orienteering. The product focus is a macOS-first desktop
application that retains Purple Pen's established event, course, variation,
control-description, IOF XML, OCAD/OMAP, and MapModel functionality.

The first new capability is non-destructive, print-safe PDF export: a
versioned print profile must be applied at export time without changing the
source map. See [CODEX_LOCAL_HANDOFF.md](CODEX_LOCAL_HANDOFF.md) for the
agreed product and implementation plan.

## Upstream and licence

This repository starts from [`petergolde/PurplePen`](https://github.com/petergolde/PurplePen)
at upstream commit `dd5bda74000838d60e20c82ac9595192d7c3e9e4` (2026-08-19).
Keep `upstream` configured to that repository and develop on a separate branch.

Purple Pen is distributed under the BSD 3-Clause licence. The original
copyright and licence terms are retained in [LICENSE](LICENSE); third-party
notices and licences bundled with the upstream source must remain intact.
Neither this project nor its releases may imply endorsement by Peter Golde or
the Purple Pen contributors.

## Build on macOS (Apple Silicon)

Prerequisites:

- macOS 13 or newer on Apple Silicon;
- .NET SDK 10 (`dotnet --info` should report a 10.x SDK);
- Xcode Command Line Tools (`xcode-select --install`) and Git.

Use a full Git clone. The embedded PDFsharp build uses GitVersion, which needs
commit ancestry and tags; a shallow clone will fail during the build.

```sh
git clone https://github.com/YOUR-ACCOUNT/purple-pen-next.git
cd purple-pen-next
git remote add upstream https://github.com/petergolde/PurplePen.git
git fetch --tags upstream

cd src
dotnet restore AvPurplePen/AvPurplePen.csproj
dotnet restore PdfConverter/PdfConverter.csproj
dotnet build AvPurplePen/AvPurplePen.csproj --configuration Debug --no-restore -m:1
dotnet run --project AvPurplePen/AvPurplePen.csproj --configuration Debug --no-build
```

`PdfConverter` is invoked by `AvPurplePen`'s custom build target, so it needs
its own restore before a `--no-restore` build. `-m:1` is intentionally used for
the documented macOS command to keep the first build and its diagnostics
predictable. A normally configured CI runner can build in parallel.

If an existing clone is shallow, repair it before building:

```sh
git fetch --unshallow --tags upstream master
```

Run the fast cross-platform view-model test suite with:

```sh
cd src
dotnet test PurplePenViewModels.Tests/PurplePenViewModels.Tests.csproj --configuration Debug --no-restore --verbosity minimal
```

## Development boundaries

- Keep the Avalonia + Skia UI/rendering direction. Do not replace the map
  engine or rebuild Purple Pen from scratch.
- Preserve CMYK data through the print pipeline; do not silently route a
  print export through display RGB.
- A normal CMYK PDF is not PDF/X or print-safe. Profiles that require an ICC
  OutputIntent, true overprint, or spot/separation support must block a
  production export until the engine can provide it.
- Make small, independently verified commits. Push each completed block to
  `origin`; never leave major work only in an unpushed working tree.
