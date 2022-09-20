using UBot.Views.User;

namespace UBot.Pages.User;

public partial class Settings : ContentView
{
	public Settings()
	{
		InitializeComponent();

		this.BindingContext = new SettingsView();
	}

	private async void TextSave(object sender, TextChangedEventArgs e) => await Globals.SaveSetup();

	private async void PickerChanged(object sender, EventArgs e) => await Globals.SaveSetup();
}