using UBot.Pages;
using UBot.Controls;

namespace UBot
{
    public partial class App : Application
    {
        public App(IFolderPicker folderPicker)
        {
            InitializeComponent();

            FolderPicker = folderPicker;
            _app = this;

            MainPage = new NavigationPage(new Welcome());

            //MainPage = new AppShell();
        }

        private static App _app;
        public readonly IFolderPicker FolderPicker;

        public static App GetInstance() => _app;
    }
}