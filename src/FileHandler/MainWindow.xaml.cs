using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Navigation;
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
        DateTextBlock.Text = DateTime.Now.ToString("dd/MM/yyyy");
        SubfolderLogPathTextBlock.Text = $"Los registros se guardan en: {_subfolderFileService.LogPath}";
        MediaOrganizerLogPathTextBlock.Text = $"Los registros se guardan en: {_mediaOrganizerService.LogPath}";
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
                $"No se pudo abrir el enlace de correo: {exception.Message}",
                "No se pudo abrir el correo",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(
                this,
                $"No se pudo abrir el enlace de correo: {exception.Message}",
                "No se pudo abrir el correo",
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
        var selectedPath = SelectFolder("Selecciona la carpeta de origen");
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
        SubfolderStatusTextBlock.Text = "Escaneando archivos en subcarpetas...";
        _subfolderOperationRunning = true;
        SelectSubfolderSourceButton.IsEnabled = false;
        SubfolderBackButton.IsEnabled = false;
        UpdateSubfolderControls();

        var progress = new Progress<OperationProgress>(UpdateSubfolderProgress);
        try
        {
            var scan = await Task.Run(() => _subfolderFileService.Scan(source, progress));
            _subfolderFileScan = scan;
            SubfolderCountsTextBlock.Text = $"{scan.Files.Count} archivos encontrados en subcarpetas";
            if (scan.ScanErrors > 0)
            {
                SubfolderCountsTextBlock.Text += $" · {scan.ScanErrors} errores de escaneo";
            }

            if (scan.Files.Count == 0)
            {
                SubfolderSummaryTextBlock.Text = "No hay archivos en subcarpetas para mover.";
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
        var selectedPath = SelectFolder("Selecciona la carpeta de destino");
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
            $"Se intentarán mover {scan.Files.Count} archivos encontrados en subcarpetas a la raíz del destino. " +
            $"Los archivos que ya estén en la raíz del origen o del destino se conservarán.{Environment.NewLine}{Environment.NewLine}" +
            "Ante cualquier colisión se conservarán ambos archivos y el entrante recibirá un sufijo incremental, " +
            "incluso si su contenido es idéntico. ¿Quieres continuar?",
            "Confirmar extracción de archivos",
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
                $"Encontrados: {summary.FilesFound} · Movidos: {summary.Moved} · " +
                $"Renombrados por colisión: {summary.CollisionRenames} · Ya en destino: " +
                $"{summary.AlreadyAtDestination} · Errores de escaneo/movimiento: " +
                $"{summary.ScanErrors}/{summary.Errors}.";
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
        SubfolderStatusTextBlock.Text = $"La operación no pudo completarse: {message}";
        SubfolderLogTextBox.AppendText($"[ERROR] {message}{Environment.NewLine}");
        MessageBox.Show(this, message, "Error al extraer archivos", MessageBoxButton.OK, MessageBoxImage.Error);
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
        SubfolderStatusTextBlock.Text = "Selecciona la carpeta de origen para buscar archivos en sus subcarpetas.";
        SelectSubfolderSourceButton.IsEnabled = true;
        SubfolderBackButton.IsEnabled = true;
        UpdateSubfolderControls();
    }

    private async void SelectMediaOrganizerSource_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder("Selecciona la carpeta de origen multimedia");
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
        MediaOrganizerStatusTextBlock.Text = "Escaneando imágenes y vídeos...";
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
                $"{scan.ImageFiles.Count} imágenes · {scan.VideoFiles.Count} vídeos · " +
                $"{scan.SubdirectoryCount} subcarpetas";
            if (scan.Errors > 0)
            {
                MediaOrganizerCountsTextBlock.Text += $" · {scan.Errors} errores de escaneo";
            }

            if (scan.ImageFiles.Count == 0 && scan.VideoFiles.Count == 0)
            {
                MediaOrganizationSummaryTextBlock.Text = "No se encontraron imágenes ni vídeos compatibles.";
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
        var selectedPath = SelectFolder("Selecciona el destino de las imágenes");
        if (selectedPath is not null)
        {
            ImageOrganizationDestinationTextBox.Text = selectedPath;
            UpdateMediaOrganizationControls();
        }
    }

    private void SelectVideoOrganizationDestination_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder("Selecciona el destino de los vídeos");
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
            selectedItems.Add($"{scan.ImageFiles.Count} imágenes → {imageDestination}");
        }
        if (organizeVideos)
        {
            selectedItems.Add($"{scan.VideoFiles.Count} vídeos → {videoDestination}");
        }

        var confirmation = MessageBox.Show(
            this,
            $"Se moverán estos archivos a carpetas Año/Mes:{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, selectedItems) +
            $"{Environment.NewLine}{Environment.NewLine}Los nombres en conflicto recibirán un sufijo incremental. " +
            "Si el movimiento entre volúmenes no es posible, se copiará y verificará por SHA-256 antes de borrar el origen. " +
            "¿Quieres continuar?",
            "Confirmar organización multimedia",
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
                $"Movidos: {summary.ImagesMoved}/{summary.ImagesToOrganize} imágenes · " +
                $"{summary.VideosMoved}/{summary.VideosToOrganize} vídeos · " +
                $"Renombrados por colisión: {summary.CollisionRenames} · Ya organizados: " +
                $"{summary.AlreadyOrganized} · Errores de escaneo/movimiento: " +
                $"{summary.ScanErrors}/{summary.Errors}.";
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
        MediaOrganizerStatusTextBlock.Text = $"La operación no pudo completarse: {message}";
        MediaOrganizerLogTextBox.AppendText($"[ERROR] {message}{Environment.NewLine}");
        MessageBox.Show(
            this,
            message,
            "Error al organizar multimedia",
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
        MediaOrganizerStatusTextBlock.Text = "Selecciona la carpeta de origen para escanearla.";
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
