using System.IO;
using System.Security;
using System.Windows;
using Microsoft.Win32;
using FileHandler.Models;
using FileHandler.Services;

namespace FileHandler;

public partial class MainWindow : Window
{
    private readonly DateOrganizer _organizer = new();
    private readonly MediaCopyService _mediaCopyService = new();
    private MediaScanResult? _mediaScanResult;
    private bool _mediaOperationRunning;

    public MainWindow()
    {
        InitializeComponent();
        LogPathTextBlock.Text = $"Los registros se guardan en: {_organizer.LogPath}";
        MediaLogPathTextBlock.Text = $"Los registros se guardan en: {_mediaCopyService.LogPath}";
    }

    private void OrganizeByDate_Click(object sender, RoutedEventArgs e)
    {
        HomeView.Visibility = Visibility.Collapsed;
        DateOrganizerView.Visibility = Visibility.Visible;
    }

    private void BackToHome_Click(object sender, RoutedEventArgs e)
    {
        HomeView.Visibility = Visibility.Visible;
        DateOrganizerView.Visibility = Visibility.Collapsed;
    }

    private void CopyMedia_Click(object sender, RoutedEventArgs e)
    {
        HomeView.Visibility = Visibility.Collapsed;
        MediaCopyView.Visibility = Visibility.Visible;
        ResetMediaView();
    }

