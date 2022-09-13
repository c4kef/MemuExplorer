using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UBot.Controls;
using UBot.Pages;
using UBot.Pages.Dialogs;

namespace UBot.Views.User
{
    public struct DataEmulator
    {
        public int Index { get; set; }
        public bool IsEnabled { get; set; }
        public Color CurrentColor { get; set; }
    }

    public class ManagerView : BaseView, INotifyPropertyChanged
    {
        public ManagerView()
        {
            AddEmulator = new Command<ImageButton>(async (button) => await AddEmulatorExecute(button));
            SelectEmulator = new Command<DataEmulator>(async (data) => await SelectEmulatorExecute(data));
            ActionEmulator = new Command<object>(async (action) => await ActionEmulatorExecute((int)action));
        }

        #region variables
        public DataEmulator SelectedEmulator { get; set; }

        public ImageSource ScreenPicture { get; set; }

        public Command ActionEmulator { get; set; }

        public Command SelectEmulator { get; set; }

        public Command AddEmulator { get; set; }

        public ObservableCollection<DataEmulator> Emulators { get; set; }
        #endregion

        private void UpdateListEmulators()
        {
            var emulators = new List<DataEmulator>();
            //To-Do

            if (Emulators is null)
                Emulators = new ObservableCollection<DataEmulator>(emulators.ToArray());
            else
            {
                Emulators.Clear();
                foreach (var emulator in emulators)
                    Emulators.Add(emulator);
            }

            OnPropertyChanged(nameof(Emulators));
        }

        private async Task SelectEmulatorExecute(DataEmulator data)
        {
            if (!data.IsEnabled)
            {
                await ActionEmulatorExecute(0, data);
                return;
            }

            SelectedEmulator = data;
            OnPropertyChanged(nameof(SelectedEmulator));
        }

        private async Task ActionEmulatorExecute(int action, DataEmulator? data = null)
        {
            if (SelectedEmulator.Index == -1)
                return;

            var emulator = Emulators.ToList().FindIndex(emulator => emulator.Index == ((data is not null) ? data?.Index : SelectedEmulator.Index));

            Emulators[emulator] = new DataEmulator()
            {
                Index = Emulators[emulator].Index,
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Active"),
                IsEnabled = true
            };

            //OnPropertyChanged(nameof(Emulators));
        }

        private async Task AddEmulatorExecute(ImageButton button)
        {
            button.BackgroundColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Busy");
            Emulators ??= new ObservableCollection<DataEmulator>();

            Emulators.Add(new DataEmulator()
            {
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "NotActive"),
                Index = new Random().Next(0, 20),
                IsEnabled = false
            });

            OnPropertyChanged(nameof(Emulators));
            button.BackgroundColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "NotActive");
        }
    }
}