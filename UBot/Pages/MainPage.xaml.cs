using Microsoft.Maui.Controls.Shapes;
using UBot.Views;

namespace UBot.Pages;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
        _mainPage = this;

        this.BindingContext = new MainPageView();
        (this.BindingContext as MainPageView).Indicators.Add(SelectShadow1);//Чтобы при следующем клике мы могли сразу скрыть индикатор
    }

	private static MainPage _mainPage;

	public static MainPage GetInstance() => _mainPage;
}