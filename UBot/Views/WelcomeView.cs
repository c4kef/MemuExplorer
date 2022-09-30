using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UBot.Pages;
using UBot.Pages.Dialogs;

namespace UBot.Views;

public class WelcomeView : BaseView, INotifyPropertyChanged
{
    public WelcomeView()
    {
        var timeOfDay = DateTime.Now.TimeOfDay;
        if (timeOfDay.Hours >= 0 && timeOfDay.Hours < 12)
            Text = "Доброе утро";
        else if (timeOfDay.Hours >= 12 && timeOfDay.Hours < 16)
            Text = "Добрый день";
        else if (timeOfDay.Hours >= 16 && timeOfDay.Hours < 21)
            Text = "Добрый вечер";
        else if (timeOfDay.Hours >= 21 && timeOfDay.Hours < 24)
            Text = "Добрый ночи";
    }

    private string _text;
    public string Text
    {
        get => _text;
        set { SetProperty(ref _text, value); }
    }
}
