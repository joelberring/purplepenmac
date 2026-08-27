# Print-production PDF architecture

## Decision

Continue with the existing `Map_PDF` + bundled PDFsharp pipeline. It already
emits CMYK drawing operators and retains Purple Pen's map/course rendering
path. Do not claim PDF/X, ICC OutputIntent, or true PDF overprint until those
features have complete producer-to-output verification.

This is deliberately an incremental extension, not a new PDF engine. A new
engine would duplicate map rendering semantics, fonts, imported PDF handling,
and course layout without first solving the production metadata contract.

## Verified current behaviour

| Requirement | Current result | Evidence |
| --- | --- | --- |
| CMYK drawing values | Supported | `Map_PDF/PdfWriter.cs` selects `PdfColorMode.Cmyk`; `PdfGraphicsTarget.cs` creates `XColor.FromCmyk`. |
| Profile colour preflight | Supported, non-destructively | `PurplePenCore/ColorPreflight.cs`. |
| PDF/X OutputIntent | Not supported | `PdfDocument.SetPdfA()` is explicitly a `HACK_OLD`, produces an sRGB PDF/A-style OutputIntent, and is not called by `Map_PDF`. |
| Embedded print ICC profile | Not supported | No profile selected from `CoursePdfSettings` reaches `PdfDocument`; the only embedded profile is the internal sRGB resource in the PDF/A prototype. |
| True PDF overprint | Supported for map and course colours | `Pdf_GraphicsTarget` transfers `SymColor.OverPrint` to PDFsharp pens and brushes. `PdfProductionTests` generates a PDF and verifies its `/OP`, `/op`, and `/OPM 1` entries. |

The preflight declares CMYK and the structurally verified true-overprint path
as available. Custom profiles that require PDF/X OutputIntent or ICC embedding
are intentionally blocked.

## Extension plan

1. Add an explicit PDF production-options object to `IPdfDocumentWriter` and
   `PdfDocumentWriter`, including a chosen ICC byte stream, OutputIntent
   metadata, conformance target, and an opt-in overprint flag.
2. Maintain fixture PDFs for CMYK fills/strokes and overprinted course violet,
   and retain the structural check for `/OP`, `/op`, and `/OPM 1`.
3. Generate fixture PDFs for an embedded CMYK OutputIntent. Add structural
   tests that inspect the produced PDF dictionaries and content streams.
4. Validate fixture PDFs in an external prepress validator using the selected
   PDF/X standard and printer-supplied ICC profile. Only after these checks
   pass may `PdfExportCapabilities` advertise either feature as supported.

## Integrity rule

Profile matching and warnings never rewrite the OCAD/OpenMapper source map.
Any future production transform applies only to the export document and must
be visible in the preflight report, including the selected profile version and
ICC identifier.

The selected profile currently applies its course-colour rule (CMYK, optional
OCAD ID, and overprint) to the PDF course layer only. Map colours are never
rewritten; their deviations remain visible as preflight warnings.

Built-in and imported profile JSON documents declare `SchemaVersion`. Purple
Pen rejects an unknown schema version rather than applying a future profile
with current assumptions.
