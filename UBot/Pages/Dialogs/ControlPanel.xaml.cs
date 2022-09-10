using CommunityToolkit.Maui.Views;
using UBot.Views.Dialogs;

namespace UBot.Pages.Dialogs;

public partial class ControlPanel : Popup
{
	public ControlPanel()
	{
		InitializeComponent();

        //BindingContext = new MessageView(title, content, isNoVisible);
	}
}