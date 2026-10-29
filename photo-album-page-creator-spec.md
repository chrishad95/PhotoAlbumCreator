# Photo Album Page Creator
## Draft Application Specification

**Version:** 0.1 (Draft)  
**Status:** Draft for review  
**Purpose:** Define a minimal application that automatically creates photo album pages from a collection of digital photographs.

---

## 1. Project Overview

### 1.1 Objective

Develop a simple application that automatically arranges photographs into pages suitable for printing in a physical photo album book.

The application accepts a directory of JPEG photographs and lets the user configure:

- Page dimensions.
- Minimum number of photographs per page.
- Maximum number of photographs per page.
- Whether photographs should be arranged chronologically.
- Basic page margins and spacing.

The application automatically distributes photographs across pages, generates the page layouts, provides a preview, and exports the finished pages to a print-ready PDF.

The primary goal is to automate the tedious process of manually arranging photographs into an album.

### 1.2 Design Principles

1. **Minimalism:** Provide only the functionality necessary to create photo album pages.
2. **Automation:** Require as little manual intervention as possible.
3. **Local processing:** Process photographs locally without uploading them to an external service.
4. **Non-destructive behavior:** Never modify or delete original photographs.
5. **Print quality:** Generate pages suitable for printing.
6. **Predictability:** Apply layout rules consistently and explain unavoidable exceptions.
7. **Usability:** Make it possible to create an album without technical knowledge.

### 1.3 Target Platform

The initial target is a local desktop application that runs on Windows. The architecture should allow macOS and Linux support to be added later.

The implementation technology is at the coding assistant's discretion. Prefer a simple, maintainable architecture over an unnecessarily complex framework.

All photo processing and PDF generation must happen locally.

---

## 2. Core User Workflow

1. **Select photographs:** The user selects a directory containing JPEG photographs.
2. **Configure the album:** The user specifies page dimensions, measurement units, minimum and maximum photographs per page, and whether chronological ordering is enabled.
3. **Generate the album:** The application reads metadata, orders the photographs, distributes them across pages, calculates image positions, and generates layouts.
4. **Preview pages:** The user browses the generated pages and inspects the arrangements.
5. **Export:** The user selects an output location and exports the album as a PDF.

---

## 3. Functional Requirements

### 3.1 Photograph Source

The application must allow the user to select a directory containing JPEG photographs.

#### Supported image formats

Required:

- JPEG (`.jpg`)
- JPEG (`.jpeg`)

PNG and WebP may be supported if doing so does not add significant complexity.

#### Directory scanning

The application must:

- Scan the selected directory for supported images.
- Display the number of photographs discovered.
- Ignore unrelated files.
- Detect images that cannot be opened.
- Report unreadable photographs without crashing.
- Preserve original files.

The initial version only needs to scan the selected directory, not its subdirectories. Recursive scanning may be added later.

#### Image metadata

For each photograph, attempt to obtain:

- Filename.
- Image width and height.
- Image orientation.
- Date and time the photograph was taken.
- Original file path.

Read EXIF metadata when available and respect EXIF orientation when displaying and exporting images.

#### Duplicate photographs

Automatic duplicate detection is not required in version 1.0. Each supported image file is treated as a separate photograph, even if another file contains identical image data.

### 3.2 Page Dimensions

The user must be able to specify album page dimensions.

Required controls:

- Page width.
- Page height.
- Measurement units.

Supported units:

- Inches.
- Millimeters.
- Centimeters.

Example configuration:

- Width: `8`
- Height: `10`
- Units: `Inches`

The application must convert the dimensions correctly for PDF generation.

#### Page orientation

Orientation is determined by the specified dimensions:

- `8 × 10` inches produces a portrait page.
- `10 × 8` inches produces a landscape page.

The application must not silently swap the specified width and height.

#### Output dimensions

PDF page dimensions must match the dimensions entered by the user. The application must not silently add printer margins or change the page size.

Printer-specific bleed settings are outside the scope of the initial version.

