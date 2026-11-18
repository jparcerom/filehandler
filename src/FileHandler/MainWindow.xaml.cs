using System.IO;
using System.Windows;
using Microsoft.Win32;
using FileHandler.Models;
using FileHandler.Services;

namespace FileHandler;

public partial class MainWindow : Window
{
    private readonly DateOrganizer _organizer = new();

    public MainWindow()
    {
        InitializeComponent();
        LogPathTextBlock.Text = $"Los registros se guardan en: {_organizer.LogPath}";
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
