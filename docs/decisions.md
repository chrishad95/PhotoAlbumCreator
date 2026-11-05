# Decision Log

A running record of project decisions and the reasoning behind them. Newest entries go at the bottom. Each entry states the decision, why it was made, and what was rejected.

---

## 2026-10-30 — Language and UI framework: C# on .NET 10 with WPF

**Decision:** Build the application in C# on .NET 10 (LTS), with WPF for the Windows UI.

**Why:**
- The target is a local Windows desktop app ([spec §1.3](../photo-album-page-creator-spec.md)). .NET produces a single double-click `.exe`, which suits the "no technical knowledge" goal (§1.2).
- `async`/`await`, `IProgress<T>` and `CancellationToken` map directly to the performance requirements (§7): no frozen UI, progress display, incremental processing.
- Only the UI is Windows-specific. Core logic is plain .NET, so macOS/Linux support later means writing a new UI project, not rewriting the app.
- Layout and distribution are cheap arithmetic, so raw language speed is not a factor.

**Rejected:**

| Option | Reason |
|---|---|
| Python (PySide6/Tkinter) | Excellent imaging libraries, but Windows packaging is weak (large bundles, slow start, antivirus false positives). Reasonable only for personal-use tooling. |
| Java / Kotlin | Needs a bundled JRE and the UI looks dated. Viable second choice. |
| Rust | Immature desktop UI options and a steep learning curve. Nothing in the spec is CPU-bound. |
| C++ / Qt | Too complex for the "simple, maintainable" goal. |
| Electron | 150 MB+ bundle and high memory use, which conflicts with handling thousands of photos (§7). |
| Tauri | Two languages and two toolchains. |
| WinUI 3 / MAUI | Rougher tooling and packaging, or cross-platform complexity that v1.0 does not need. |

