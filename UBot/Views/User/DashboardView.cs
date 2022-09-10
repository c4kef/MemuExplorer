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
        }

        public Command OpenControlPanel { get; }
        
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

        private async void ExecuteOpenControlPanel()
        {
            await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new ControlPanel());
        }
    }
}
