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
        public Color CurrentColor { get; set; }
    }

    public class ManagerView : BaseView, INotifyPropertyChanged
    {
        public ManagerView()
        {
            //(Color)ResourceHelper.FindResource(MainPage.GetInstance(), "NotActive");
            UpdateListEmulators();
        }

        public ObservableCollection<DataEmulator> Emulators { get; set; }

        private void UpdateListEmulators()
        {
            var emulators = new List<DataEmulator>();

            emulators.Add(new DataEmulator
            {
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Active"),
                Index = 0
            });
            emulators.Add(new DataEmulator
            {
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "NotActive"),
                Index = 1
            });
            emulators.Add(new DataEmulator
            {
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Active"),
                Index = 2
            });
            emulators.Add(new DataEmulator
            {
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Busy"),
                Index = 3
            });
            emulators.Add(new DataEmulator
            {
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Active"),
                Index = 4
            });
            emulators.Add(new DataEmulator
            {
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Active"),
                Index = 5
            });
            emulators.Add(new DataEmulator
            {
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Active"),
                Index = 6
            });
            emulators.Add(new DataEmulator
            {
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Active"),
                Index = 7
            });
            emulators.Add(new DataEmulator
            {
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Active"),
                Index = 8
            });

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
    }
}