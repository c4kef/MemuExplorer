using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Reflection.Metadata;
using Windows.ApplicationModel.Activation;
using Windows.UI.ViewManagement;
using Microsoft.Maui.Controls.Compatibility.Hosting;
using Microsoft.Maui.LifecycleEvents;
using Windows.Graphics;
using Windows.UI.WindowManagement;
using WinRT.Interop;
using AppWindow = Microsoft.UI.Windowing.AppWindow;
using AppWindowTitleBar = Microsoft.UI.Windowing.AppWindowTitleBar;
using Colors = Microsoft.UI.Colors;
using Windows.Foundation;
using Windows.UI.Popups;
using System.Runtime.InteropServices;
using WinRT;
using Microsoft.UI.Xaml.Controls;

namespace UBot.WinUI
{
    public class UI
    {
        public static IAsyncOperation<IUICommand> ShowDialogAsync(string content, string title = null)
        {
            var dlg = new MessageDialog(content, title ?? "");
            var handle = GetActiveWindow();
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException();
            dlg.As<IInitializeWithWindow>().Initialize(handle);
            return dlg.ShowAsync();
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        internal interface IInitializeWithWindow
        {
            void Initialize(IntPtr hwnd);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(wndId);
        }
    }
}
