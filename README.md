# Photo Album Page Creator

A local desktop application that automatically arranges photographs into pages suitable for printing in a physical photo album book.

> **Status:** Draft specification — v0.1

---

## Overview

The application accepts a directory of JPEG photographs and automatically:

- Reads EXIF metadata and photograph dimensions.
- Orders photographs chronologically or alphabetically.
- Distributes photographs across pages within configurable per-page limits.
- Calculates image placement while preserving aspect ratios and respecting margins.
- Provides a page-by-page preview.
- Exports a print-ready PDF at the exact dimensions specified by the user.

The primary goal is to automate the tedious process of manually arranging photographs into an album. All processing happens locally — no internet connection, cloud account, or external API key is required.

---

## Core Workflow

1. **Select photographs** — choose a directory containing JPEG photographs.
2. **Configure the album** — set page dimensions, units, minimum/maximum photographs per page, and whether chronological ordering is enabled.
3. **Generate** — the application scans, sorts, distributes, and lays out the photographs.
4. **Preview** — browse the generated pages before exporting.
5. **Export** — choose an output location and save the album as a PDF.

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
- Page-by-page preview after generation.
- Navigation controls to move between pages.
- Previews use scaled-down images for performance; full resolution is used for export.

### PDF Export
- Exports to a user-selected location.
- Page dimensions match the configured values exactly.
- Every accepted photograph appears in the PDF exactly once.
- Export button is unavailable until a successful album has been generated.

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
| Output directory | Where the exported PDF is saved |

The last-used configuration is saved locally and restored on next launch.

---

## Architecture

The application is composed of six separate components:

| Component | Responsibility |
|---|---|
| **User Interface** | Configuration input, directory selection, status display, preview, export trigger |
| **Photograph Scanner** | File discovery, EXIF extraction, dimension reading, unreadable image reporting |
| **Sorting Component** | Chronological or alphabetical ordering with deterministic tie-breaking |
| **Page Distribution** | Feasibility calculation, per-page allocation, constraint enforcement |
| **Layout Engine** | Placement rectangle calculation, margin/spacing enforcement, aspect-ratio preservation |
| **PDF Generator** | Document creation, correct page dimensions, full-resolution image rendering |

Core layout algorithms are kept independent of the user interface and PDF library.

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
