using CommunityToolkit.Maui.Views;
using UBot.Controls;
using UBot.Views.Dialogs;

namespace UBot.Pages.Dialogs;

public partial class ControlPanel : Popup
{
    public Dictionary<Button, bool> _selectedRadio;
    public Dictionary<Button, bool> _selectedCheckBox;

    private ActionProfileWork _actionProfileWork;

    public ControlPanel()
	{
		InitializeComponent();

        _selectedCheckBox= new Dictionary<Button, bool>();
        _selectedRadio = new Dictionary<Button, bool>();

        _actionProfileWork.TemplateMessages = new List<TemplateMessage>();
    }

    private void PressStart(object sender, EventArgs e)
    {
        foreach (var radioBtn in _selectedRadio)
            switch (int.Parse(radioBtn.Key.ClassId))
            {
                case 1:
                    _actionProfileWork.IsNewsLetter = radioBtn.Key.Text.Contains("Рассылка") && radioBtn.Value;
                    break;
                case 2:
                    _actionProfileWork.IsWeb = radioBtn.Key.Text.Contains("Web") && radioBtn.Value;
                    break;
            }

        foreach (var checkedBtn in _selectedCheckBox)
            switch (checkedBtn.Key.Text)
            {
                case "Проверка":
                    _actionProfileWork.CheckBan = checkedBtn.Value;
                    break;
                case "Прогрев":
                    _actionProfileWork.Warm = checkedBtn.Value;
                    break;
                case "Чекер":
                    _actionProfileWork.CheckNumberValid = checkedBtn.Value;
                    break;
                case "Сканирование":
                    _actionProfileWork.Scaning = checkedBtn.Value;
                    break;
            }

        this.Close((_selectedCheckBox.Values.Any(checkbox => checkbox) || _selectedRadio.Values.Any(radio => radio)) ? _actionProfileWork : null);
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