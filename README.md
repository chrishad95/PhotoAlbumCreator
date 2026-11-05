# Photo Album Page Creator

A local desktop application that automatically arranges photographs into pages suitable for printing in a physical photo album book.

> **Status:** Working version — v0.1. Scanning, sorting, distribution, layout, page preview, PDF and PNG export, and saved settings are implemented. See [`docs/decisions.md`](docs/decisions.md) for what is still open.

---

## Overview

The application accepts a directory of JPEG photographs and automatically:

- Reads EXIF metadata and photograph dimensions.
- Orders photographs chronologically or alphabetically.
- Distributes photographs across pages within configurable per-page limits.
- Calculates image placement while preserving aspect ratios and respecting margins.
- Provides a page-by-page preview.
- Exports a print-ready PDF at the exact dimensions specified by the user, or one high-resolution PNG image per page for photo-book services.

The primary goal is to automate the tedious process of manually arranging photographs into an album. All processing happens locally — no internet connection, cloud account, or external API key is required.

---

## Core Workflow

1. **Select photographs** — choose a directory containing JPEG photographs.
2. **Configure the album** — set page dimensions, units, minimum/maximum photographs per page, and whether chronological ordering is enabled.
3. **Generate** — the application scans, sorts, distributes, and lays out the photographs.
4. **Preview** — browse the generated pages before exporting.
5. **Export** — choose PDF or PNG images, choose an output location, and save the album.

---

## Features

### Photograph Source
- Scans a selected directory for `.jpg` / `.jpeg` files (PNG and WebP optionally supported).
- Displays the count of discovered photographs.
- Reports unreadable files without crashing; allows the user to continue without them.
- Reads EXIF metadata: filename, dimensions, orientation, and capture date.
- Respects EXIF orientation when displaying and exporting.
- Does not modify or delete original files.

### Page Dimensions
- User-specified width, height, and units (inches, millimetres, or centimetres).
- Orientation is determined by the entered dimensions — width and height are never silently swapped.
- A **Swap** button exchanges width and height (for example 8 × 10 to 10 × 8).
- PDF page size matches the entered dimensions exactly — no hidden printer margins are added.

### Photograph Distribution
- Configurable **minimum** and **maximum** photographs per page.
- Photographs are distributed as evenly as practical across pages.
- Impossible configurations (e.g. 11 photographs that cannot be split into pages of 3–4) are detected and reported before generation begins.
- Photographs are never duplicated or silently discarded.

### Chronological Ordering
- Optional **Arrange photographs chronologically** toggle.
- When enabled, photographs are sorted oldest-to-newest using EXIF `DateTimeOriginal`, then `DateTimeDigitized`, then other EXIF date fields, falling back to file modification timestamp.
- When disabled, photographs are sorted alphabetically by filename.
- Missing dates are handled gracefully with documented fallback behaviour.

### Automatic Layout
- Layouts are calculated automatically for each page.
- Photograph aspect ratios are always preserved.
- Images never overlap and never extend beyond page boundaries.
- Configurable page margins and photograph spacing.

### Preview
- Page-by-page preview after generation, drawn at the page's real aspect ratio.
- Previous and next buttons, a "Page 4 of 32" label, and a go-to-page box.
- Previews use small decoded thumbnails (at most 800 pixels wide) for performance, one page at a time; full resolution is used for export.
- Rotated photographs are shown the way they will be exported.

### Export
- Exports to a user-selected location.
- Every accepted photograph appears in the output exactly once.
- Export is unavailable until a successful album has been generated.
- An existing file is never replaced without asking, and a failed export never leaves a partial file or set of files.

**PDF**
- One PDF page per album page, with page dimensions matching the configured values exactly.
- Original JPEG data is embedded without recompression.

**PNG images**
- One PNG file per page, named `photo_album_page_001.png`, `photo_album_page_002.png`, and so on, so they sort in page order.
- Resolution of 150, 200, 300 (default, the usual print standard) or 600 DPI. Pixel size is page size × DPI, so an 8 × 10 inch page at 300 DPI is 2400 × 3000 pixels.
- The DPI is recorded in each file so editors and print services report the intended print size.
- Photographs are drawn from the full-resolution originals with EXIF orientation applied; the page background is white.
- Page sizes that would exceed about 150 million pixels are refused with a message.
- Photo-book services differ in exact page size and bleed. Enter the service's full page size, bleed included, as the page dimensions.

