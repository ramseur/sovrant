using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Sovrant.Desktop.Views.Dialogs;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Desktop.Adapters;

public sealed class DesktopConfirmationHandler : IToolConfirmationHandler
{
    public async Task<bool> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new ConfirmToolDialog(toolName, input.ToString());
            var mainWindow = App.MainWindow;
            if (mainWindow is null)
                return false;

            return await dialog.ShowDialog<bool>(mainWindow);
        });
    }
}
