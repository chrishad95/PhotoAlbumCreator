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

## Environment notes

- The .NET 10 SDK (10.0.403) was installed with `winget install Microsoft.DotNet.SDK.10`. The first attempt failed with exit code 1602 because the UAC prompt was dismissed; the retry succeeded.
- A new terminal may be needed before `dotnet` is on the PATH.
