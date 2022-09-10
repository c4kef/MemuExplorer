using UBot.Views.User;

namespace UBot.Pages.User;

public partial class Dashboard : ContentView
{
	public Dashboard()
	{
		InitializeComponent();

		this.BindingContext = new DashboardView();
	}
}