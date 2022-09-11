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

        public bool IsBusy { get; private set; }
        public Command ClickNavigateButton { get; }

        private async Task AnimateSelectIndicator(Ellipse _indicator)
        {
            if (!Indicators.Any(indicator => indicator.ClassId == _indicator.ClassId))
                Indicators.Add(_indicator);

            await Parallel.ForEachAsync(Indicators.Where(indicator => indicator.ClassId != _indicator.ClassId).ToArray(), async (item, token) =>
            {
                await MainPage.GetInstance().Dispatcher.DispatchAsync(async () =>
                {
                    await MainPage.GetInstance().UserPanels[int.Parse(item.ClassId) - 1].FadeTo(0, 250, Easing.SinOut);
                    MainPage.GetInstance().UserPanels[int.Parse(item.ClassId) - 1].IsVisible = false;
                });

                await item.FadeTo(0, 250, Easing.SinOut);
            });

            await MainPage.GetInstance().Dispatcher.DispatchAsync(async () =>
            {
                MainPage.GetInstance().UserPanels[int.Parse(_indicator.ClassId) - 1].IsVisible = true;
                await MainPage.GetInstance().UserPanels[int.Parse(_indicator.ClassId) - 1].FadeTo(1, 250, Easing.SinIn);
            });

            await _indicator.FadeTo(1, 250, Easing.SinIn);

            IsBusy = false;
        }

        private async Task ExecuteClickNavigateButton(object data)
        {
            if (IsBusy)
                return;

            IsBusy = true;

            var selectIndicator = data as Ellipse;

            _ = Task.Factory.StartNew(async () => await AnimateSelectIndicator(selectIndicator), TaskCreationOptions.HideScheduler);
        }
    }
}
