using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Navigation;
using FileHandler.Localization;
using Microsoft.Win32;
using FileHandler.Models;
using FileHandler.Services;

namespace FileHandler;

public partial class MainWindow : Window
{
    private readonly SubfolderFileService _subfolderFileService = new();
    private readonly MediaOrganizerService _mediaOrganizerService = new();
    private MediaScanResult? _mediaScanResult;
    private SubfolderFileScanResult? _subfolderFileScan;
    private bool _mediaOrganizationRunning;
    private bool _subfolderOperationRunning;

    public MainWindow()
    {
        InitializeComponent();
        DateTextBlock.Text = DateTime.Now.ToString("d", AppLanguage.Culture);
        SubfolderLogPathTextBlock.Text = Text.Format("LogPath", _subfolderFileService.LogPath);
        MediaOrganizerLogPathTextBlock.Text = Text.Format("LogPath", _mediaOrganizerService.LogPath);
    }

    private void ContactHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Win32Exception exception)
        {
            MessageBox.Show(
                this,
                Text.Format("EmailOpenFailed", exception.Message),
                Text.Get("EmailErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(
                this,
                Text.Format("EmailOpenFailed", exception.Message),
                Text.Get("EmailErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OrganizeMedia_Click(object sender, RoutedEventArgs e)
    {
        HomeView.Visibility = Visibility.Collapsed;
        MediaOrganizerView.Visibility = Visibility.Visible;
        ResetMediaOrganizerView();
    }

    private void BackFromMediaOrganizer_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaOrganizationRunning)
        {
            return;
        }

        HomeView.Visibility = Visibility.Visible;
        MediaOrganizerView.Visibility = Visibility.Collapsed;
    }

    private void FindMediaDuplicates_Click(object sender, RoutedEventArgs e)
    {
        var window = new MediaDuplicateReviewWindow { Owner = this };
        window.ShowDialog();
    }

    private void ExtractSubfolderFiles_Click(object sender, RoutedEventArgs e)
    {
        HomeView.Visibility = Visibility.Collapsed;
        SubfolderFilesView.Visibility = Visibility.Visible;
        ResetSubfolderView();
    }

    private void BackFromSubfolderFiles_Click(object sender, RoutedEventArgs e)
    {
        if (_subfolderOperationRunning)
        {
            return;
        }

        HomeView.Visibility = Visibility.Visible;
        SubfolderFilesView.Visibility = Visibility.Collapsed;
    }

    private async void SelectSubfolderSource_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder(Text.Get("SelectSourceFolder"));
        if (selectedPath is not null)
        {
            SubfolderSourcePathTextBox.Text = selectedPath;
            SubfolderDestinationPathTextBox.Clear();
            await ScanSubfolderSourceAsync(selectedPath);
        }
    }

    private async Task ScanSubfolderSourceAsync(string source)
    {
        _subfolderFileScan = null;
        SubfolderCountsTextBlock.Text = string.Empty;
        SubfolderSummaryTextBlock.Text = string.Empty;
        SubfolderLogTextBox.Clear();
        SubfolderProgressBar.Value = 0;
        SubfolderProgressBar.IsIndeterminate = true;
        SubfolderStatusTextBlock.Text = Text.Get("ScanningSubfolderFiles");
        _subfolderOperationRunning = true;
        SelectSubfolderSourceButton.IsEnabled = false;
        SubfolderBackButton.IsEnabled = false;
        UpdateSubfolderControls();

        var progress = new Progress<OperationProgress>(UpdateSubfolderProgress);
        try
        {
            var scan = await Task.Run(() => _subfolderFileService.Scan(source, progress));
            _subfolderFileScan = scan;
            SubfolderCountsTextBlock.Text = Text.Format("SubfolderFilesFound", scan.Files.Count);
            if (scan.ScanErrors > 0)
            {
                SubfolderCountsTextBlock.Text += Text.Format("ScanErrorsSuffix", scan.ScanErrors);
            }

            if (scan.Files.Count == 0)
            {
                SubfolderSummaryTextBlock.Text = Text.Get("NoSubfolderFilesToMove");
            }
        }
        catch (ArgumentException exception)
        {
            ShowSubfolderOperationError(exception.Message);
        }
        catch (DirectoryNotFoundException exception)
        {
            ShowSubfolderOperationError(exception.Message);
        }
        catch (IOException exception)
        {
            ShowSubfolderOperationError(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowSubfolderOperationError(exception.Message);
        }
        catch (SecurityException exception)
        {
            ShowSubfolderOperationError(exception.Message);
        }
        finally
        {
            _subfolderOperationRunning = false;
            SelectSubfolderSourceButton.IsEnabled = true;
            SubfolderBackButton.IsEnabled = true;
            UpdateSubfolderControls();
        }
    }

    private void SelectSubfolderDestination_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder(Text.Get("SelectDestinationFolder"));
        if (selectedPath is not null)
        {
            SubfolderDestinationPathTextBox.Text = selectedPath;
            UpdateSubfolderControls();
        }
    }

    private void UpdateSubfolderControls()
    {
        var hasScan = !_subfolderOperationRunning && _subfolderFileScan is not null;
        SelectSubfolderDestinationButton.IsEnabled = hasScan;
        MoveSubfolderFilesButton.IsEnabled = hasScan &&
                                             _subfolderFileScan!.Files.Count > 0 &&
                                             SubfolderDestinationPathTextBox.Text.Trim().Length > 0;
    }

    private async void StartMoveSubfolderFiles_Click(object sender, RoutedEventArgs e)
    {
        var scan = _subfolderFileScan;
        var destination = SubfolderDestinationPathTextBox.Text.Trim();
        if (scan is null || scan.Files.Count == 0 || destination.Length == 0)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            Text.Format(
                "ConfirmMoveSubfolderFiles",
                scan.Files.Count,
                Environment.NewLine),
            Text.Get("ConfirmExtractTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _subfolderOperationRunning = true;
        SubfolderProgressBar.Value = 0;
        SubfolderProgressBar.IsIndeterminate = false;
        SubfolderSummaryTextBlock.Text = string.Empty;
        SubfolderBackButton.IsEnabled = false;
        SelectSubfolderSourceButton.IsEnabled = false;
        UpdateSubfolderControls();
        var progress = new Progress<OperationProgress>(UpdateSubfolderProgress);
        try
        {
            var summary = await Task.Run(() => _subfolderFileService.Move(scan, destination, progress));
            SubfolderSummaryTextBlock.Text =
                Text.Format(
                    "SubfolderMoveSummary",
                    summary.FilesFound,
                    summary.Moved,
                    summary.CollisionRenames,
                    summary.AlreadyAtDestination,
                    summary.ScanErrors,
                    summary.Errors);
            _subfolderFileScan = null;
        }
        catch (ArgumentException exception)
        {
            ShowSubfolderOperationError(exception.Message);
        }
        catch (DirectoryNotFoundException exception)
        {
            ShowSubfolderOperationError(exception.Message);
        }
        catch (IOException exception)
        {
            ShowSubfolderOperationError(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowSubfolderOperationError(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowSubfolderOperationError(exception.Message);
        }
        catch (SecurityException exception)
        {
            ShowSubfolderOperationError(exception.Message);
        }
        finally
        {
            _subfolderOperationRunning = false;
            SubfolderBackButton.IsEnabled = true;
            SelectSubfolderSourceButton.IsEnabled = true;
            UpdateSubfolderControls();
        }
    }

    private void UpdateSubfolderProgress(OperationProgress update)
    {
        SubfolderProgressBar.IsIndeterminate = update.IsScanning;
        if (!update.IsScanning)
        {
            SubfolderProgressBar.Value = update.Total == 0
                ? 100
                : update.Processed * 100.0 / update.Total;
        }
        SubfolderStatusTextBlock.Text = update.Message;
        SubfolderLogTextBox.AppendText(update.Message + Environment.NewLine);
        SubfolderLogTextBox.ScrollToEnd();
    }

    private void ShowSubfolderOperationError(string message)
    {
        SubfolderProgressBar.IsIndeterminate = false;
        SubfolderStatusTextBlock.Text = Text.Format("OperationFailed", message);
        SubfolderLogTextBox.AppendText($"[ERROR] {message}{Environment.NewLine}");
        MessageBox.Show(this, message, Text.Get("ExtractFilesErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void ResetSubfolderView()
    {
        _subfolderFileScan = null;
        SubfolderSourcePathTextBox.Clear();
        SubfolderDestinationPathTextBox.Clear();
        SubfolderCountsTextBlock.Text = string.Empty;
        SubfolderSummaryTextBlock.Text = string.Empty;
        SubfolderLogTextBox.Clear();
        SubfolderProgressBar.Value = 0;
        SubfolderProgressBar.IsIndeterminate = false;
        SubfolderStatusTextBlock.Text = Text.Get("SelectSourceToSearchSubfolders");
        SelectSubfolderSourceButton.IsEnabled = true;
        SubfolderBackButton.IsEnabled = true;
        UpdateSubfolderControls();
    }

    private async void SelectMediaOrganizerSource_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder(Text.Get("SelectMediaSource"));
        if (selectedPath is null)
        {
            return;
        }

        MediaOrganizerSourcePathTextBox.Text = selectedPath;
        await ScanMediaOrganizerSourceAsync(selectedPath);
    }

    private async Task ScanMediaOrganizerSourceAsync(string source)
    {
        _mediaScanResult = null;
        OrganizeImagesCheckBox.IsChecked = false;
        OrganizeVideosCheckBox.IsChecked = false;
        ImageOrganizationDestinationTextBox.Clear();
        VideoOrganizationDestinationTextBox.Clear();
        MediaOrganizerCountsTextBlock.Text = string.Empty;
        MediaOrganizationSummaryTextBlock.Text = string.Empty;
        MediaOrganizerLogTextBox.Clear();
        MediaOrganizerProgressBar.Value = 0;
        MediaOrganizerProgressBar.IsIndeterminate = true;
        MediaOrganizerStatusTextBlock.Text = Text.Get("ScanningMedia");
        _mediaOrganizationRunning = true;
        SelectMediaOrganizerSourceButton.IsEnabled = false;
        MediaOrganizerBackButton.IsEnabled = false;
        UpdateMediaOrganizationControls();

        var progress = new Progress<OperationProgress>(UpdateMediaOrganizationProgress);
        try
        {
            var scan = await Task.Run(() => _mediaOrganizerService.Scan(source, progress));
            _mediaScanResult = scan;
            MediaOrganizerCountsTextBlock.Text =
                Text.Format(
                    "MediaScanCounts",
                    scan.ImageFiles.Count,
                    Text.Get("Images"),
                    scan.VideoFiles.Count,
                    Text.Get("Videos"),
                    scan.SubdirectoryCount);
            if (scan.Errors > 0)
            {
                MediaOrganizerCountsTextBlock.Text += Text.Format("ScanErrorsSuffix", scan.Errors);
            }

            if (scan.ImageFiles.Count == 0 && scan.VideoFiles.Count == 0)
            {
                MediaOrganizationSummaryTextBlock.Text = Text.Get("NoCompatibleMedia");
            }
        }
        catch (ArgumentException exception)
        {
            ShowMediaOrganizationError(exception.Message);
        }
        catch (DirectoryNotFoundException exception)
        {
            ShowMediaOrganizationError(exception.Message);
        }
        catch (IOException exception)
        {
            ShowMediaOrganizationError(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowMediaOrganizationError(exception.Message);
        }
        catch (SecurityException exception)
        {
            ShowMediaOrganizationError(exception.Message);
        }
        finally
        {
            _mediaOrganizationRunning = false;
            SelectMediaOrganizerSourceButton.IsEnabled = true;
            MediaOrganizerBackButton.IsEnabled = true;
            UpdateMediaOrganizationControls();
        }
    }

    private void SelectImageOrganizationDestination_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder(Text.Get("SelectImageDestination"));
        if (selectedPath is not null)
        {
            ImageOrganizationDestinationTextBox.Text = selectedPath;
            UpdateMediaOrganizationControls();
        }
    }

    private void SelectVideoOrganizationDestination_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder(Text.Get("SelectVideoDestination"));
        if (selectedPath is not null)
        {
            VideoOrganizationDestinationTextBox.Text = selectedPath;
            UpdateMediaOrganizationControls();
        }
    }

    private void MediaOrganizationSelection_Changed(object sender, RoutedEventArgs e)
    {
        UpdateMediaOrganizationControls();
    }

    private void UpdateMediaOrganizationControls()
    {
        var hasScan = !_mediaOrganizationRunning && _mediaScanResult is not null;
        OrganizeImagesCheckBox.IsEnabled = hasScan && _mediaScanResult!.ImageFiles.Count > 0;
        OrganizeVideosCheckBox.IsEnabled = hasScan && _mediaScanResult!.VideoFiles.Count > 0;

        var organizeImages = hasScan && OrganizeImagesCheckBox.IsChecked == true;
        var organizeVideos = hasScan && OrganizeVideosCheckBox.IsChecked == true;
        SelectImageOrganizationDestinationButton.IsEnabled = organizeImages;
        SelectVideoOrganizationDestinationButton.IsEnabled = organizeVideos;
        ImageOrganizationDestinationTextBox.IsEnabled = organizeImages;
        VideoOrganizationDestinationTextBox.IsEnabled = organizeVideos;
        OrganizeMediaButton.IsEnabled = hasScan &&
            (organizeImages || organizeVideos) &&
            (!organizeImages || ImageOrganizationDestinationTextBox.Text.Trim().Length > 0) &&
            (!organizeVideos || VideoOrganizationDestinationTextBox.Text.Trim().Length > 0);
    }

    private async void StartMediaOrganization_Click(object sender, RoutedEventArgs e)
    {
        var scan = _mediaScanResult;
        var organizeImages = OrganizeImagesCheckBox.IsChecked == true;
        var organizeVideos = OrganizeVideosCheckBox.IsChecked == true;
        var imageDestination = ImageOrganizationDestinationTextBox.Text.Trim();
        var videoDestination = VideoOrganizationDestinationTextBox.Text.Trim();
        if (scan is null || (!organizeImages && !organizeVideos))
        {
            return;
        }

        var selectedItems = new List<string>();
        if (organizeImages)
        {
            selectedItems.Add(Text.Format(
                "SelectedMediaItem",
                scan.ImageFiles.Count,
                Text.Get("Images"),
                imageDestination));
        }
        if (organizeVideos)
        {
            selectedItems.Add(Text.Format(
                "SelectedMediaItem",
                scan.VideoFiles.Count,
                Text.Get("Videos"),
                videoDestination));
        }

        var confirmation = MessageBox.Show(
            this,
            Text.Format(
                "ConfirmMediaOrganization",
                Environment.NewLine,
                string.Join(Environment.NewLine, selectedItems)),
            Text.Get("ConfirmMediaOrganizationTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _mediaOrganizationRunning = true;
        MediaOrganizerProgressBar.Value = 0;
        MediaOrganizerProgressBar.IsIndeterminate = false;
        MediaOrganizationSummaryTextBlock.Text = string.Empty;
        MediaOrganizerBackButton.IsEnabled = false;
        SelectMediaOrganizerSourceButton.IsEnabled = false;
        UpdateMediaOrganizationControls();
        var progress = new Progress<OperationProgress>(UpdateMediaOrganizationProgress);
        try
        {
            var summary = await Task.Run(() => _mediaOrganizerService.Organize(
                scan,
                organizeImages,
                imageDestination,
                organizeVideos,
                videoDestination,
                progress));
            MediaOrganizationSummaryTextBlock.Text =
                Text.Format(
                    "MediaOrganizationSummary",
                    summary.ImagesMoved,
                    summary.ImagesToOrganize,
                    Text.Get("Images"),
                    summary.VideosMoved,
                    summary.VideosToOrganize,
                    Text.Get("Videos"),
                    summary.CollisionRenames,
                    summary.AlreadyOrganized,
                    summary.ScanErrors,
                    summary.Errors);
            _mediaScanResult = null;
        }
        catch (ArgumentException exception)
        {
            ShowMediaOrganizationError(exception.Message);
        }
        catch (DirectoryNotFoundException exception)
        {
            ShowMediaOrganizationError(exception.Message);
        }
        catch (IOException exception)
        {
            ShowMediaOrganizationError(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowMediaOrganizationError(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowMediaOrganizationError(exception.Message);
        }
        catch (SecurityException exception)
        {
            ShowMediaOrganizationError(exception.Message);
        }
        finally
        {
            _mediaOrganizationRunning = false;
            MediaOrganizerBackButton.IsEnabled = true;
            SelectMediaOrganizerSourceButton.IsEnabled = true;
            UpdateMediaOrganizationControls();
        }
    }

    private void UpdateMediaOrganizationProgress(OperationProgress update)
    {
        MediaOrganizerProgressBar.IsIndeterminate = update.IsScanning;
        if (!update.IsScanning)
        {
            MediaOrganizerProgressBar.Value = update.Total == 0
                ? 100
                : update.Processed * 100.0 / update.Total;
        }
        MediaOrganizerStatusTextBlock.Text = update.Message;
        MediaOrganizerLogTextBox.AppendText(update.Message + Environment.NewLine);
        MediaOrganizerLogTextBox.ScrollToEnd();
    }

    private void ShowMediaOrganizationError(string message)
    {
        MediaOrganizerProgressBar.IsIndeterminate = false;
        MediaOrganizerStatusTextBlock.Text = Text.Format("OperationFailed", message);
        MediaOrganizerLogTextBox.AppendText($"[ERROR] {message}{Environment.NewLine}");
        MessageBox.Show(
            this,
            message,
            Text.Get("OrganizeMediaErrorTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void ResetMediaOrganizerView()
    {
        _mediaScanResult = null;
        MediaOrganizerSourcePathTextBox.Clear();
        OrganizeImagesCheckBox.IsChecked = false;
        OrganizeVideosCheckBox.IsChecked = false;
        ImageOrganizationDestinationTextBox.Clear();
        VideoOrganizationDestinationTextBox.Clear();
        MediaOrganizerProgressBar.Value = 0;
        MediaOrganizerProgressBar.IsIndeterminate = false;
        MediaOrganizerStatusTextBlock.Text = Text.Get("SelectSourceToScan");
        MediaOrganizerCountsTextBlock.Text = string.Empty;
        MediaOrganizationSummaryTextBlock.Text = string.Empty;
        MediaOrganizerLogTextBox.Clear();
        SelectMediaOrganizerSourceButton.IsEnabled = true;
        MediaOrganizerBackButton.IsEnabled = true;
        UpdateMediaOrganizationControls();
    }

    private string? SelectFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }
}
