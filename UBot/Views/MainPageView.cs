using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UBot.Pages.Dialogs;

namespace UBot.Views
{
    public class MainPageView : BaseView, INotifyPropertyChanged
    {
        public MainPageView()
        {
            Indicators = new List<Ellipse>();

            ClickNavigateButton = new Command<object>(async (data) => await ExecuteClickNavigateButton(data));
        }
        
        public readonly List<Ellipse> Indicators;

        public Command ClickNavigateButton { get; }

        private async Task AnimateSelectIndicator(Ellipse _indicator)
        {
            if (!Indicators.Any(indicator => indicator.ClassId == _indicator.ClassId))
                Indicators.Add(_indicator);
            
            await _indicator.FadeTo(1, 250);

            await Parallel.ForEachAsync(Indicators.Where(indicator => indicator.ClassId != _indicator.ClassId).ToArray(), async (item, token) => await item.FadeTo(0, 250));
        }

        private async Task ExecuteClickNavigateButton(object data)
        {
            var selectIndicator = data as Ellipse;

            _ = Task.Factory.StartNew(async () => await AnimateSelectIndicator(selectIndicator), TaskCreationOptions.HideScheduler);
        }
    }
}