### 3.3 Minimum and Maximum Photographs per Page

The user must be able to specify the minimum and maximum number of photographs allowed on a page.

Example:

- Minimum photographs per page: `2`
- Maximum photographs per page: `5`

The application should attempt to distribute photographs so that each page satisfies these limits.

#### Distribution rules

The application should:

1. Determine the total number of accepted photographs.
2. Calculate a feasible number of pages.
3. Distribute photographs as evenly as practical.
4. Avoid a page with only one photograph when the configured minimum is greater than one, if a valid alternative distribution exists.
5. Preserve the photograph sequence when chronological ordering is enabled.
6. Avoid image overlap and placement outside page boundaries.

Favor balanced page layouts rather than filling every page to its maximum capacity.

#### Handling impossible configurations

Some combinations of photograph counts and page limits cannot satisfy all requirements.

For example, 11 photographs cannot be divided into pages containing at least 3 and at most 4 photographs each.

The application must detect this situation and explain the problem before generating the album. It must not silently violate the user's constraints.

#### Final-page handling

Distribute photographs to avoid an underfilled final page whenever possible. If no valid distribution exists, explain the issue and let the user adjust the minimum or maximum.

Never duplicate or silently discard photographs to make the page count work.

### 3.4 Chronological Ordering

Provide a setting:

**Arrange photographs chronologically: Yes / No**

#### When chronological ordering is enabled

Sort photographs from oldest to newest using their original capture dates.

Use the following metadata preference:

1. EXIF `DateTimeOriginal`.
2. EXIF `DateTimeDigitized`.
3. Other reliable EXIF date metadata, if available.

If no usable capture date is available, use the file's modification timestamp as a fallback.

The application must distinguish capture dates from file timestamps internally.

#### Missing dates

Photographs without a reliable capture date must not cause the application to fail.

The application should:

- Use the fallback timestamp when available.
- Record which date source was used.
- Display a warning when reliable capture dates are missing.

#### When chronological ordering is disabled

Use a predictable order: alphabetical by filename by default. Do not use a random order.

#### Equal dates

When multiple photographs have the same timestamp, use filenames as a secondary sorting key to make ordering deterministic.

Sorting must not rename photographs or change their timestamps. It affects only the order in which photographs appear in the album.

### 3.5 Automatic Page Layout

#### General requirements

Automatically arrange photographs on each page. The layout engine should consider:

- Page dimensions.
- Number of photographs assigned to the page.
- Photograph aspect ratios.
- Page margins.
- Spacing between photographs.
- Image orientation.

The initial version should not require users to position every photograph manually.

#### Layout strategy

Use a simple, deterministic layout engine. Support suitable layouts for:

- One photograph.
- Two photographs.
- Three photographs.
- Four photographs.
- Five or more photographs.

Examples of possible arrangements:

- Two photographs side by side.
- Three photographs with one larger image and two smaller images.
- Four photographs in a two-by-two grid.
- Five photographs in a balanced grid, potentially with one image occupying a larger area.

These are examples, not rigid requirements. The layout engine may select other arrangements when they produce a better result. Prefer a small set of reliable patterns over a complicated optimization system.

#### Photograph aspect ratios

Photographs must retain their original aspect ratios. Never stretch an image horizontally or vertically.

Default behavior:

- Scale the photograph to fit within its assigned rectangle.
- Preserve the entire photograph.
- Center it within the rectangle.
- Leave any unused area as page background.

Do not automatically crop photographs in version 1.0. Cropping may be considered in a future version.

#### Page margins and spacing

Provide configurable page margins and spacing between photographs.

Defaults:

- Page margin: `0.25` inches.
- Photo spacing: `0.10` inches.

Apply margins consistently to all four page edges. Keep all photographs within the usable page area.

Convert margin and spacing values correctly when the user selects millimeters or centimeters.

#### Layout validation

Before rendering a page, verify that:

- Every photograph has valid placement coordinates.
- No photograph extends beyond the usable page area.
- Photographs do not overlap.
- Aspect ratios are preserved.
- The photograph count satisfies the configured limits.
- The layout fits the specified page dimensions.

