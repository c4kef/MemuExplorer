using CommunityToolkit.Maui.Views;
using UBot.Controls;
using UBot.Views.Dialogs;

namespace UBot.Pages.Dialogs;

public partial class ControlPanel : Popup
{
    public Dictionary<Button, bool> _selectedRadio;
    public Dictionary<Button, bool> _selectedCheckBox;

    public ControlPanel()
	{
		InitializeComponent();

        _selectedCheckBox = _selectedRadio = new Dictionary<Button, bool>();
    }


    private void PressRadioButton(object sender, EventArgs e)
    {
        var btn = sender as Button;

        _selectedRadio[btn] = (_selectedRadio.ContainsKey(btn) ? !_selectedRadio[btn] : true);

        foreach (var _btn in _selectedRadio.ToList())
            if (_btn.Key.ClassId == btn.ClassId && _btn.Key != btn)
            {
                _btn.Key.BackgroundColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "NotActive");
                _selectedRadio[_btn.Key] = false;
            }


        btn.BackgroundColor = (Color)(_selectedRadio[btn] ? ResourceHelper.FindResource(MainPage.GetInstance(), "Active") : ResourceHelper.FindResource(MainPage.GetInstance(), "NotActive"));
    }

    private void PressCheckBox(object sender, EventArgs e)
    {
        var btn = sender as Button;

        if (_selectedCheckBox.Any(checkbox => checkbox.Key.ClassId == "33" && checkbox.Value) && btn.ClassId != "33")
            return;

        _selectedCheckBox[btn] = (_selectedCheckBox.ContainsKey(btn) ? !_selectedCheckBox[btn] : true);

        if (btn.ClassId == "33")
            foreach (var _btn in _selectedCheckBox.ToList())
                if (_btn.Key != btn)
                {
                    _btn.Key.BackgroundColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "NotActive");
                    _selectedCheckBox[_btn.Key] = false;
                }

        btn.BackgroundColor = (Color)(_selectedCheckBox[btn] ? ResourceHelper.FindResource(MainPage.GetInstance(), "Active") : ResourceHelper.FindResource(MainPage.GetInstance(), "NotActive"));
    }
}