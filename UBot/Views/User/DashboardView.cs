using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel.Communication;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UBot.Pages;
using UBot.Pages.Dialogs;
using UBot.Pages.User;

namespace UBot.Views.User
{
    public class DashboardView : BaseView, INotifyPropertyChanged
    {
        public DashboardView(Dashboard _dashboard)
        {
            Dashboard = _dashboard;
            OpenControlPanel = new Command(ExecuteOpenControlPanel);

            _webPrep = new Whatsapp.Web.AccPreparation();
            _webNewsletter = new Whatsapp.Web.Newsletter();

            _isFree = true;
            _instance = this;
        }

        #region UI variables

        private string _text;
        public string Text
        {
            get => _text;
            set { SetProperty(ref _text, value); }
        }

        private int _allTasks;
        public int AllTasks
        {
            get => _allTasks;
            set { SetProperty(ref _allTasks, value); }
        }

        private int _completedTasks;
        public int CompletedTasks
        {
            get => _completedTasks;
            set { SetProperty(ref _completedTasks, value); }
        }

        private int _deniedTasks;
        public int DeniedTasks
        {
            get => _deniedTasks;
            set { SetProperty(ref _deniedTasks, value); }
        }

        private int _averageMessages;
        public int AverageMessages
        {
            get => _averageMessages;
            set { SetProperty(ref _averageMessages, value); }
        }

        private int _averageAllMessages;
        public int AverageAllMessages
        {
            get => _averageAllMessages;
            set { SetProperty(ref _averageAllMessages, value); }
        }

        #endregion

        #region variables

        private static DashboardView _instance;
        
        public Dashboard Dashboard { get; private set; }

        public Command OpenControlPanel { get; }

        private bool _isFree;

        private readonly Whatsapp.Web.AccPreparation _webPrep;
        private readonly Whatsapp.Web.Newsletter _webNewsletter;

        #endregion

        public static DashboardView GetInstance() => _instance;

        private async void ExecuteOpenControlPanel()
        {
            if (!_isFree)
                return;

            var result = await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new ControlPanel()) as ActionProfileWork?;

            if (result is null)
                return;

            AllTasks = 0;
            CompletedTasks = 0;
            DeniedTasks = 0;
            AverageMessages = 0;
            AverageAllMessages = 0;

            Globals.KillChromeDriverProcesses();

            if ((result.Value.Warm || result.Value.CheckBan) && result.Value.IsWeb)
            {
                _isFree = false;
                var _activeTask = Task.Run(async () =>
                {
                    await _webPrep.Run(await File.ReadAllTextAsync(Globals.Setup.PathToFileTextWarm), result.Value);
                });

                await _activeTask;
                await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", "Подготовка была завершена", false));
                _isFree = true;
            }

            if (result.Value.IsNewsLetter && result.Value.IsWeb)
            {
                _isFree = false;
                var _activeTask = Task.Run(async () =>
                {
                    await _webNewsletter.Run();
                });

                await _activeTask;
                await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", "Рассылка была завершена", false));
                _isFree = true;
            }

            Globals.KillChromeDriverProcesses();
        }
    }
}