If a layout fails validation, select another layout or report the problem.

### 3.6 Page Preview

Provide a preview of the generated album.

Required functionality:

- View the current page.
- Move to the previous page.
- Move to the next page.
- Jump to a specified page number.
- See the current page number and total page count.
- See photographs arranged as they will appear on the page.

The preview should approximate the final page layout and aspect ratio.

Example:

`Page 4 of 32`

`[Previous Page] [Next Page]`

A sophisticated image editor is not required.

#### Preview performance

Use appropriately sized preview images or thumbnails so browsing an album containing hundreds of photographs remains responsive. Do not load every full-resolution photograph into memory simultaneously.

### 3.7 PDF Export

PDF export is required.

The application must:

- Generate a single PDF containing the entire album.
- Use the configured page dimensions.
- Place each album page on a separate PDF page.
- Preserve photograph aspect ratios and orientation.
- Maintain image quality suitable for printing.
- Avoid unintended cropping.
- Avoid adding headers, footers, or page numbers to the printed album pages.

#### Resolution and image quality

Preserve source photograph quality as much as reasonably possible. Scale images appropriately for their final printed dimensions and avoid unnecessary JPEG recompression.

#### Output location

Let the user specify the PDF filename and destination directory.

Default filename: `photo_album.pdf`

Warn the user before overwriting an existing output file.

#### Export validation

After export, confirm that:

- Export completed successfully.
- The output file was created.
- The PDF page count matches the generated album.
- PDF page dimensions match the configured dimensions.

If export fails, display a useful error message.

---

## 4. User Interface

### 4.1 General Design

Use a minimal, practical interface in a single main window. Keep configuration, generation, preview, and export straightforward. Avoid unnecessary menus, toolbars, dialogs, and decorative elements.

### 4.2 Suggested Interface

#### Section A: Photograph Source

- Directory: `[Select Photo Directory]`
- Photographs found: `245`

#### Section B: Page Settings

- Page width: `[8.0]`
- Page height: `[10.0]`
- Units: `[Inches ▼]`
- Minimum photographs per page: `[2]`
- Maximum photographs per page: `[5]`
- Arrange chronologically: `[Yes / No]`
- Page margin: `[0.25]`
- Photo spacing: `[0.10]`

#### Section C: Generate

- Button: `[Generate Album]`
- Status text, for example:
  - Scanning photographs...
  - Reading metadata...
  - Arranging photographs...
  - Generating preview...

#### Section D: Preview

Display the current page preview and navigation controls:

`[Previous] Page 1 of 62 [Next]`

#### Section E: Export

- Button: `[Export PDF]`

The export button must remain unavailable until an album has been generated successfully.

After a successful export, provide a convenient way to open the PDF or its containing directory.

This is a conceptual layout. The implementation may improve the arrangement while preserving the minimal workflow.

---

## 5. Application Architecture

Separate the following responsibilities. The exact technology stack is at the coding assistant's discretion.

### 5.1 User Interface

Responsibilities:

- Collect configuration settings.
- Allow directory selection.
- Display scanning and generation status.
- Display validation errors.
- Show the album preview.
- Initiate PDF export.

Keep core photo-layout algorithms outside the user interface.

### 5.2 Photograph Scanner

Responsibilities:

- Discover supported image files.
- Read image dimensions and orientation.
- Extract available EXIF metadata.
- Record file paths and timestamps.
- Identify unreadable images.
- Produce a collection of photograph records.

Each photograph record should contain at least:

- File path.
- Filename.
- Image width and height.
- Capture date, when available.
- Fallback timestamp, when available.
- Date source.
- Orientation information.

### 5.3 Sorting Component

Responsibilities:

- Sort photographs chronologically when requested.
- Sort photographs alphabetically otherwise.
- Apply deterministic tie-breaking.
- Preserve the selected ordering throughout page generation.

### 5.4 Page Distribution Component

