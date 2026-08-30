using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace AiImageBatchEditor.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void BrowseInput_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            _viewModel.InputFolder = dialog.SelectedPath;
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            _viewModel.OutputFolder = dialog.SelectedPath;
    }

    private void AddReferences_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            foreach (var file in dialog.FileNames)
                _viewModel.AddReference(file);
    }

    private void RemoveReference_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Forms.Button button && button.Tag is string path)
            _viewModel.RemoveReference(path);
    }

    private void ChooseTestImage_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            _viewModel.SetTestImage(dialog.FileName);
    }

    private async void TestEdit_Click(object sender, RoutedEventArgs e)
        => await _viewModel.TestEditAsync();

    private async void ProcessBatch_Click(object sender, RoutedEventArgs e)
        => await _viewModel.ProcessBatchAsync();

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => _viewModel.Cancel();
}