---

## Configuration Options

| Setting | Description |
|---|---|
| Source directory | Directory containing JPEG photographs |
| Page width | Width of each album page |
| Page height | Height of each album page |
| Measurement units | Inches, millimetres, or centimetres |
| Minimum photos per page | Fewest photographs allowed on a single page |
| Maximum photos per page | Most photographs allowed on a single page |
| Chronological ordering | Arrange photographs by capture date (Yes / No) |
| Page margin | Space between the page edge and photograph area |
| Photo spacing | Gap between individual photographs |
| Output directory | Where the exported PDF or PNG images are saved |
| Export format | PDF document or PNG images |
| PNG resolution | 150, 200, 300 or 600 DPI (PNG export only) |

The last-used configuration is saved locally to `%LocalAppData%\PhotoAlbumCreator\settings.json` and restored on next launch: source directory, output directory, page size, units, minimum and maximum photographs per page, chronological ordering, margin and spacing. The saved folder is scanned again automatically if it still exists. A missing or damaged file falls back to the defaults, field by field. The export format and PNG resolution are not saved yet.

---

## Architecture

The application is composed of seven separate components:

| Component | Responsibility |
|---|---|
| **User Interface** | Configuration input, directory selection, status display, preview, export trigger |
| **Photograph Scanner** | File discovery, EXIF extraction, dimension reading, unreadable image reporting |
| **Sorting Component** | Chronological or alphabetical ordering with deterministic tie-breaking |
| **Page Distribution** | Feasibility calculation, per-page allocation, constraint enforcement |
| **Layout Engine** | Placement rectangle calculation, margin/spacing enforcement, aspect-ratio preservation |
| **PDF Generator** | Document creation, correct page dimensions, full-resolution image rendering |
| **PNG Generator** | One print-resolution image per page, DPI embedded, all-or-nothing writes |

They are built as separate projects: `PhotoAlbum.Core` (scanning, sorting, distribution, layout, saved settings), `PhotoAlbum.Pdf`, `PhotoAlbum.Png` and `PhotoAlbum.App` (the WPF window and preview). Core layout algorithms are kept independent of the user interface and the PDF and PNG libraries.

---

## Target Platform

- **Initial target:** Windows (local desktop application).
- macOS and Linux support may be added in later versions.
- Works entirely offline — no network access is required.

---

## Build and Run

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) (Windows). A new terminal may be needed after installing it.

```
dotnet build PhotoAlbum.slnx
dotnet test PhotoAlbum.slnx
dotnet run --project src/PhotoAlbum.App
```

Project decisions and their reasoning are recorded in [`docs/decisions.md`](docs/decisions.md).

---

## Out of Scope (Version 1.0)

The following features are explicitly excluded from the initial release:

- User accounts or authentication
- Cloud storage or online photo libraries
- AI-based image selection, captions, or face recognition
- Photo editing or image filters
- Decorative backgrounds, templates, stickers, or embellishments
- Text captions on album pages
- Manual drag-and-drop photo positioning or page rearrangement
- Automatic duplicate detection
- Book printing or ordering services
- Mobile applications
- Recursive subdirectory scanning
- Saved/reopenable album projects

---

## Future Considerations

- Facing-page spreads with binding allowance
- Full-bleed layouts
- Optional cropping modes
- Recursive directory scanning
- Additional image formats
- Manual page or photograph ordering
- Saved and reusable album projects
- **Exploring generative AI and LLMs** to enhance the application (research only; see [`docs/decisions.md`](docs/decisions.md) for the constraints). Areas to investigate:
  - Suggested captions or page titles from photograph content and capture dates.
  - Grouping photographs into events or scenes beyond capture-time gaps.
  - Choosing a "hero" photograph for each page and suggesting layouts.
  - Flagging near-duplicate, blurry, or poorly exposed photographs for the user to review.
  - Natural-language album requests, for example "make a vacation album with one photo per day".
