# Lightweight GitHub branch

This branch is a source-focused snapshot for testing PurplePen Mac/Avalonia work.

It intentionally excludes large regression-test maps and PDFs, documentation archives,
bundled PDFium binaries, legacy installer packages, and third-party sample/documentation
trees. These omissions keep the initial GitHub clone practical and do not modify the
full local development history.

Build the Avalonia app from `src`:

```bash
dotnet build AvPurplePen/AvPurplePen.csproj --configuration Debug
```

The full test corpus and legacy packaging inputs remain in the local full repository.
