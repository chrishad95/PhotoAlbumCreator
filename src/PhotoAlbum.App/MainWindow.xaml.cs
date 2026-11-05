using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using PhotoAlbum.Core.Album;
using PhotoAlbum.Core.Layout;
using PhotoAlbum.Core.Scanning;
using PhotoAlbum.Pdf;

namespace PhotoAlbum.App;

public partial class MainWindow : Window
{
    private const int MaxNamesShown = 5;

    private readonly bool _ready;
    private ScanResult? _scan;
    private AlbumResult? _album;
    private string _scanNotice = string.Empty;
    private string _albumWarnings = string.Empty;
    private string? _lastPdf;
    private string? _lastExportDirectory;
    private LengthUnit _unit = LengthUnit.Inches;
    private bool _busy;
    private int _pageIndex;

    public MainWindow()
    {
        InitializeComponent();
        _ready = true;
        UpdateControls();
    }

    // ---- Section 1: photographs ----

    private async void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select the folder containing your photographs" };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var folder = dialog.FolderName;
        DirectoryText.Text = folder;
        DirectoryText.ToolTip = folder;
        _scan = null;
        _album = null;
        Preview.Clear();
        _scanNotice = string.Empty;
        _albumWarnings = string.Empty;
        FoundText.Text = "Photographs found: 0";
        ExportText.Text = string.Empty;

        SetBusy(true, "Scanning photographs...");
        try
        {
            _scan = await Task.Run(() => PhotoScanner.Scan(folder));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            AppLog.Error("Folder scan failed", ex);
            SetBusy(false);
            ShowStatus("That folder could not be read. Check that it exists and that you have permission to open it.", true);
            return;
        }

        SetBusy(false);
        var found = _scan.Photographs.Count;
        FoundText.Text = $"Photographs found: {found}";
        _scanNotice = DescribeUnreadable(_scan);

