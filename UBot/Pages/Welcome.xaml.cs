using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using UBot.Pages.Dialogs;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using WinRT;
using WinRT.Interop;

namespace UBot.Pages;

public partial class Welcome : ContentPage
{
	public Welcome()
	{
		InitializeComponent();
	}
}