using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Handlers;
using UBot.Pages.Dialogs;

namespace UBot.Pages;

public partial class Welcome : ContentPage
{
	public Welcome()
	{
		InitializeComponent();
    }

    private async void test(object sender, EventArgs e)
	{
        await Navigation.PushAsync(new MainPage(), true);
		//if ((MessageCloseStatus)(await PopupExtensions.ShowPopupAsync(this, new Message("", "OK", true))) == MessageCloseStatus.Ok)
	}
}