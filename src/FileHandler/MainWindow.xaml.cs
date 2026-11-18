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
    private readonly MediaCleanupService _mediaCleanupService = new();
    private readonly WindowsDuplicateService _windowsDuplicateService = new();
    private MediaScanResult? _mediaScanResult;
    private MediaCopySummary? _mediaCopySummary;
    private WindowsDuplicateScanResult? _windowsDuplicateScan;
    private bool _mediaOperationRunning;
    private bool _duplicatesOperationRunning;

    public MainWindow()
    {
        InitializeComponent();
        LogPathTextBlock.Text = $"Los registros se guardan en: {_organizer.LogPath}";
        MediaLogPathTextBlock.Text = $"Los registros se guardan en: {_mediaCopyService.LogPath}";
        DuplicatesLogPathTextBlock.Text = $"Los registros se guardan en: {_windowsDuplicateService.LogPath}";
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

    private void MoveDuplicates_Click(object sender, RoutedEventArgs e)
    {
        HomeView.Visibility = Visibility.Collapsed;
        WindowsDuplicatesView.Visibility = Visibility.Visible;
        ResetDuplicatesView();
    }

    private void BackFromDuplicates_Click(object sender, RoutedEventArgs e)
    {
        if (_duplicatesOperationRunning)
        {
            return;
        }

        HomeView.Visibility = Visibility.Visible;
        WindowsDuplicatesView.Visibility = Visibility.Collapsed;
    }

    private async void SelectDuplicatesSource_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder("Selecciona la carpeta para buscar duplicados");
        if (selectedPath is not null)
        {
            DuplicatesSourcePathTextBox.Text = selectedPath;
            DuplicatesDestinationPathTextBox.Clear();
            await ScanDuplicatesSourceAsync(selectedPath);
        }
    }

    private async Task ScanDuplicatesSourceAsync(string source)
    {
        _windowsDuplicateScan = null;
        DuplicatesCountsTextBlock.Text = string.Empty;
        DuplicatesSummaryTextBlock.Text = string.Empty;
        DuplicatesLogTextBox.Clear();
        DuplicatesProgressBar.Value = 0;
        DuplicatesProgressBar.IsIndeterminate = true;
        DuplicatesStatusTextBlock.Text = "Escaneando y verificando archivos numerados...";
        _duplicatesOperationRunning = true;
        SelectDuplicatesSourceButton.IsEnabled = false;
        DuplicatesBackButton.IsEnabled = false;
        UpdateDuplicatesControls();

        var progress = new Progress<OperationProgress>(UpdateDuplicatesProgress);
        try
        {
            var scan = await Task.Run(() => _windowsDuplicateService.Scan(source, progress));
            _windowsDuplicateScan = scan;
            DuplicatesCountsTextBlock.Text =
                $"{scan.CandidateCount} candidatos · {scan.VerifiedDuplicates.Count} verificados · " +
                $"{scan.UnverifiedCount} no verificados · {scan.FallbackOriginalCount} grupos sin original " +
                "(se retuvo el sufijo más bajo)";
            if (scan.ScanErrors > 0 || scan.VerificationErrors > 0)
            {
                DuplicatesCountsTextBlock.Text +=
                    $" · {scan.ScanErrors} errores de escaneo · {scan.VerificationErrors} errores de verificación";
            }

            if (scan.VerifiedDuplicates.Count == 0)
            {
                DuplicatesSummaryTextBlock.Text = "No hay archivos verificados para mover.";
            }
        }
        catch (ArgumentException exception)
        {
            ShowDuplicatesOperationError(exception.Message);
        }
        catch (DirectoryNotFoundException exception)
        {
            ShowDuplicatesOperationError(exception.Message);
        }
        catch (IOException exception)
        {
            ShowDuplicatesOperationError(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowDuplicatesOperationError(exception.Message);
        }
        catch (SecurityException exception)
        {
            ShowDuplicatesOperationError(exception.Message);
        }
        finally
        {
            _duplicatesOperationRunning = false;
            SelectDuplicatesSourceButton.IsEnabled = true;
            DuplicatesBackButton.IsEnabled = true;
            UpdateDuplicatesControls();
        }
    }

    private void SelectDuplicatesDestination_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder("Selecciona la carpeta donde guardar los duplicados");
        if (selectedPath is not null)
        {
            DuplicatesDestinationPathTextBox.Text = selectedPath;
            UpdateDuplicatesControls();
        }
    }

    private void UpdateDuplicatesControls()
    {
        var hasVerifiedFiles = !_duplicatesOperationRunning &&
                               _windowsDuplicateScan is { VerifiedDuplicates.Count: > 0 };
        SelectDuplicatesDestinationButton.IsEnabled = hasVerifiedFiles;
        MoveDuplicatesButton.IsEnabled = hasVerifiedFiles &&
                                         DuplicatesDestinationPathTextBox.Text.Trim().Length > 0;
    }

    private async void StartMoveDuplicates_Click(object sender, RoutedEventArgs e)
    {
        var scan = _windowsDuplicateScan;
        var destination = DuplicatesDestinationPathTextBox.Text.Trim();
        if (scan is null || scan.VerifiedDuplicates.Count == 0 || destination.Length == 0)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Se moverán {scan.VerifiedDuplicates.Count} archivos cuyo contenido fue verificado. " +
            $"Los originales usados como referencia y los archivos no coincidentes se conservarán.{Environment.NewLine}{Environment.NewLine}" +
            $"Destino: {destination}{Environment.NewLine}{Environment.NewLine}" +
            "Si ya existe un archivo con el mismo nombre, se añadirá un sufijo. ¿Quieres continuar?",
            "Confirmar movimiento de duplicados",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _duplicatesOperationRunning = true;
        DuplicatesProgressBar.Value = 0;
        DuplicatesProgressBar.IsIndeterminate = false;
        DuplicatesSummaryTextBlock.Text = string.Empty;
        DuplicatesBackButton.IsEnabled = false;
        SelectDuplicatesSourceButton.IsEnabled = false;
        UpdateDuplicatesControls();
        var progress = new Progress<OperationProgress>(UpdateDuplicatesProgress);
        try
        {
            var summary = await Task.Run(() => _windowsDuplicateService.Move(scan, destination, progress));
            DuplicatesSummaryTextBlock.Text =
                $"Movidos: {summary.Moved} · Renombrados por colisión: {summary.CollisionRenames} · " +
                $"No verificados: {summary.UnverifiedCount} · Cambiados desde el escaneo: {summary.ChangedSinceScan} · " +
                $"Errores de movimiento: {summary.Errors} · Errores de escaneo/verificación: " +
                $"{summary.ScanErrors}/{summary.VerificationErrors}.";
            _windowsDuplicateScan = null;
        }
        catch (ArgumentException exception)
        {
            ShowDuplicatesOperationError(exception.Message);
        }
        catch (DirectoryNotFoundException exception)
        {
            ShowDuplicatesOperationError(exception.Message);
        }
        catch (IOException exception)
        {
            ShowDuplicatesOperationError(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowDuplicatesOperationError(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowDuplicatesOperationError(exception.Message);
        }
        catch (SecurityException exception)
        {
            ShowDuplicatesOperationError(exception.Message);
        }
        finally
        {
            _duplicatesOperationRunning = false;
            DuplicatesBackButton.IsEnabled = true;
            SelectDuplicatesSourceButton.IsEnabled = true;
            UpdateDuplicatesControls();
        }
    }

    private void UpdateDuplicatesProgress(OperationProgress update)
    {
        DuplicatesProgressBar.IsIndeterminate = update.IsScanning;
        if (!update.IsScanning)
        {
            DuplicatesProgressBar.Value = update.Total == 0
                ? 100
                : update.Processed * 100.0 / update.Total;
        }
        DuplicatesStatusTextBlock.Text = update.Message;
        DuplicatesLogTextBox.AppendText(update.Message + Environment.NewLine);
        DuplicatesLogTextBox.ScrollToEnd();
    }

    private void ShowDuplicatesOperationError(string message)
    {
        DuplicatesProgressBar.IsIndeterminate = false;
        DuplicatesStatusTextBlock.Text = $"La operación no pudo completarse: {message}";
        DuplicatesLogTextBox.AppendText($"[ERROR] {message}{Environment.NewLine}");
        MessageBox.Show(this, message, "Error con duplicados", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void ResetDuplicatesView()
    {
        _windowsDuplicateScan = null;
        DuplicatesSourcePathTextBox.Clear();
        DuplicatesDestinationPathTextBox.Clear();
        DuplicatesCountsTextBlock.Text = string.Empty;
        DuplicatesSummaryTextBlock.Text = string.Empty;
        DuplicatesLogTextBox.Clear();
        DuplicatesProgressBar.Value = 0;
        DuplicatesProgressBar.IsIndeterminate = false;
        DuplicatesStatusTextBlock.Text = "Selecciona la carpeta de origen para buscar duplicados.";
        SelectDuplicatesSourceButton.IsEnabled = true;
        DuplicatesBackButton.IsEnabled = true;
        UpdateDuplicatesControls();
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
        _mediaCopySummary = null;
        CopyImagesCheckBox.IsChecked = false;
        CopyVideosCheckBox.IsChecked = false;
        DeleteImagesCheckBox.IsChecked = false;
        DeleteVideosCheckBox.IsChecked = false;
        DeleteMediaSection.Visibility = Visibility.Collapsed;
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
            string.Join(Environment.NewLine, selectedItems) +
            $"{Environment.NewLine}{Environment.NewLine}Las colisiones se resolverán con sufijos; no se sobrescribirá ningún archivo.",
            "Confirmar copia",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _mediaCopySummary = null;
        DeleteImagesCheckBox.IsChecked = false;
        DeleteVideosCheckBox.IsChecked = false;
        DeleteMediaSection.Visibility = Visibility.Collapsed;
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
            _mediaCopySummary = summary;
            DeleteImagesCheckBox.Content = $"Eliminar originales de imágenes ({summary.ImagesCopied})";
            DeleteVideosCheckBox.Content = $"Eliminar originales de vídeos ({summary.VideosCopied})";
            DeleteImagesCheckBox.IsChecked = false;
            DeleteVideosCheckBox.IsChecked = false;
            DeleteMediaSection.Visibility = summary.SuccessfulCopies.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
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
            UpdateMediaDeleteControls();
        }
    }

    private void MediaDeleteSelection_Changed(object sender, RoutedEventArgs e)
    {
        UpdateMediaDeleteControls();
    }

    private void UpdateMediaDeleteControls()
    {
        var summary = _mediaCopySummary;
        var canDelete = summary is not null && !_mediaOperationRunning;
        var imageCopies = summary?.SuccessfulCopies.Count(copy => copy.IsImage) ?? 0;
        var videoCopies = summary?.SuccessfulCopies.Count(copy => !copy.IsImage) ?? 0;

        DeleteImagesCheckBox.IsEnabled = canDelete && imageCopies > 0;
        DeleteVideosCheckBox.IsEnabled = canDelete && videoCopies > 0;
        DeleteMediaButton.IsEnabled = canDelete &&
            (DeleteImagesCheckBox.IsChecked == true || DeleteVideosCheckBox.IsChecked == true);
    }

    private async void DeleteCopiedMedia_Click(object sender, RoutedEventArgs e)
    {
        var scan = _mediaScanResult;
        var copySummary = _mediaCopySummary;
        var deleteImages = DeleteImagesCheckBox.IsChecked == true;
        var deleteVideos = DeleteVideosCheckBox.IsChecked == true;
        if (scan is null || copySummary is null || (!deleteImages && !deleteVideos))
        {
            MessageBox.Show(
                this,
                "Selecciona al menos un tipo de original para eliminar.",
                "No hay archivos seleccionados",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var removeEmptyDirectories =
            MediaCleanupService.ShouldRemoveEmptyDirectories(scan, deleteImages, deleteVideos);
        var selectedItems = new List<string>();
        if (deleteImages)
        {
            selectedItems.Add($"{copySummary.SuccessfulCopies.Count(copy => copy.IsImage)} imágenes");
        }
        if (deleteVideos)
        {
            selectedItems.Add($"{copySummary.SuccessfulCopies.Count(copy => !copy.IsImage)} vídeos");
        }

        var confirmationMessage =
            $"Se intentarán eliminar estos originales:{Environment.NewLine}{string.Join(", ", selectedItems)}." +
            $"{Environment.NewLine}{Environment.NewLine}Solo se borrarán si siguen coincidiendo por SHA-256 con su copia. " +
            "Los archivos que no se copiaron o que cambiaron se conservarán.";
        confirmationMessage += removeEmptyDirectories
            ? $"{Environment.NewLine}{Environment.NewLine}También se intentarán eliminar las subcarpetas vacías, " +
              "de abajo arriba. Las carpetas no vacías se conservarán."
            : $"{Environment.NewLine}{Environment.NewLine}No se eliminarán subcarpetas porque no se han seleccionado " +
              "todos los tipos multimedia presentes en el origen.";

        var confirmation = MessageBox.Show(
            this,
            confirmationMessage,
            "Confirmar eliminación de originales",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _mediaOperationRunning = true;
        SelectMediaSourceButton.IsEnabled = false;
        MediaBackButton.IsEnabled = false;
        UpdateMediaCopyControls();
        UpdateMediaDeleteControls();
        MediaProgressBar.Value = 0;
        MediaProgressBar.IsIndeterminate = false;
        var progress = new Progress<OperationProgress>(UpdateMediaProgress);
        try
        {
            var summary = await Task.Run(() => _mediaCleanupService.DeleteCopiedFiles(
                scan,
                copySummary.SuccessfulCopies,
                deleteImages,
                deleteVideos,
                progress));
            MediaSummaryTextBlock.Text =
                $"Eliminados: {summary.ImagesDeleted} imágenes · {summary.VideosDeleted} vídeos · " +
                $"{summary.EmptyDirectoriesDeleted} subcarpetas vacías · " +
                $"{summary.FilesSkipped} conservados · {summary.Errors} errores.";
            DeleteMediaSection.Visibility = Visibility.Collapsed;
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
            UpdateMediaDeleteControls();
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
        _mediaCopySummary = null;
        MediaSourcePathTextBox.Clear();
        CopyImagesCheckBox.IsChecked = false;
        CopyVideosCheckBox.IsChecked = false;
        DeleteImagesCheckBox.IsChecked = false;
        DeleteVideosCheckBox.IsChecked = false;
        DeleteMediaSection.Visibility = Visibility.Collapsed;
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
        UpdateMediaDeleteControls();
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
