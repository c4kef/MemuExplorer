using UBot.Views.User;

namespace UBot.Pages.User;

public partial class Manager : ContentView
{
	public Manager()
	{
		InitializeComponent();

		this.BindingContext = new ManagerView();
	}
}