        ShowStatus(
            found == 0
                ? "No JPEG photographs were found in that folder."
                : "Choose your page settings, then select Generate Album.",
            found == 0);
        UpdateControls();
    }

    private static string DescribeUnreadable(ScanResult scan)
    {
        if (scan.Unreadable.Count == 0)
        {
            return string.Empty;
        }

        var names = scan.Unreadable.Select(u => Path.GetFileName(u.FilePath)).Take(MaxNamesShown).ToList();
        var more = scan.Unreadable.Count - names.Count;
        var list = string.Join(", ", names) + (more > 0 ? $" and {more} more" : string.Empty);
        return $"{scan.Unreadable.Count} file(s) could not be read and will be skipped: {list}.";
    }

    // ---- Section 2: settings ----

    private void SettingChanged(object sender, RoutedEventArgs e) => InvalidateAlbum();

    private void Swap_Click(object sender, RoutedEventArgs e) =>
        (WidthBox.Text, HeightBox.Text) = (HeightBox.Text, WidthBox.Text);

    private void UnitsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        var newUnit = SelectedUnit();
        if (newUnit == _unit)
        {
            return;
        }

        // Convert the lengths already entered so "8 inches" becomes "203.2 millimeters", not "8 millimeters".
        foreach (var box in new[] { WidthBox, HeightBox, MarginBox, SpacingBox })
        {
            if (ReadNumber(box) is { } value)
            {
                var points = LengthUnits.ToPoints(value, _unit);
                box.Text = Format(points / LengthUnits.ToPoints(1, newUnit));
            }
        }

        _unit = newUnit;
        InvalidateAlbum();
    }

    private LengthUnit SelectedUnit() => UnitsCombo.SelectedIndex switch
    {
        1 => LengthUnit.Millimeters,
        2 => LengthUnit.Centimeters,
        _ => LengthUnit.Inches,
    };

    private static string Format(double value) =>
        Math.Round(value, 3).ToString("0.###", CultureInfo.CurrentCulture);

    private static double? ReadNumber(TextBox box) =>
        double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
        double.IsFinite(value)
            ? value
            : null;

    private static int? ReadWholeNumber(TextBox box) =>
        int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var value) ? value : null;

    private bool TryReadSettings(out AlbumSettings settings, out string error)
    {
        settings = new AlbumSettings(0, 0, LengthUnit.Inches, 1, 1, false, 0, 0);
        error = string.Empty;

        if (ReadNumber(WidthBox) is not { } width)
        {
            error = "Enter a number for the page width.";
            return false;
        }

        if (ReadNumber(HeightBox) is not { } height)
        {
            error = "Enter a number for the page height.";
            return false;
        }

        if (ReadWholeNumber(MinBox) is not { } min)
        {
            error = "Enter a whole number for the minimum photographs per page.";
            return false;
        }

        if (ReadWholeNumber(MaxBox) is not { } max)
        {
            error = "Enter a whole number for the maximum photographs per page.";
            return false;
        }

        if (ReadNumber(MarginBox) is not { } margin)
        {
            error = "Enter a number for the page margin.";
            return false;
        }

        if (ReadNumber(SpacingBox) is not { } spacing)
        {
            error = "Enter a number for the photo spacing.";
            return false;
        }

        settings = new AlbumSettings(
            width, height, SelectedUnit(), min, max, ChronoCheck.IsChecked == true, margin, spacing);
        return true;
    }

    private void InvalidateAlbum()
    {
        if (!_ready || _album is null)
        {
            return;
        }

        _album = null;
        _albumWarnings = string.Empty;
        Preview.Clear();
        ShowStatus("Settings changed. Select Generate Album to update the album.");
        UpdateControls();
    }

    // ---- Section 3: generate ----

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_scan is null || _scan.Photographs.Count == 0)
        {
            ShowStatus("Select a folder that contains JPEG photographs first.", true);
            return;
        }

        if (!TryReadSettings(out var settings, out var error))
        {
            ShowStatus(error, true);
            return;
        }

        var photographs = _scan.Photographs;
        _album = null;
        _albumWarnings = string.Empty;
        Preview.Clear();
        ExportText.Text = string.Empty;
        SetBusy(true, "Arranging photographs...");

        AlbumResult result;
        try
        {
            result = await Task.Run(() => AlbumBuilder.Build(photographs, settings));
        }
        finally
        {
            SetBusy(false);
        }

        if (!result.IsSuccess)
        {
            ShowStatus(result.Error ?? "The album could not be created.", true);
            UpdateControls();
            return;
        }

        _album = result;
        _albumWarnings = string.Join(Environment.NewLine, result.Warnings);
        ShowStatus($"Album ready: {result.Pages.Count} pages for {result.PhotographCount} photographs.");
        UpdateControls();
        await ShowPageAsync(0);
    }

    // ---- Section 4: preview ----

    private async void PrevPage_Click(object sender, RoutedEventArgs e) => await ShowPageAsync(_pageIndex - 1);

    private async void NextPage_Click(object sender, RoutedEventArgs e) => await ShowPageAsync(_pageIndex + 1);

    private async void GoTo_Click(object sender, RoutedEventArgs e) => await GoToEnteredPageAsync();

    private async void GoToBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            await GoToEnteredPageAsync();
        }
    }

    private async Task GoToEnteredPageAsync()
    {
        if (_album is not { } album)
        {
            return;
        }

        if (ReadWholeNumber(GoToBox) is not { } number || number < 1 || number > album.Pages.Count)
        {
            ShowStatus($"Enter a page number from 1 to {album.Pages.Count}.", true);
            return;
        }

        await ShowPageAsync(number - 1);
    }

    private async Task ShowPageAsync(int index)
    {
        if (_album is not { IsSuccess: true } album || album.Pages.Count == 0)
        {
            return;
        }

        _pageIndex = Math.Clamp(index, 0, album.Pages.Count - 1);
        GoToBox.Text = (_pageIndex + 1).ToString(CultureInfo.CurrentCulture);
        UpdateControls();

        var maxWidth = Math.Max(100, PreviewHost.ActualWidth - 2);
        var maxHeight = Math.Max(100, PreviewHost.Height - 2);
        await Preview.ShowAsync(album.Pages[_pageIndex], album.Page, maxWidth, maxHeight);
    }

    // ---- Section 5: export ----

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_album is not { IsSuccess: true } album)
        {
            return;
        }

        // The dialog asks before replacing an existing file, as the spec requires.
        var dialog = new SaveFileDialog
        {
            Title = "Export album as PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = "photo_album.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = _lastExportDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var path = dialog.FileName;
        var pageCount = album.Pages.Count;
        Progress.Maximum = pageCount;
        Progress.Value = 0;
        SetBusy(true, "Exporting PDF...", indeterminate: false);
        ExportText.Text = string.Empty;

        var progress = new Progress<int>(done =>
        {
            Progress.Value = done;
            StatusText.Text = $"Exporting page {done} of {pageCount}...";
        });

        try
        {
            var result = await Task.Run(() => PdfAlbumExporter.Export(album.Pages, album.Page, path, true, progress));
            _lastPdf = result.FilePath;
            _lastExportDirectory = Path.GetDirectoryName(result.FilePath);
            SetBusy(false);
            ShowStatus($"Exported {result.PageCount} pages.");
            ExportText.Text = result.FilePath;
        }
        catch (PdfExportException ex)
        {
            AppLog.Error("PDF export failed", ex);
            SetBusy(false);
            ShowStatus(ex.Message, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("PDF export failed", ex);
            SetBusy(false);
            ShowStatus("The PDF could not be saved. Check the location and try again.", true);
        }

        UpdateControls();
    }

    private void OpenPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_lastPdf is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_lastPdf) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            AppLog.Error("Open PDF failed", ex);
            ShowStatus("The PDF could not be opened. You can open it from the folder instead.", true);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastPdf is null)
        {
            return;
        }

        try
        {
            var info = new ProcessStartInfo("explorer.exe");
            info.ArgumentList.Add($"/select,{_lastPdf}");
            Process.Start(info);
        }
        catch (Win32Exception ex)
        {
            AppLog.Error("Open folder failed", ex);
            ShowStatus("The folder could not be opened.", true);
        }
    }

    // ---- Shared UI state ----

    private void SetBusy(bool busy, string? status = null, bool indeterminate = true)
    {
        _busy = busy;
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        Progress.IsIndeterminate = indeterminate;
        if (status is not null)
        {
            ShowStatus(status);
        }

        UpdateControls();
    }

    private void ShowStatus(string text, bool isError = false)
    {
        StatusText.Text = text;
        StatusText.Foreground = isError ? Brushes.Firebrick : SystemColors.ControlTextBrush;
    }

    private void UpdateControls()
    {
        var hasPhotos = _scan is { Photographs.Count: > 0 };

        SelectButton.IsEnabled = !_busy;
        SettingsGrid.IsEnabled = !_busy;
        GenerateButton.IsEnabled = !_busy && hasPhotos;
        ExportButton.IsEnabled = !_busy && _album is not null;

        var pageCount = _album?.Pages.Count ?? 0;
        PrevButton.IsEnabled = !_busy && _pageIndex > 0 && pageCount > 0;
        NextButton.IsEnabled = !_busy && _pageIndex < pageCount - 1;
        GoToBox.IsEnabled = !_busy && pageCount > 0;
        GoToButton.IsEnabled = GoToBox.IsEnabled;
        PageText.Text = pageCount > 0 ? $"Page {_pageIndex + 1} of {pageCount}" : "No pages yet";
        OpenPdfButton.IsEnabled = !_busy && _lastPdf is not null && File.Exists(_lastPdf);
        OpenFolderButton.IsEnabled = OpenPdfButton.IsEnabled;

        var warnings = string.Join(
            Environment.NewLine, new[] { _scanNotice, _albumWarnings }.Where(s => s.Length > 0));
        WarningsText.Text = warnings;
        WarningsText.Visibility = warnings.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
