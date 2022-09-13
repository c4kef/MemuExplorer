using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UBot.Controls;
using UBot.Pages;
using UBot.Pages.Dialogs;

namespace UBot.Views.User
{
    public class SettingsView : BaseView, INotifyPropertyChanged
    {
        public SettingsView()
        {
            ButtonClick = new Command<string>(async (id) => await ButtonClickExecute(int.Parse(id)));
        }

        public Command ButtonClick { get; }

        #region variables color
        public Color ButtonPickerFileNameColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFileNames) ? "Active" : "NotActive");

        public Color ButtonPickerImageColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFileImage) ? "Active" : "NotActive");

        public Color ButtonPickerAccountsColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), Directory.Exists(Globals.Setup.PathToFolderAccounts) ? "Active" : "NotActive");

        public Color ButtonPickerTextWarmColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFileTextWarm) ? "Active" : "NotActive");
        #endregion

        #region variables text
        public int? PinCodeAccount
        {
            get => Globals.Setup.PinCodeAccount;
            set => SetProperty(ref Globals.Setup.PinCodeAccount, value);
        }

        public int? NumberRepetitionsActions
        {
            get => Globals.Setup.NumberRepetitionsActions;
            set => SetProperty(ref Globals.Setup.NumberRepetitionsActions, value);
        }

        public int? CountMessages
        {
            get => Globals.Setup.CountMessages;
            set => SetProperty(ref Globals.Setup.CountMessages, value);
        }

        public int? CountThreads
        {
            get => Globals.Setup.CountThreads;
            set => SetProperty(ref Globals.Setup.CountThreads, value);
        }

        public int? MinTrustLevel
        {
            get => Globals.Setup.MinTrustLevel;
            set => SetProperty(ref Globals.Setup.MinTrustLevel, value);
        }

        public int? DelaySendMessageFrom
        {
            get => Globals.Setup.DelaySendMessageFrom;
            set => SetProperty(ref Globals.Setup.DelaySendMessageFrom, value);
        }

        public int? DelaySendMessageTo
        {
            get => Globals.Setup.DelaySendMessageTo;
            set => SetProperty(ref Globals.Setup.DelaySendMessageTo, value);
        }

        public string NumbersForNewsletter
        {
            get => Globals.Setup.NumbersForNewsletter;
            set => SetProperty(ref Globals.Setup.NumbersForNewsletter, value);
        }
        #endregion

        private async Task ButtonClickExecute(int idButton)
        {
            string pick = string.Empty;

            switch (idButton)
            {
                case 1:
                    pick = await App.GetInstance().FolderPicker.PickFile(".txt");

                    if (string.IsNullOrEmpty(pick))
                        return;

                    Globals.Setup.PathToFileNames = pick;
                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerFileNameColor));

                    break;

                case 2:
                    pick = await App.GetInstance().FolderPicker.PickFile(".ova");

                    if (string.IsNullOrEmpty(pick))
                        return;

                    Globals.Setup.PathToFileImage = pick;
                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerImageColor));

                    break;

                case 3:
                    pick = await App.GetInstance().FolderPicker.PickFolder();

                    if (string.IsNullOrEmpty(pick))
                        return;

                    Globals.Setup.PathToFolderAccounts = pick;
                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerAccountsColor));

                    break;

                case 4:
                    pick = await App.GetInstance().FolderPicker.PickFile(".txt");

                    if (string.IsNullOrEmpty(pick))
                        return;

                    Globals.Setup.PathToFileTextWarm = pick;
                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerTextWarmColor));

                    break;
            }
        }
    }
}