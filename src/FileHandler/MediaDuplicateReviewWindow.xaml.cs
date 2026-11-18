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
    private MediaDuplicateScanResult? _scan;
    private bool _isBusy;

    public MediaDuplicateReviewWindow()
    {
        InitializeComponent();
        DuplicateGroupsListBox.ItemsSource = _groups;
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
                foreach (var file in group.Files)
                {
                    var fileRow = new DuplicateFileRow(file);
                    fileRow.PropertyChanged += DuplicateFile_PropertyChanged;
                    groupRow.Files.Add(fileRow);
                }

                _groups.Add(groupRow);
            }

            DuplicateResultsGrid.Visibility = _groups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            DuplicateEmptyTextBlock.Visibility = _groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            CountsTextBlock.Text =
                $"{scan.ImageCount} imágenes · {scan.VideoCount} vídeos · " +
                $"{scan.DuplicateGroups.Count} grupos idénticos";
            if (scan.ScanErrors > 0 || scan.VerificationErrors > 0)
            {
                CountsTextBlock.Text +=
                    $" · {scan.ScanErrors} errores de escaneo · {scan.VerificationErrors} errores de comparación";
            }

            SummaryTextBlock.Text = scan.DuplicateGroups.Count == 0
                ? "No se encontraron archivos multimedia con contenido idéntico."
                : "Revisa cada grupo y marca los archivos que quieres enviar a la Papelera. " +
                  "Se conservará al menos una copia de cada grupo.";
            StatusTextBlock.Text = "Búsqueda completada.";

            if (_groups.Count > 0)
            {
                DuplicateGroupsListBox.SelectedIndex = 0;
            }
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

    private async void DuplicateGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DuplicateGroupsListBox.SelectedItem is not DuplicateGroupRow selectedGroup)
        {
            DuplicateFilesGrid.ItemsSource = null;
            UpdateControls();
            return;
        }

        DuplicateFilesGrid.ItemsSource = selectedGroup.Files;
        UpdateControls();

        var rowsWithoutThumbnails = selectedGroup.Files
            .Where(file => file.Thumbnail is null)
            .ToArray();
        if (rowsWithoutThumbnails.Length == 0)
        {
            return;
        }

        var thumbnails = await Task.WhenAll(rowsWithoutThumbnails.Select(file =>
            Task.Run(() => MediaThumbnailProvider.Load(file.File.Path, file.File.IsImage))));
        if (!ReferenceEquals(DuplicateGroupsListBox.SelectedItem, selectedGroup))
        {
            return;
        }

        for (var index = 0; index < rowsWithoutThumbnails.Length; index++)
        {
            rowsWithoutThumbnails[index].Thumbnail = thumbnails[index];
        }
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
        var invalidGroup = HasGroupWithEveryFileSelected();

        DeleteSelectedButton.IsEnabled =
            !_isBusy && _scan is not null && selectedCount > 0 && !invalidGroup;
        SelectionWarningTextBlock.Text = invalidGroup
            ? "Desmarca al menos una copia de cada grupo para conservarla."
            : selectedCount > 0
                ? "Se conservará al menos una copia de cada grupo."
                : string.Empty;

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
        DuplicateFilesGrid.ItemsSource = null;
        DuplicateResultsGrid.Visibility = Visibility.Collapsed;
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
        }

        public MediaDuplicateGroup Group { get; }
        public ObservableCollection<DuplicateFileRow> Files { get; }
        public string DisplayText { get; }
        public string HashPreview { get; }
    }

    private sealed class DuplicateFileRow : INotifyPropertyChanged
    {
        private bool _isSelectedForDeletion;
        private ImageSource? _thumbnail;

        public DuplicateFileRow(MediaDuplicateFile file)
        {
            File = file;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MediaDuplicateFile File { get; }
        public string Path => File.Path;
        public string SizeText => FormatSize(File.Size);
        public string TypeText => File.IsImage ? "Imagen" : "Vídeo";
        public string TypeIcon => File.IsImage ? "FOTO" : "VÍDEO";

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
