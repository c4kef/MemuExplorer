using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Handlers;
using UBot.Pages.Dialogs;

namespace UBot.Pages;

public partial class Loading : ContentPage
{
    public Loading()
	{
		InitializeComponent();
		_ = Task.Factory.StartNew(BackgroundLoad);
    }

	private async Task BackgroundLoad()
	{
		var count = 0f;
		while (count < 1.1)
		{
            await progressBar.ProgressTo(count += 0.01f, 500, Easing.Linear);
			await Task.Delay(100);
		}
	}
}