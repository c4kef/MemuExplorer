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


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UBot.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(nameof(IWindow), (handler, view) =>
            {
                var mauiWindow = handler.VirtualView;
                var nativeWindow = handler.PlatformView;
                nativeWindow.Activate();
                IntPtr windowHandle = WindowNative.GetWindowHandle(nativeWindow);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    var m_AppWindow = appWindow;
                    var titleBar = m_AppWindow.TitleBar;

                    titleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;

                    titleBar.ButtonInactiveBackgroundColor = titleBar.ButtonBackgroundColor = titleBar.BackgroundColor = Windows.UI.Color.FromArgb(255, 3, 4, 9);
                
                }
            });
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}