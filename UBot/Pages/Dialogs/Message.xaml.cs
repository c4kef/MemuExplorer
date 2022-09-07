using CommunityToolkit.Maui.Views;
using UBot.Views.Dialogs;

namespace UBot.Pages.Dialogs;

enum MessageCloseStatus
{
    Ok,
    No
}

public partial class Message : Popup
{
	public Message(string title, string content, bool isNoVisible)
	{
		InitializeComponent();

        BindingContext = new MessageView(title, content, isNoVisible);
	}

    private void ClickOk(object sender, EventArgs e) => this.Close(MessageCloseStatus.Ok);
    
	private void ClickNo(object sender, EventArgs e) => this.Close(MessageCloseStatus.No);
}