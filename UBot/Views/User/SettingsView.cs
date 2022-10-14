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

            ActionSelectEmulatorScan = new List<SelectEmulatorScan>();
            ActionSelectEmulatorScan.Add(new SelectEmulatorScan
            {
                Index = 0,
                Name = "Все"
            });
            ActionSelectEmulatorScan.Add(new SelectEmulatorScan
            {
                Index = 1,
                Name = "Получатель"
            });
            ActionSelectEmulatorScan.Add(new SelectEmulatorScan
            {
                Index = 2,
                Name = "Отправитель"
            });

            _instance = this;
        }

        public Command ButtonClick { get; }

        private static SettingsView _instance;

        #region variables color
        public Color ButtonPickerFileNameColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFileNames) ? "Active" : "NotActive");

        public Color ButtonPickerImageColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFileImage) ? "Active" : "NotActive");

        public Color ButtonPickerAccountsColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), Directory.Exists(Globals.Setup.PathToFolderAccounts) ? "Active" : "NotActive");

        public Color ButtonPickerTextWarmColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFileTextWarm) ? "Active" : "NotActive");

        public Color ButtonPickerGroupsColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFileGroups) ? "Active" : "NotActive");

        public Color ButtonPickerChatBotsColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFileChatBots) ? "Active" : "NotActive");

        public Color ButtonPickerPeoplesColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFilePeoples) ? "Active" : "NotActive");

        public Color ButtonPickerTextPeopleWarmColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFileTextPeopleWarm) ? "Active" : "NotActive");

        public Color ButtonPickerPhonesColor =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFilePhones) ? "Active" : "NotActive");

        public Color ButtonPickerFileProxy =>
            (Color)ResourceHelper.FindResource(MainPage.GetInstance(), File.Exists(Globals.Setup.PathToFileProxy) ? "Active" : "NotActive");
        #endregion

        #region variables text
        public SelectEmulatorScan? SelectEmulatorScan
        {
            get => Globals.Setup.SelectEmulatorScan;
            set => SetProperty(ref Globals.Setup.SelectEmulatorScan, value);
        }

        public List<SelectEmulatorScan> ActionSelectEmulatorScan { get; set; }

        public int? PinCodeAccount
        {
            get => Globals.Setup.PinCodeAccount;
            set => SetProperty(ref Globals.Setup.PinCodeAccount, value);
        }

        public int? CountGroups
        {
            get => Globals.Setup.CountGroups;
            set => SetProperty(ref Globals.Setup.CountGroups, value);
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

        public bool RemoveAvatar
        {
            get => Globals.Setup.RemoveAvatar;
            set => SetProperty(ref Globals.Setup.RemoveAvatar, value);
        }

        #endregion

        public static SettingsView GetInstance() => _instance;

        private async Task ButtonClickExecute(int idButton)
        {
            string pick = string.Empty;

            switch (idButton)
            {
                case 1:
                    if (string.IsNullOrEmpty(Globals.Setup.PathToFileNames))
                    {
                        pick = await App.GetInstance().FolderPicker.PickFile(".csv");

                        if (string.IsNullOrEmpty(pick))
                            return;

                        Globals.Setup.PathToFileNames = pick;
                    }
                    else
                        Globals.Setup.PathToFileNames = string.Empty;

                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerFileNameColor));

                    break;

                case 2:
                    if (string.IsNullOrEmpty(Globals.Setup.PathToFileImage))
                    {
                        pick = await App.GetInstance().FolderPicker.PickFile(".ova");

                        if (string.IsNullOrEmpty(pick))
                            return;

                        Globals.Setup.PathToFileImage = pick;
                    }
                    else
                        Globals.Setup.PathToFileImage = string.Empty;

                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerImageColor));

                    break;

                case 3:
                    if (string.IsNullOrEmpty(Globals.Setup.PathToFolderAccounts))
                    {
                        pick = await App.GetInstance().FolderPicker.PickFolder();

                        if (string.IsNullOrEmpty(pick))
                            return;

                        Globals.Setup.PathToFolderAccounts = pick;
                    }
                    else
                        Globals.Setup.PathToFolderAccounts = string.Empty;
                    
                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerAccountsColor));

                    break;

                case 4:
                    if (string.IsNullOrEmpty(Globals.Setup.PathToFileTextWarm))
                    {
                        pick = await App.GetInstance().FolderPicker.PickFile(".txt");

                        if (string.IsNullOrEmpty(pick))
                            return;

                        Globals.Setup.PathToFileTextWarm = pick;
                    }
                    else
                        Globals.Setup.PathToFileTextWarm = string.Empty;

                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerTextWarmColor));

                    break;

                case 5:
                    if (string.IsNullOrEmpty(Globals.Setup.PathToFileGroups))
                    {
                        pick = await App.GetInstance().FolderPicker.PickFile(".txt");

                        if (string.IsNullOrEmpty(pick))
                            return;

                        Globals.Setup.PathToFileGroups = pick;
                    }
                    else
                        Globals.Setup.PathToFileGroups = string.Empty;

                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerGroupsColor));

                    break;

                case 6:
                    if (string.IsNullOrEmpty(Globals.Setup.PathToFileChatBots))
                    {
                        pick = await App.GetInstance().FolderPicker.PickFile(".txt");

                        if (string.IsNullOrEmpty(pick))
                            return;

                        Globals.Setup.PathToFileChatBots = pick;
                    }
                    else
                        Globals.Setup.PathToFileChatBots = string.Empty;
                    
                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerChatBotsColor));

                    break;

                case 7:
                    if (string.IsNullOrEmpty(Globals.Setup.PathToFilePeoples))
                    {
                        pick = await App.GetInstance().FolderPicker.PickFile(".txt");

                        if (string.IsNullOrEmpty(pick))
                            return;

                        Globals.Setup.PathToFilePeoples = pick;
                    }
                    else
                        Globals.Setup.PathToFilePeoples = string.Empty;

                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerPeoplesColor));

                    break;

                case 8:
                    if (string.IsNullOrEmpty(Globals.Setup.PathToFileTextPeopleWarm))
                    {
                        pick = await App.GetInstance().FolderPicker.PickFile(".txt");

                        if (string.IsNullOrEmpty(pick))
                            return;

                        Globals.Setup.PathToFileTextPeopleWarm = pick;
                    }
                    else
                        Globals.Setup.PathToFileTextPeopleWarm = string.Empty;

                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerTextPeopleWarmColor));

                    break;

                case 9:
                    if (string.IsNullOrEmpty(Globals.Setup.PathToFilePhones))
                    {
                        pick = await App.GetInstance().FolderPicker.PickFile(".txt");

                        if (string.IsNullOrEmpty(pick))
                            return;

                        Globals.Setup.PathToFilePhones = pick;
                    }
                    else
                        Globals.Setup.PathToFilePhones = string.Empty;

                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerPhonesColor));

                    break;

                case 10:
                    if (string.IsNullOrEmpty(Globals.Setup.PathToFileProxy))
                    {
                        pick = await App.GetInstance().FolderPicker.PickFile(".txt");

                        if (string.IsNullOrEmpty(pick))
                            return;

                        Globals.Setup.PathToFileProxy = pick;
                    }
                    else
                        Globals.Setup.PathToFileProxy = string.Empty;

                    await Globals.SaveSetup();

                    OnPropertyChanged(nameof(ButtonPickerFileProxy));

                    break;
            }
        }
    }
}