**Revisit if:** macOS/Linux becomes a near-term requirement. Use Avalonia (still C#) for the UI project and keep Core and Pdf unchanged.

---

## 2026-10-30 — Solution structure: layered projects

**Decision:** Four projects in `PhotoAlbum.slnx`:

- `src/PhotoAlbum.Core` — scanning, sorting, distribution, layout. No UI or PDF references.
- `src/PhotoAlbum.Pdf` — PDF generation, references Core.
- `src/PhotoAlbum.App` — WPF UI, references Core and Pdf.
- `tests/PhotoAlbum.Tests` — xUnit, references Core and Pdf.

**Why:** The spec requires the layout engine to be independent of the UI and PDF library (§5, README). Separate projects enforce that boundary at compile time, and pure logic in Core is unit-testable without a UI or file system.

---

## 2026-10-30 — PDF and imaging libraries: PDFsharp and SkiaSharp

**Decision:** Use PDFsharp (PDF output) and SkiaSharp (image decoding and thumbnails). Use MetadataExtractor for EXIF in Core.

**Why:** All are permissively licensed (PDFsharp MIT, SkiaSharp MIT, MetadataExtractor Apache 2.0), which satisfies the project security rule on compatible licences.

**Rejected:**
- **QuestPDF** — community licence with a revenue threshold; would need a policy check.
- **ImageSharp** — split licence; SkiaSharp avoids the question.

---

## 2026-10-30 — Dependency vulnerability override

**Decision:** Add an explicit reference to `System.Security.Cryptography.Pkcs` 10.0.14 in `PhotoAlbum.Pdf`.

**Why:** PDFsharp 6.5.0 transitively pulls in `System.Security.Cryptography.Pkcs` 8.0.1, which has known vulnerabilities (GHSA-846f-f764-76mx high, GHSA-2fmv-rv2c-59x7 moderate). With warnings-as-errors the restore failed until the patched version was referenced directly.

**Revisit if:** PDFsharp is upgraded. The pin may then be unnecessary; remove it only if the audit stays clean.

---

## 2026-10-30 — Build policy

**Decision:** In `Directory.Build.props`: NuGet lock files enabled, NuGet vulnerability audit on (all dependencies), and warnings treated as errors. `packages.lock.json` files are committed.

**Why:** The project security rules require dependency lock files, vulnerability scanning before use, and never ignoring security warnings.

---

## 2026-10-30 — Working agreement: commit directly to `main`

**Decision:** While there is a single contributor, commit and push directly to `main`, with no feature branches or pull requests.

**Why:** No reviewer exists, so branches and PRs add overhead without benefit.

**Revisit if:** another contributor joins.

---

## 2026-10-30 — Scope and token budget for the first working version

**Decision:** Reach a working app in five steps; defer the sixth.

1. Scaffold the solution and packages — **done** (commit `c9575d7`)
2. Core: scanner, sorting, distribution, with unit tests
3. Core: layout engine, with unit tests
4. PDF export
5. WPF window: folder picker, settings, Generate and Export buttons
6. Page preview and saved settings — deferred

**Cost-saving choices:**
- Default WPF controls with no custom styling (the spec asks for a minimal UI).
- Unit tests only for distribution and layout, where bugs are most likely.
- JPEG only; PNG/WebP is optional in the spec and is skipped for now.

---

## 2026-10-30 — Distribution: single-photo pages allowed, and group by time frame

**Decision:**
- Pages with fewer than 3 photographs, including a single photograph, are acceptable. The minimum per page is user-configurable down to 1. The planned UI default is minimum 1, maximum 4.
- In chronological mode, page breaks are placed at the largest time gaps between photographs, so photographs from the same time frame tend to share a page. The minimum and maximum are always respected and the page count does not change.

**Why:** The owner wants chronological albums to read as events, not as arbitrary equal-sized chunks. Even sizing alone ignores when photographs were taken.

**How:** `PageDistributor.Calculate` takes optional gaps (from `GapsBetween`). A dynamic programme rewards breaks at large gaps (log-scaled) and penalises pages far from the ideal size (weight `UnevenPagePenalty` = 0.1). Photographs with no date get a zero gap, meaning no preference. Without gaps, sizes stay even.

**Tuning:** If pages look too uneven or too eager to split on small gaps, adjust `UnevenPagePenalty`.

**Spec correction:** Spec §3.3 says 11 photographs cannot be split into pages of 3–4, but 3 + 4 + 4 = 11. The code treats it as feasible.

---

## 2026-10-30 — Layout: JSON split-tree templates, no cropping

**Decision:**
- Page layouts are defined as JSON templates in `src/PhotoAlbum.Core/Layout/Templates/`. Each template has a `name`, `description`, `photoCount`, and a `root` tree. A node is either `{ "slot": n }` (a photograph) or `{ "split": "rows" | "columns", "children": [...] }`. Slots are numbered 1..N in reading order, so photograph order is preserved.
- Photographs are never cropped in v1.0. Cell sizes come from the photographs' aspect ratios (photos in a row share a height, photos in a column share a width), and the result is scaled to fit the usable area and centered.
- Where several templates exist for the same photo count, the engine ranks them as candidates in a deterministic order. A future UI "Shuffle" button steps through that list, so results stay repeatable.

**Why:** New layouts can be added as text files with no code change and no new screenshots. The tree form describes every example layout the owner supplied. Aspect-ratio sizing satisfies the spec's no-crop, no-stretch rules.

**Source examples:** PNG mock-ups in `photo album examples/` (git-ignored, not published). The "overlayed" two-photo example was converted to a plain stacked layout because the spec forbids overlap. The red dashed lines in the mock-ups come from another tool and are ignored; margins come from the configurable page margin. The front and back cover examples are out of scope for v1.0.

**Templates cover 1-8 photographs.** For more than 8 (rare), the engine builds a grid in code instead of reading a JSON file. Photographs per row should be as equal as possible across rows. Owner's examples: 15 = 3 rows of 5; 16 = 4 rows of 4; 17 = 4 rows (three of 4, one of 5).
- Row count = floor(sqrt(n)), which reproduces all three examples (the grid is wider than tall).
- Photographs are spread across those rows as evenly as possible, so row sizes differ by at most one.

**Future ideas (owner):**
- Zoom, crop and reposition a photograph within its frame in the preview.
- Swap two photographs between frames on a page.

---

## 2026-11-02 — Future exploration: generative AI and LLMs

**Decision:** Record generative AI / LLM enhancements as a future area to explore. No AI work is in scope for v1.0, and the README's out-of-scope list still excludes AI-based selection, captions and face recognition for that release.

**Candidate uses:** captions and page titles; event or scene grouping; hero-photo and layout suggestions; flagging duplicates and poor-quality photographs; natural-language album requests.

**Constraints any AI feature must respect (from the spec and project security rules):**
- **Privacy:** the spec requires all photo processing to stay local (§1.2, §1.3). Family photographs must not be sent to an external service by default. Prefer a local model (for example a local vision-language model). Any cloud option must be opt-in, clearly disclosed, and off by default.
- **Offline:** the app works with no network. AI features must degrade gracefully when no model or connection is available.
- **Secrets:** any API key comes from environment variables or a secure store, never from code or the repository.
- **Transport:** any external call uses HTTPS (TLS 1.2 or higher) with certificate validation and timeouts.
- **Non-destructive:** AI output is a suggestion the user can accept or reject. It never modifies original photographs and never silently reorders or drops them.
- **Architecture:** put AI behind an interface in its own project so Core stays free of it, matching the Pdf separation.

**Next step when picked up:** a short research spike comparing local and cloud options for one use case (captions are the likely first), covering quality, hardware needs, licensing, and cost.

---

## 2026-11-03 — PDF export: PDFsharp pinned to 6.2.4 (OPEN LICENCE/MAINTENANCE QUESTION)

**Decision (provisional):** `PhotoAlbum.Pdf` uses PDFsharp **6.2.4** (MIT), not 6.5.0.

**Why:** PDFsharp 6.5.0 (published 2026-10-14) is licensed **AGPL-3.0-only** and throws `PdfSharpLicenseException: The loaded license is not valid` the first time a `PdfDocument` is created, so PDF export fails. Versions 6.1.0 to 6.2.4 are MIT. The earlier note choosing PDFsharp as "MIT, no licence concerns" was true only for 6.2.x and earlier.

**Conflict with project security rules:** 6.2.4 was published 2026-01-06, about 10 months ago, which is outside the "release within the last 6 months" maintenance check. 6.5.0 is the maintained line but its licence is not one of the preferred ones (Apache 2.0, MIT, BSD) and needs a licence key or AGPL compliance.

**Options:**
1. Keep PDFsharp 6.2.4 (MIT, stable, stale by the 6-month rule). Needs an explicit exception.
2. Replace PDFsharp with SkiaSharp's built-in PDF writer. SkiaSharp is already a dependency (MIT, actively released), so this removes a package. Needs a spike to confirm JPEG data is embedded without recompression and EXIF rotation is handled, and a separate way to verify page count and size.
3. Use PDFsharp 6.5.0 under a valid licence. Not preferred (AGPL).

**Status:** owner decision 2026-11-03: keep PDFsharp 6.2.4 for now (option 1), accepting the exception to the 6-month release rule. Revisit with a spike on option 2 (SkiaSharp PDF writer) later.

**Verified:** page count and size, original JPEG bytes embedded unchanged, EXIF orientations 1, 3, 6 and 8 rendered correctly (checked by rasterising a test PDF with PDFium).

---

## 2026-11-05 — Status of the first working version, and what changed since the plan

**Status:** Steps 2 to 5 of the 2026-10-30 plan are done, and step 6 (preview and saved settings), which was deferred, is now done too. The app scans, sorts, distributes, lays out, previews and exports.

**Added beyond the plan:**
- **Swap button** next to page width and height.
- **Page preview:** previous, next, "Page N of M" and go-to-page. Only the current page is drawn, using photographs decoded at no more than 800 pixels wide, so full-resolution images are never held in memory. It uses the same `OrientationTransform` as the exporters so the preview matches the output.
- **Saved settings:** a JSON file at `%LocalAppData%\PhotoAlbumCreator\settings.json`, holding the last folders, page size, units, photos per page, chronological toggle, margin and spacing. The model and store live in Core so they can be unit-tested. Loading never throws: each unusable value falls back to its default on its own, and the file is written to a temporary name and then moved into place.
- **PNG export** (see the next entry).

**Not done yet:** the chosen export format and PNG resolution are not saved. The layout "Shuffle" button is not built, although the engine already ranks candidates for it. The PDFsharp licence and maintenance question is still open (2026-11-03). The spec's acceptance checklist (§11) has not been run against a real photograph folder.

---

## 2026-11-05 — PNG export at print resolution, in its own project

**Decision:** Add an option to export one PNG image per page instead of a PDF, for photo-book services such as Snapfish, Shutterfly and Walmart. It lives in a new project, `src/PhotoAlbum.Png`, which references Core and SkiaSharp only.

**Why:**
- Those services usually take page images, not a PDF, and expect about 300 DPI. The default is therefore 300 DPI, with 150, 200 and 600 as choices. Pixel size is page size × DPI (8 × 10 inches at 300 DPI is 2400 × 3000).
- SkiaSharp is already a dependency (MIT, actively released), so no new package was added. A separate project mirrors the PDF one and keeps Core free of imaging code.
- Photographs are drawn from the full-resolution originals using the same orientation transform as the PDF and the preview, so all three agree.

**Behaviour:**
- Files are named `photo_album_page_001.png` and so on, padded so they sort in page order.
- Skia's PNG encoder does not write a resolution, so a small `pHYs` chunk is inserted after the header. Pixel data is not touched, and the tests check the chunk values.
- Output is all-or-nothing. Pages are written under temporary names, each is checked for the expected pixel size, and they are renamed only when every page succeeded. A failure or cancellation leaves no partial set, and existing files are untouched.
- The user is asked before replacing images from an earlier export.
- A page over 150 million pixels is refused before any memory is allocated. A 200 × 200 inch page at 300 DPI is far beyond any book page.
- Originals are never modified, and error messages show a file name but never a full path.

**Open question:** each service has its own exact page size and may require bleed. There is no bleed setting. The user enters the service's full page size, bleed included, as the page dimensions. Revisit with a bleed option or per-service presets once the real requirements are known.

---

## Environment notes

- The .NET 10 SDK (10.0.403) was installed with `winget install Microsoft.DotNet.SDK.10`. The first attempt failed with exit code 1602 because the UAC prompt was dismissed; the retry succeeded.
- A new terminal may be needed before `dotnet` is on the PATH.
