using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UBot.Pages;
using UBot.Pages.Dialogs;

namespace UBot.Views.User
{
    public class DashboardView : BaseView, INotifyPropertyChanged
    {
        public DashboardView()
        {
            OpenControlPanel = new Command(ExecuteOpenControlPanel);

            _webPrep = new Whatsapp.Web.AccPreparation();
            _isFree = true;
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

        public Command OpenControlPanel { get; }

        private bool _isFree;

        private readonly Whatsapp.Web.AccPreparation _webPrep;

        #endregion

        private async void ExecuteOpenControlPanel()
        {
            if (!_isFree)
                return;

            var result = await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new ControlPanel()) as ActionProfileWork?;

            if (result is null)
                return;

            if (!result.Value.IsNewsLetter)
            {
                _isFree = false;
                var _activeTask = Task.Run(async () =>
                {
                    await _webPrep.Run(string.IsNullOrEmpty(Text) ? await File.ReadAllTextAsync(Globals.Setup.PathToFileTextWarm) : Text, result.Value);
                });

                await _activeTask;
                await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", "Прогрев был завершен", false));
                _isFree = true;
            }
        }
    }
}
