using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Handlers;
using UBot.Pages.Dialogs;
using UBot.Views;

namespace UBot.Pages;

public partial class Welcome : ContentPage
{
	public Welcome()
	{
		InitializeComponent();
		this.BindingContext = new WelcomeView();
    }

    private async void test(object sender, EventArgs e)
	{
        await Navigation.PushAsync(new MainPage(), true);
		//if ((MessageCloseStatus)(await PopupExtensions.ShowPopupAsync(this, new Message("", "OK", true))) == MessageCloseStatus.Ok)
	}
}