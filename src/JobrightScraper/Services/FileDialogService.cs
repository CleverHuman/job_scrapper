using Microsoft.Win32;

namespace JobrightScraper.Services;

public sealed class FileDialogService
{
    public string? PickSavePath(string filter, string defaultFileName, string defaultExt)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
            DefaultExt = defaultExt,
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