Responsibilities:

- Calculate a feasible number of pages.
- Distribute photographs among pages.
- Enforce configured minimum and maximum counts.
- Preserve photograph order.
- Report impossible configurations.

### 5.5 Layout Engine

Responsibilities:

- Select an appropriate layout for each page.
- Calculate photograph placement rectangles.
- Respect page margins and spacing.
- Preserve aspect ratios.
- Validate layouts.

Keep the layout engine independent of the user interface and PDF library.

### 5.6 Preview Renderer

Responsibilities:

- Render page layouts for display.
- Use appropriately scaled images.
- Support efficient page navigation.
- Reuse cached previews when practical.

### 5.7 PDF Generator

Responsibilities:

- Create the PDF document.
- Set the correct page dimensions.
- Render photographs at appropriate quality.
- Preserve image proportions and orientation.
- Validate the generated output.

---

## 6. Configuration and Error Handling

Validate user settings before generating the album.

Invalid settings include:

- An inaccessible source directory.
- A directory containing no supported photographs.
- Page width or height less than or equal to zero.
- A negative page margin.
- Negative photograph spacing.
- A minimum photograph count less than one.
- A maximum photograph count smaller than the minimum.
- Margins or spacing that leave no usable page area.

Explain what needs to be corrected.

Example messages:

- `Maximum photographs per page must be greater than or equal to the minimum.`
- `The selected page dimensions and margins leave insufficient space for the requested layout.`

### Unreadable photographs

If some photographs cannot be read, identify them and allow the user to continue without those images. Clearly report how many photographs were excluded.

### Generation failures

An error processing one photograph should not unnecessarily crash the application. Log errors for troubleshooting without showing confusing technical stack traces to the user.

---

## 7. Performance Requirements

The application should be practical for albums containing hundreds or thousands of photographs.

- Directory scanning must not freeze the user interface.
- Metadata extraction should be efficient.
- Avoid keeping every full-resolution photograph in memory.
- Generate thumbnails and previews at appropriate resolutions.
- Process photographs incrementally during PDF export where practical.
- Display progress during longer operations.

Advanced parallel processing is not required unless performance testing demonstrates a need.

---

## 8. Data Persistence

A database and user accounts are not required.

The application should operate directly on local files. If practical, remember the last-used configuration:

- Last source directory.
- Page dimensions.
- Measurement units.
- Minimum and maximum photographs per page.
- Chronological ordering preference.
- Page margin.
- Photo spacing.
- Last output directory.

Store configuration locally.

Never modify original photographs when saving configuration or generating an album.

Saving and reopening complete album projects is outside the initial scope.

---

## 9. Security and Privacy

- Process photographs locally.
- Do not upload photographs, metadata, or directory contents to external services.
- Do not require a cloud account or external API key.
- Access only files and directories needed for the requested operation.
- Never execute files discovered in the photograph directory.

The application must work without an internet connection.

---

## 10. Testing Requirements

Implement automated tests for core functionality.

### 10.1 Photograph Scanning Tests

Test that:

- JPEG photographs are discovered.
- Unsupported files are ignored.
- Unreadable images are handled gracefully.
- Missing EXIF metadata does not cause failures.
- Image orientation is handled correctly.

### 10.2 Sorting Tests

Test that:

- Chronological sorting produces the correct sequence.
- Alphabetical sorting produces the correct sequence.
- Missing capture dates use the documented fallback.
- Equal timestamps produce deterministic ordering.

### 10.3 Distribution Tests

Test that:

- Photographs are distributed within the configured minimum and maximum.
- Distribution is balanced when possible.
- Original photograph order is preserved.
- Every accepted photograph appears exactly once.
- Impossible configurations are detected.

Example: Given 12 photographs, a minimum of 2, and a maximum of 4, a valid distribution is four pages with three photographs each.

Tests should verify the constraints and ordering, not require this exact distribution for every implementation.

### 10.4 Layout Tests

Test that:

