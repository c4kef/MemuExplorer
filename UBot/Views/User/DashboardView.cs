using CommunityToolkit.Maui.Views;
using MemuLib.Core.Contacts;
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
using UBot.Whatsapp;

namespace UBot.Views.User
{
    public class DashboardView : BaseView, INotifyPropertyChanged
    {
        public DashboardView(Dashboard _dashboard)
        {
            Dashboard = _dashboard;
            OpenControlPanel = new Command(ExecuteOpenControlPanel);
            ShowLastAccountsPanel = new Command(ExecuteShowLastAccountsPanel);

            _webPrep = new Whatsapp.Web.AccPreparation();
            _webNewsletter = new Whatsapp.Web.Newsletter();
            _emPrep = new Whatsapp.AccPreparation();

        obj = new();
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
        public Command ShowLastAccountsPanel { get; }

        private bool _isFree;
        private object obj;

        private readonly Whatsapp.Web.AccPreparation _webPrep;
        private readonly Whatsapp.AccPreparation _emPrep;
        private readonly Whatsapp.Web.Newsletter _webNewsletter;

        #endregion

        public static DashboardView GetInstance() => _instance;

        private async void ExecuteShowLastAccountsPanel()
        {
            var arr = _webNewsletter.SendedMessagesCountFromAccount.TakeLast(10);

            var builder = new StringBuilder();
            for (var i = 0; i < arr.Count(); i++)
                builder.Append($"                              {i + 1}. {arr.ElementAt(i).Value}      {((i % 2 == 0) ? "" : "\n")}");


            PopupExtensions.ShowPopup(MainPage.GetInstance(), new Message("Информация", builder.ToString(), false));
        }

        List<string> _contacts = new List<string>();
        List<string> _usedPhonesUsers = new List<string>();

        private async void ExecuteOpenControlPanel()
        {
            /*_ = Task.Factory.StartNew(async () =>
            {

                _contacts = (await File.ReadAllLinesAsync(@"C:\Users\artem\Downloads\Telegram Desktop\AZ_100k-15.txt")).ToList();

                var client = new Client("77029926521", @"C:\Users\artem\source\repos\MemuExplorer\Data\Accounts\77029926521");
                await client.Web!.Init(false, @"C:\Users\artem\source\repos\MemuExplorer\Data\Accounts\77029926521\77029926521", "");

                //var client1 = new Client("77056710562", @"C:\Users\artem\source\repos\MemuExplorer\Data\Accounts\77056710562");
                //await client1.Web!.Init(false, @"C:\Users\artem\source\repos\MemuExplorer\Data\Accounts\77056710562\77056710562", "");

                var checkedPhones = new List<string>();
                var tasks = new List<Task>();
                var x = 0;

                tasks.Add(Task.Run(async () =>
                {
                    while (true)
                    {
                        var _tasks = new List<Task>();
                        var peopleReal = GetFreeNumbersUser();
                        if (peopleReal.Length == 0)
                            break;

                        foreach (var value in peopleReal)
                        {
                            _tasks.Add(Task.Factory.StartNew(async () =>
                            {
                                var res = await client.Web!.CheckValidPhone(value);
                                lock (obj)
                                {
                                    if (res)
                                        ++CompletedTasks;
                                    else
                                        ++DeniedTasks;

                                    checkedPhones.Add($"{value}:{res}");
                                    x++;
                                }
                            }, TaskCreationOptions.HideScheduler).Unwrap());
                        }

                        Task.WaitAll(_tasks.ToArray(), -1);
                        _tasks.Clear();
                    }
                }));

              /*  tasks.Add(Task.Run(async () =>
                {
                    while (true)
                    {
                        var _tasks = new List<Task>();
                        var peopleReal = GetFreeNumbersUser();
                        if (peopleReal.Length == 0)
                            break;

                        foreach (var value in peopleReal)
                        {
                            _tasks.Add(Task.Factory.StartNew(async () =>
                            {
                                var res = await client1.Web!.CheckValidPhone(value);
                                lock (obj)
                                {
                                    if (res)
                                        ++CompletedTasks;
                                    else
                                        ++DeniedTasks;

                                    checkedPhones.Add($"{value}: {res}");
                                    x++;
                                }
                            }, TaskCreationOptions.HideScheduler).Unwrap());
                        }

                        Task.WaitAll(_tasks.ToArray(), -1);
                        _tasks.Clear();
                    }
                }));

                Task.WaitAll(tasks.ToArray());

                await File.AppendAllLinesAsync("checked.csv", checkedPhones);
            });

            return;*/

            if (!_isFree)
            {
                PopupExtensions.ShowPopup(MainPage.GetInstance(), new Message("Информация", "Заявка на остановку была создана", false));
                _emPrep.IsStop = true;
                _webPrep.IsStop = true;
                _webNewsletter.IsStop = true;
                return;
            }

            var result = await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new ControlPanel()) as ActionProfileWork?;

            if (result is null)
                return;

            AllTasks = 0;
            CompletedTasks = 0;
            DeniedTasks = 0;
            AverageMessages = 0;
            AverageAllMessages = 0;

            Globals.KillChromeDriverProcesses();

            if ((result.Value.Warm || result.Value.CheckBan || result.Value.CheckNumberValid) && result.Value.IsWeb)
            {
                if (!File.Exists(Globals.Setup.PathToFileTextWarm) || !Directory.Exists(Globals.Setup.PathToFolderAccounts) || (Globals.Setup.CountThreads <= 1 && (!result.Value.CheckBan && !result.Value.CheckNumberValid)) || Globals.Setup.CountGroups < 1 || Globals.Setup.CountGroups > 9 || Globals.Setup.CountMessages < 1 || Globals.Setup.RepeatCounts < 1 || (result.Value.CheckNumberValid && !File.Exists(Globals.Setup.PathToCheckNumbers)))
                {
                    await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Ошибка", "Похоже вы не настроили мою девочку перед прогревом", false));
                    return;
                }

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
                if (!File.Exists(Globals.Setup.PathToFilePhones) || !Directory.Exists(Globals.Setup.PathToFolderAccounts) || Globals.Setup.CountThreads < 1 || Globals.Setup.CountMessages < 1 || string.IsNullOrEmpty(Text) || Text.Length < 5)
                {
                    await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Ошибка", "Похоже вы не настроили мою девочку перед рассылкой", false));
                    return;
                }

                _isFree = false;
                var _activeTask = Task.Run(async () =>
                {
                    await _webNewsletter.Run();
                });

                await _activeTask;
                await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", "Рассылка была завершена", false));
                _isFree = true;
            }

            if ((result.Value.Warm || result.Value.CheckBan || result.Value.Scaning) && !result.Value.IsWeb)
            {
                if (!File.Exists(Globals.Setup.PathToFileNames) || !File.Exists(Globals.Setup.PathToFileTextWarm) || !Directory.Exists(Globals.Setup.PathToFolderAccounts) || (ManagerView.GetInstance().Emulators.Count(emulator => emulator.IsEnabled) < Globals.Setup.CountGroups * Globals.Setup.CountThreads && !result.Value.CheckBan) || Globals.Setup.CountGroups < 1 || Globals.Setup.CountGroups > 9 || Globals.Setup.CountMessages < 1 || Globals.Setup.RepeatCounts < 1)
                {
                    await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Ошибка", "Похоже вы не настроили мою девочку перед прогревом", false));
                    return;
                }

                _isFree = false;
                var _activeTask = Task.Run(async () =>
                {
                    await _emPrep.Run(await File.ReadAllTextAsync(Globals.Setup.PathToFileTextWarm), result.Value);
                });

                await _activeTask;
                await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", "Прогрев был завершен", false));
                _isFree = true;
            }

            Globals.KillChromeDriverProcesses();
        }
    }
}
