using UBot.Pages;

namespace UBot
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new NavigationPage(new Welcome());
            //MainPage = new AppShell();
        }
    }
}