- Images fit within the page.
- Images do not overlap.
- Margins and spacing are respected.
- Aspect ratios are preserved.
- Portrait and landscape photographs can share a page.
- Layouts work with all supported photograph counts.

### 10.5 PDF Tests

Test that:

- A PDF is generated successfully.
- Page count is correct.
- Page dimensions are correct.
- Images are present in expected positions.
- The PDF can be opened by a PDF parsing library.
- Export errors are handled correctly.

---

## 11. Acceptance Criteria

Version 1.0 is complete when:

1. The user can select a directory containing JPEG photographs.
2. The application identifies and counts supported photographs.
3. The user can specify custom page dimensions.
4. The user can choose inches, millimeters, or centimeters.
5. The user can specify minimum and maximum photographs per page.
6. The user can enable or disable chronological ordering.
7. The application reads available photograph capture dates.
8. Missing dates are handled without crashing.
9. Photographs are automatically distributed across pages.
10. The selected photograph order is preserved.
11. Generated layouts respect margins and spacing.
12. Photograph aspect ratios are preserved.
13. The user can preview generated pages.
14. The user can navigate between pages.
15. The application generates a PDF with the correct page dimensions.
16. The PDF contains every accepted photograph exactly once.
17. Invalid configurations and processing errors are reported clearly.
18. Original photographs remain unchanged.
19. The application works without an internet connection.
20. Automated tests cover scanning, sorting, distribution, layout, and export.

---

## 12. Features Excluded from Version 1.0

To keep the initial application minimal, do not implement:

- User accounts or authentication.
- Cloud storage or online photo libraries.
- Social media integration.
- AI-based image selection.
- AI-generated captions.
- Face recognition.
- Photo editing or image filters.
- Decorative backgrounds or templates.
- Text captions on album pages.
- Stickers, clip art, or embellishments.
- Manual drag-and-drop photo positioning.
- Manual page rearrangement.
- Automatic duplicate detection.
- Book printing or ordering services.
- Online collaboration.
- Mobile applications.
- A database for storing photo collections.
- Multi-user functionality.

These features may be considered in future versions if there is a demonstrated need.

---

## 13. Implementation Instructions for the AI Coding Assistant

Use this specification as the source of truth for implementation.

### Before implementation

1. Review the specification.
2. Select a straightforward technology stack suitable for a local Windows desktop application.
3. Explain the proposed architecture and important technology choices.
4. Identify ambiguities that could materially affect implementation.
5. Keep the implementation focused on the requirements in this document.

### During implementation

1. Build the application in small, testable components.
2. Keep photograph scanning, sorting, page distribution, layout, preview, and PDF generation separate.
3. Write automated tests for core algorithms.
4. Use established libraries for image processing, EXIF metadata, and PDF generation.
5. Handle errors explicitly.
6. Avoid unnecessary dependencies.
7. Do not add features outside the specified scope.
8. Keep the user interface simple and responsive.

### Before completion

1. Run the automated tests.
2. Test using a directory of real JPEG photographs.
3. Test chronological and alphabetical ordering.
4. Test portrait and landscape photographs.
5. Test different page sizes and measurement units.
6. Test valid and invalid minimum/maximum combinations.
7. Generate a PDF and verify page dimensions and image placement.
8. Document how to install, run, and build the application.
9. Provide a concise README describing the application and configuration options.

If a requirement is ambiguous, prefer the simplest implementation that preserves the application's purpose and document the decision.

---

## 14. Future Considerations

These items are not required for version 1.0 but may be evaluated later:

- Facing-page spreads and allowance for the center binding.
- Full-bleed layouts.
- Optional cropping modes.
- Recursive directory scanning.
- Additional image formats.
- Manual page or photograph ordering.
- Saved and reusable album projects.

---

## 15. Final Objective

Deliver a reliable, minimal desktop application that converts a directory of photographs into a photo album PDF using configurable page dimensions and photograph-count constraints. The user can choose chronological or alphabetical ordering, preview the generated pages, and export a print-ready PDF without modifying the original photographs.
