using UBot.Controls;
using WindowsFolderPicker = Windows.Storage.Pickers.FolderPicker;
using WindowsFilePicker = Windows.Storage.Pickers.FileOpenPicker;

namespace UBot.Platforms.Windows
{
    public class FolderPicker : IFolderPicker
    {
        public async Task<string> PickFolder()
        {
            var folderPicker = new WindowsFolderPicker();

            folderPicker.FileTypeFilter.Add("*");

            var hwnd = ((MauiWinUIWindow)App.Current.Windows[0].Handler.PlatformView).WindowHandle;

            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var result = await folderPicker.PickSingleFolderAsync();

            return result?.Path ?? string.Empty;
        }

        public async Task<string> PickFile(string filter)
        {
            var filePicker = new WindowsFilePicker();

            filePicker.FileTypeFilter.Add(filter);

            var hwnd = ((MauiWinUIWindow)App.Current.Windows[0].Handler.PlatformView).WindowHandle;

            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

            var result = await filePicker.PickSingleFileAsync();

            return result?.Path ?? string.Empty;
        }
    }
}
