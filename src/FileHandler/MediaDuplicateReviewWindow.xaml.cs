using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileHandler.Models;
using FileHandler.Services;
using Microsoft.Win32;

namespace FileHandler;

public partial class MediaDuplicateReviewWindow : Window
{
    private readonly MediaDuplicateService _service = new();
    private readonly ObservableCollection<DuplicateGroupRow> _groups = new();
    private readonly ObservableCollection<DuplicateFileRow> _files = new();
    private MediaDuplicateScanResult? _scan;
    private bool _isBusy;

    public MediaDuplicateReviewWindow()
    {
        InitializeComponent();
        DuplicateFilesGrid.ItemsSource = _files;
        LogPathTextBlock.Text = $"Los registros se guardan en: {_service.LogPath}";
    }

    private async void SelectSource_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecciona la carpeta para buscar imágenes y vídeos duplicados",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        SourcePathTextBox.Text = dialog.FolderName;
        await ScanSourceAsync(dialog.FolderName);
    }

    private async void ScanAgain_Click(object sender, RoutedEventArgs e)
    {
        var source = SourcePathTextBox.Text.Trim();
        if (source.Length > 0)
        {
            await ScanSourceAsync(source);
        }
    }

    private async Task ScanSourceAsync(string source)
    {
        _scan = null;
        ClearResults();
        ScanProgressBar.Value = 0;
        ScanProgressBar.IsIndeterminate = true;
        StatusTextBlock.Text = "Escaneando imágenes y vídeos...";
        CountsTextBlock.Text = string.Empty;
        SummaryTextBlock.Text = string.Empty;
        _isBusy = true;
        UpdateControls();

        var progress = new Progress<OperationProgress>(UpdateProgress);
        try
        {
            var scan = await Task.Run(() => _service.Scan(source, progress));
            _scan = scan;

            foreach (var group in scan.DuplicateGroups)
            {
                var groupRow = new DuplicateGroupRow(group);
                _groups.Add(groupRow);
                for (var index = 0; index < group.Files.Count; index++)
                {
                    var fileRow = new DuplicateFileRow(
                        group.Files[index],
                        groupRow.GroupHeaderText,
                        index > 0);
                    fileRow.PropertyChanged += DuplicateFile_PropertyChanged;
                    groupRow.Files.Add(fileRow);
                    _files.Add(fileRow);
                }
            }

            DuplicateFilesGrid.Visibility = _files.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            DuplicateEmptyTextBlock.Visibility = _groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            CountsTextBlock.Text =
                $"{scan.ImageCount} imágenes · {scan.VideoCount} vídeos · " +
                $"{scan.DuplicateGroups.Count} grupos idénticos";
            if (scan.ScanErrors > 0 || scan.VerificationErrors > 0)
            {
                CountsTextBlock.Text +=
                    $" · {scan.ScanErrors} errores de escaneo · {scan.VerificationErrors} errores de comparación";
            }

            var selectedCount = _files.Count(file => file.IsSelectedForDeletion);
            SummaryTextBlock.Text = scan.DuplicateGroups.Count == 0
                ? "No se encontraron archivos multimedia con contenido idéntico."
                : $"Se han marcado automáticamente {selectedCount} copias para la Papelera. " +
                  "Revisa la lista y desmarca las que quieras conservar. Se deja una copia sin marcar por grupo.";
            StatusTextBlock.Text = "Búsqueda completada.";

            UpdateControls();
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
        catch (SecurityException exception)
        {
            ShowOperationError(exception.Message);
        }
        finally
        {
            _isBusy = false;
            ScanProgressBar.IsIndeterminate = false;
            ScanAgainButton.IsEnabled = SourcePathTextBox.Text.Trim().Length > 0;
            UpdateControls();
        }
    }

    private async void DuplicateFilesGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is not DuplicateFileRow file || !file.TryBeginThumbnailLoad())
        {
            return;
        }

        file.Thumbnail = await Task.Run(() => MediaThumbnailProvider.Load(file.File.Path, file.File.IsImage));
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        DuplicateFilesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        DuplicateFilesGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var scan = _scan;
        var selectedFiles = _groups
            .SelectMany(group => group.Files)
            .Where(file => file.IsSelectedForDeletion)
            .ToArray();
        if (scan is null || selectedFiles.Length == 0 || HasGroupWithEveryFileSelected())
        {
            UpdateControls();
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Se enviarán {selectedFiles.Length} archivos multimedia seleccionados a la Papelera de reciclaje. " +
            $"Se conservará al menos una copia de cada grupo.{Environment.NewLine}{Environment.NewLine}" +
            "La Papelera permite recuperarlos mientras no se vacíe. ¿Quieres continuar?",
            "Confirmar eliminación de duplicados",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _isBusy = true;
        DeleteSelectedButton.IsEnabled = false;
        ScanAgainButton.IsEnabled = false;
        ScanProgressBar.Value = 0;
        ScanProgressBar.IsIndeterminate = false;
        SummaryTextBlock.Text = string.Empty;
        var progress = new Progress<OperationProgress>(UpdateProgress);
        try
        {
            var summary = await Task.Run(() => _service.SendSelectedToRecycleBin(
                scan,
                selectedFiles.Select(file => file.File.Path).ToArray(),
                progress));

            SummaryTextBlock.Text =
                $"Enviados a la Papelera: {summary.SentToRecycleBin.Count} · " +
                $"Modificados desde el escaneo: {summary.ChangedSinceScan} · Errores: {summary.Errors}.";
            if (summary.SentToRecycleBin.Count > 0)
            {
                ClearResults();
                _scan = null;
                CountsTextBlock.Text = string.Empty;
                StatusTextBlock.Text = "Vuelve a buscar para actualizar los resultados.";
            }
        }
        catch (ArgumentException exception)
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
        catch (InvalidOperationException exception)
        {
            ShowOperationError(exception.Message);
        }
        catch (SecurityException exception)
        {
            ShowOperationError(exception.Message);
        }
        finally
        {
            _isBusy = false;
            ScanAgainButton.IsEnabled = SourcePathTextBox.Text.Trim().Length > 0;
            UpdateControls();
        }
    }

    private void DuplicateFile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DuplicateFileRow.IsSelectedForDeletion))
        {
            UpdateControls();
        }
    }

    private void UpdateControls()
    {
        var selectedCount = _groups
            .SelectMany(group => group.Files)
            .Count(file => file.IsSelectedForDeletion);
        var invalidGroup = false;
        foreach (var group in _groups)
        {
            var groupIsInvalid = group.Files.Count > 0
                && group.Files.All(file => file.IsSelectedForDeletion);
            invalidGroup |= groupIsInvalid;
            foreach (var file in group.Files)
            {
                file.IsGroupSelectionInvalid = groupIsInvalid;
            }
        }

        DeleteSelectedButton.IsEnabled =
            !_isBusy && _scan is not null && selectedCount > 0 && !invalidGroup;
        SelectionWarningTextBlock.Text = invalidGroup
            ? "Deja al menos una copia sin marcar en cada grupo."
            : selectedCount > 0
                ? $"Hay {selectedCount} copias marcadas; queda al menos una sin marcar por grupo."
                : string.Empty;
        SelectionWarningBorder.Background = invalidGroup
            ? new SolidColorBrush(Color.FromRgb(185, 28, 28))
            : Brushes.Transparent;
        SelectionWarningBorder.Padding = invalidGroup
            ? new Thickness(8, 4, 8, 4)
            : new Thickness(0);
        SelectionWarningTextBlock.Foreground = invalidGroup
            ? Brushes.White
            : new SolidColorBrush(Color.FromRgb(180, 83, 9));
        SelectionWarningTextBlock.FontWeight = invalidGroup
            ? FontWeights.Bold
            : FontWeights.Normal;

        SelectSourceButton.IsEnabled = !_isBusy;
        ScanAgainButton.IsEnabled = !_isBusy && SourcePathTextBox.Text.Trim().Length > 0;
    }

    private bool HasGroupWithEveryFileSelected() =>
        _groups.Any(group =>
            group.Files.Count > 0 && group.Files.All(file => file.IsSelectedForDeletion));

    private void UpdateProgress(OperationProgress update)
    {
        ScanProgressBar.IsIndeterminate = update.IsScanning;
        if (!update.IsScanning)
        {
            ScanProgressBar.Value = update.Total == 0
                ? 100
                : update.Processed * 100.0 / update.Total;
        }

        StatusTextBlock.Text = update.Message;
        ActivityLogTextBox.AppendText(update.Message + Environment.NewLine);
        ActivityLogTextBox.ScrollToEnd();
    }

    private void ShowOperationError(string message)
    {
        ScanProgressBar.IsIndeterminate = false;
        StatusTextBlock.Text = $"La operación no pudo completarse: {message}";
        ActivityLogTextBox.AppendText($"[ERROR] {message}{Environment.NewLine}");
        MessageBox.Show(this, message, "Error al buscar duplicados", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void ClearResults()
    {
        foreach (var row in _groups.SelectMany(group => group.Files))
        {
            row.PropertyChanged -= DuplicateFile_PropertyChanged;
        }

        _groups.Clear();
        _files.Clear();
        DuplicateFilesGrid.Visibility = Visibility.Collapsed;
        DuplicateEmptyTextBlock.Visibility = Visibility.Visible;
        UpdateControls();
    }

    private static string FormatSize(long? size)
    {
        if (!size.HasValue)
        {
            return "No disponible";
        }

        var value = (double)size.Value;
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private sealed class DuplicateGroupRow
    {
        public DuplicateGroupRow(MediaDuplicateGroup group)
        {
            Group = group;
            Files = new ObservableCollection<DuplicateFileRow>();
            var repeatedBytes = (decimal)group.Size * (group.Files.Count - 1);
            DisplayText = $"{group.Files.Count} archivos idénticos · {FormatSize((long)Math.Min(repeatedBytes, long.MaxValue))} repetidos";
            HashPreview = $"SHA-256 {group.Sha256[..12]}...";
            GroupHeaderText = $"{DisplayText} · {HashPreview}";
        }

        public MediaDuplicateGroup Group { get; }
        public ObservableCollection<DuplicateFileRow> Files { get; }
        public string DisplayText { get; }
        public string HashPreview { get; }
        public string GroupHeaderText { get; }
    }

    private sealed class DuplicateFileRow : INotifyPropertyChanged
    {
        private bool _thumbnailLoadStarted;
        private bool _isSelectedForDeletion;
        private bool _isGroupSelectionInvalid;
        private ImageSource? _thumbnail;

        public DuplicateFileRow(
            MediaDuplicateFile file,
            string groupHeaderText,
            bool isSelectedForDeletion)
        {
            File = file;
            GroupHeaderText = groupHeaderText;
            _isSelectedForDeletion = isSelectedForDeletion;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MediaDuplicateFile File { get; }
        public string GroupHeaderText { get; }
        public string Path => File.Path;
        public string SizeText => FormatSize(File.Size);
        public string TypeText => File.IsImage ? "Imagen" : "Vídeo";
        public string TypeIcon => File.IsImage ? "FOTO" : "VÍDEO";

        public bool IsGroupSelectionInvalid
        {
            get => _isGroupSelectionInvalid;
            set
            {
                if (_isGroupSelectionInvalid == value)
                {
                    return;
                }

                _isGroupSelectionInvalid = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGroupSelectionInvalid)));
            }
        }

        public bool TryBeginThumbnailLoad()
        {
            if (_thumbnailLoadStarted)
            {
                return false;
            }

            _thumbnailLoadStarted = true;
            return true;
        }

        public bool IsSelectedForDeletion
        {
            get => _isSelectedForDeletion;
            set
            {
                if (_isSelectedForDeletion == value)
                {
                    return;
                }

                _isSelectedForDeletion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelectedForDeletion)));
            }
        }

        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (ReferenceEquals(_thumbnail, value))
                {
                    return;
                }

                _thumbnail = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            }
        }
    }

}
