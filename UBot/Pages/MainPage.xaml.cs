using UBot.Views;

namespace UBot.Pages;

public partial class MainPage : ContentPage
{
	public readonly List<ContentView> UserPanels;

    public MainPage()
	{
		InitializeComponent();
		_mainPage = this;

        UserPanels = new List<ContentView>();

        UserPanels.Add(DashboardPanel);
        UserPanels.Add(SettingsPanel);
        UserPanels.Add(ManagerPanel);

        this.BindingContext = new MainPageView();
		(this.BindingContext as MainPageView).Indicators.Add(SelectShadow1);//Чтобы при следующем клике мы могли сразу скрыть индикатор
	}

	private static MainPage _mainPage;

	public static MainPage GetInstance() => _mainPage;
}