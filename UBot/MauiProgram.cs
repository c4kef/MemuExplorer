using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls.Xaml;
using System.Runtime.InteropServices;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Markup;
using UBot.Pages;
using UBot.Pages.Dialogs;
using UBot.Views.Dialogs;

#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
#endif

namespace UBot
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("Segoe UI Bold Italic.ttf", "SegoeUIBoldItalic");
                    fonts.AddFont("Segoe UI Bold.ttf", "SegoeUIBold");
                    fonts.AddFont("Segoe UI Italic.ttf", "SegoeUIItalic");
                    fonts.AddFont("Segoe UI.ttf", "SegoeUI");
                })
                .UseMauiCommunityToolkit()
                .UseMauiCommunityToolkitCore()
                .UseMauiCommunityToolkitMarkup();

#if WINDOWS
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddWindows(wndLifeCycleBuilder =>
                {
                    wndLifeCycleBuilder.OnWindowCreated(window =>
                    {
                        window.ExtendsContentIntoTitleBar = false;
                        IntPtr nativeWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);

                        WindowId win32WindowsId = Win32Interop.GetWindowIdFromWindow(nativeWindowHandle);

                        AppWindow winuiAppWindow = AppWindow.GetFromWindowId(win32WindowsId);

                        (winuiAppWindow.Presenter as OverlappedPresenter).IsResizable = false;
                        (winuiAppWindow.Presenter as OverlappedPresenter).IsMaximizable = false;
                        winuiAppWindow.Resize(new SizeInt32(1280, 720));
                    });
                });
            });
#endif     

            return builder.Build();
        }
    }
}