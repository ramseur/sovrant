using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Sovrant.Desktop.Views.Dialogs;
using Sovrant.Tools.Extended;

namespace Sovrant.Desktop.Adapters;

public sealed class DesktopUserInputProvider : IUserInputProvider
{
    public async Task<string> AskAsync(string question, CancellationToken ct = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new UserInputDialog(question);
            var mainWindow = App.MainWindow;
            if (mainWindow is null)
                return $"[User input requested: {question}]";

            return await dialog.ShowDialog<string>(mainWindow) ?? string.Empty;
        });
    }
}