    private void BackFromMediaCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaOperationRunning)
        {
            return;
        }

        HomeView.Visibility = Visibility.Visible;
        MediaCopyView.Visibility = Visibility.Collapsed;
    }

    private void SelectSource_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder("Selecciona la carpeta de origen");
        if (selectedPath is not null)
        {
            SourcePathTextBox.Text = selectedPath;
        }
    }

    private void SelectDestination_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder("Selecciona la carpeta de destino");
        if (selectedPath is not null)
        {
            DestinationPathTextBox.Text = selectedPath;
        }
    }

    private async void SelectMediaSource_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder("Selecciona la carpeta de origen multimedia");
        if (selectedPath is null)
        {
            return;
        }

        MediaSourcePathTextBox.Text = selectedPath;
        await ScanMediaSourceAsync(selectedPath);
    }

    private async Task ScanMediaSourceAsync(string source)
    {
        _mediaScanResult = null;
        CopyImagesCheckBox.IsChecked = false;
        CopyVideosCheckBox.IsChecked = false;
        ImageDestinationTextBox.Clear();
        VideoDestinationTextBox.Clear();
        MediaCountsTextBlock.Text = string.Empty;
        MediaSummaryTextBlock.Text = string.Empty;
        MediaLogTextBox.Clear();
        MediaProgressBar.Value = 0;
        MediaProgressBar.IsIndeterminate = true;
        MediaStatusTextBlock.Text = "Escaneando archivos multimedia...";
        _mediaOperationRunning = true;
        SelectMediaSourceButton.IsEnabled = false;
        MediaBackButton.IsEnabled = false;
        UpdateMediaCopyControls();

        var progress = new Progress<OperationProgress>(UpdateMediaProgress);
        try
        {
            _mediaScanResult = await Task.Run(() => _mediaCopyService.Scan(source, progress));
            MediaCountsTextBlock.Text =
                $"{_mediaScanResult.ImageFiles.Count} imágenes · " +
                $"{_mediaScanResult.VideoFiles.Count} vídeos · " +
                $"{_mediaScanResult.SubdirectoryCount} subcarpetas";
            if (_mediaScanResult.Errors > 0)
            {
                MediaCountsTextBlock.Text += $" · {_mediaScanResult.Errors} errores de escaneo";
            }
        }
        catch (ArgumentException exception)
        {
            ShowMediaOperationError(exception.Message);
        }
        catch (DirectoryNotFoundException exception)
        {
            ShowMediaOperationError(exception.Message);
        }
        catch (IOException exception)
        {
            ShowMediaOperationError(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowMediaOperationError(exception.Message);
        }
        catch (SecurityException exception)
        {
            ShowMediaOperationError(exception.Message);
        }
        finally
        {
            _mediaOperationRunning = false;
            SelectMediaSourceButton.IsEnabled = true;
            MediaBackButton.IsEnabled = true;
            UpdateMediaCopyControls();
        }
    }

    private void SelectImageDestination_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder("Selecciona el destino de las imágenes");
        if (selectedPath is not null)
        {
            ImageDestinationTextBox.Text = selectedPath;
            UpdateMediaCopyControls();
        }
    }

    private void SelectVideoDestination_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder("Selecciona el destino de los vídeos");
        if (selectedPath is not null)
        {
            VideoDestinationTextBox.Text = selectedPath;
            UpdateMediaCopyControls();
        }
    }

    private void MediaTypeSelection_Changed(object sender, RoutedEventArgs e)
    {
        UpdateMediaCopyControls();
    }

    private void UpdateMediaCopyControls()
    {
        var hasScan = _mediaScanResult is not null && !_mediaOperationRunning;
        CopyImagesCheckBox.IsEnabled = hasScan && _mediaScanResult!.ImageFiles.Count > 0;
        CopyVideosCheckBox.IsEnabled = hasScan && _mediaScanResult!.VideoFiles.Count > 0;

        var copyImages = hasScan && CopyImagesCheckBox.IsChecked == true;
        var copyVideos = hasScan && CopyVideosCheckBox.IsChecked == true;
        SelectImageDestinationButton.IsEnabled = copyImages;
        ImageDestinationTextBox.IsEnabled = copyImages;
        SelectVideoDestinationButton.IsEnabled = copyVideos;
        VideoDestinationTextBox.IsEnabled = copyVideos;
        CopyMediaButton.IsEnabled = hasScan &&
            ((copyImages && ImageDestinationTextBox.Text.Length > 0) ||
             (copyVideos && VideoDestinationTextBox.Text.Length > 0));
    }

    private async void StartMediaCopy_Click(object sender, RoutedEventArgs e)
    {
        var scan = _mediaScanResult;
        var copyImages = CopyImagesCheckBox.IsChecked == true;
        var copyVideos = CopyVideosCheckBox.IsChecked == true;
        var imageDestination = ImageDestinationTextBox.Text.Trim();
        var videoDestination = VideoDestinationTextBox.Text.Trim();
        if (scan is null || (!copyImages && !copyVideos))
        {
            MessageBox.Show(
                this,
                "Selecciona al menos un tipo de archivo para copiar.",
                "No hay archivos seleccionados",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var selectedItems = new List<string>();
        if (copyImages)
        {
            selectedItems.Add($"{scan.ImageFiles.Count} imágenes → {imageDestination}");
        }
        if (copyVideos)
        {
            selectedItems.Add($"{scan.VideoFiles.Count} vídeos → {videoDestination}");
        }

        var confirmation = MessageBox.Show(
            this,
            $"Se copiarán los siguientes archivos; el origen se conservará:{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, selectedItems),
            "Confirmar copia",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _mediaOperationRunning = true;
        CopyMediaButton.IsEnabled = false;
        SelectMediaSourceButton.IsEnabled = false;
        MediaBackButton.IsEnabled = false;
        UpdateMediaCopyControls();
        MediaProgressBar.Value = 0;
        MediaProgressBar.IsIndeterminate = false;
        MediaSummaryTextBlock.Text = string.Empty;
        var progress = new Progress<OperationProgress>(UpdateMediaProgress);
        try
        {
            var summary = await Task.Run(() => _mediaCopyService.Copy(
                scan,
                copyImages,
                imageDestination,
                copyVideos,
                videoDestination,
                progress));
            MediaSummaryTextBlock.Text =
                $"Copiados: {summary.ImagesCopied}/{summary.ImagesToCopy} imágenes · " +
                $"{summary.VideosCopied}/{summary.VideosToCopy} vídeos · " +
                $"{summary.CopyErrors} errores";
            if (summary.ScanErrors > 0)
            {
                MediaSummaryTextBlock.Text += $" · {summary.ScanErrors} errores de escaneo";
            }
        }
        catch (ArgumentException exception)
        {
            ShowMediaOperationError(exception.Message);
        }
        catch (DirectoryNotFoundException exception)
        {
            ShowMediaOperationError(exception.Message);
        }
        catch (IOException exception)
        {
            ShowMediaOperationError(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowMediaOperationError(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowMediaOperationError(exception.Message);
        }
        catch (SecurityException exception)
        {
            ShowMediaOperationError(exception.Message);
        }
        finally
        {
            _mediaOperationRunning = false;
            SelectMediaSourceButton.IsEnabled = true;
            MediaBackButton.IsEnabled = true;
            UpdateMediaCopyControls();
        }
    }

    private void UpdateMediaProgress(OperationProgress update)
    {
        MediaProgressBar.IsIndeterminate = update.IsScanning;
        if (!update.IsScanning)
        {
            MediaProgressBar.Value = update.Total == 0
                ? 100
                : update.Processed * 100.0 / update.Total;
        }
        MediaStatusTextBlock.Text = update.Message;
        MediaLogTextBox.AppendText(update.Message + Environment.NewLine);
        MediaLogTextBox.ScrollToEnd();
    }

    private void ShowMediaOperationError(string message)
    {
        MediaProgressBar.IsIndeterminate = false;
        MediaStatusTextBlock.Text = $"La operación no pudo completarse: {message}";
        MediaLogTextBox.AppendText($"[ERROR] {message}{Environment.NewLine}");
        MessageBox.Show(this, message, "Error en la copia", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void ResetMediaView()
    {
        _mediaScanResult = null;
        MediaSourcePathTextBox.Clear();
        CopyImagesCheckBox.IsChecked = false;
        CopyVideosCheckBox.IsChecked = false;
        ImageDestinationTextBox.Clear();
        VideoDestinationTextBox.Clear();
        MediaProgressBar.Value = 0;
        MediaProgressBar.IsIndeterminate = false;
        MediaStatusTextBlock.Text = "Selecciona la carpeta de origen para escanearla.";
        MediaCountsTextBlock.Text = string.Empty;
        MediaSummaryTextBlock.Text = string.Empty;
        MediaLogTextBox.Clear();
        SelectMediaSourceButton.IsEnabled = true;
        MediaBackButton.IsEnabled = true;
        UpdateMediaCopyControls();
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var source = SourcePathTextBox.Text.Trim();
        var destination = DestinationPathTextBox.Text.Trim();
        if (source.Length == 0 || destination.Length == 0)
        {
            MessageBox.Show(
                this,
                "Selecciona las carpetas de origen y destino.",
                "Faltan carpetas",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            "Esta operación moverá los archivos de origen. Los archivos con un nombre ya existente en el destino se omitirán. ¿Quieres continuar?",
            "Confirmar organización",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        StartButton.IsEnabled = false;
        BackButton.IsEnabled = false;
        SelectSourceButton.IsEnabled = false;
        SelectDestinationButton.IsEnabled = false;
        OperationProgressBar.IsIndeterminate = true;
        OperationProgressBar.Value = 0;
        SummaryTextBlock.Text = string.Empty;
        LogTextBox.Clear();
        StatusTextBlock.Text = "Preparando el recorrido de archivos...";

        var progress = new Progress<OperationProgress>(UpdateProgress);
        try
        {
            var summary = await Task.Run(() => _organizer.Organize(source, destination, progress));
            SummaryTextBlock.Text =
                $"Resultado: {summary.Total} encontrados · {summary.Moved} movidos · " +
                $"{summary.Skipped} omitidos · {summary.Errors} errores.";
        }
        catch (ArgumentException exception)
        {
            ShowOperationError(exception.Message);
        }
        catch (DirectoryNotFoundException exception)
        {
            ShowOperationError(exception.Message);
        }
        catch (IOException exception)
        {
            ShowOperationError(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowOperationError(exception.Message);
        }
        finally
        {
            StartButton.IsEnabled = true;
            BackButton.IsEnabled = true;
            SelectSourceButton.IsEnabled = true;
            SelectDestinationButton.IsEnabled = true;
        }
    }

    private void UpdateProgress(OperationProgress update)
    {
        OperationProgressBar.IsIndeterminate = update.IsScanning;
        if (!update.IsScanning)
        {
            OperationProgressBar.Value = update.Total == 0
                ? 100
                : update.Processed * 100.0 / update.Total;
        }
        StatusTextBlock.Text = update.Message;
        LogTextBox.AppendText(update.Message + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }

    private void ShowOperationError(string message)
    {
        OperationProgressBar.IsIndeterminate = false;
        StatusTextBlock.Text = $"La operación no pudo completarse: {message}";
        LogTextBox.AppendText($"[ERROR] {message}{Environment.NewLine}");
        MessageBox.Show(this, message, "Error en la operación", MessageBoxButton.OK, MessageBoxImage.Error);
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
