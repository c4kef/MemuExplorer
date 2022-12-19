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
            ShowTemplateMessagesPanel = new Command(ExecuteShowTemplateMessagesPanel);
            ShowDetailBanPanel = new Command(ExecuteShowDetailBanPanel);

            _webPrep = new Whatsapp.Web.AccPreparation();
            _webNewsletter = new Whatsapp.Web.Newsletter();
            _webNewsletterLong = new Whatsapp.Web.NewsletterLong();
            _newsletter = new Whatsapp.Newsletter();
            _emPrep = new Whatsapp.AccPreparation();

            _isFree = true;
            _instance = this;
        }

        #region UI variables

        public string Text
        {
            get => Globals.Setup.TextMessage;
            set => SetProperty(ref Globals.Setup.TextMessage, value);
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

        public int DeniedTasksStart = 0;
        public int DeniedTasksWork = 0;

        #endregion

        #region variables

        private static DashboardView _instance;
        
        public Dashboard Dashboard { get; private set; }
        public Command OpenControlPanel { get; }
        public Command ShowLastAccountsPanel { get; }
        public Command ShowDetailBanPanel { get; }
        public Command ShowTemplateMessagesPanel { get; }

        private bool _isFree;

        private readonly Whatsapp.Web.AccPreparation _webPrep;
        private readonly Whatsapp.AccPreparation _emPrep;
        private readonly Whatsapp.Web.Newsletter _webNewsletter;
        private readonly Whatsapp.Web.NewsletterLong _webNewsletterLong;
        private readonly Whatsapp.Newsletter _newsletter;

        #endregion

        public static DashboardView GetInstance() => _instance;

        private async void ExecuteShowLastAccountsPanel()
        {
            var arr = _webNewsletter.SendedMessagesCountFromAccount.TakeLast(10);

            var builder = new StringBuilder();
            for (var i = 0; i < arr.Count(); i++)
                builder.Append($"                              {i + 1}. {arr.ElementAt(i).Value}      {((i % 2 == 0) ? "" : "\n")}");

            PopupExtensions.ShowPopup(MainPage.GetInstance(), new Message("Среднее", builder.ToString(), false));
        }

        private async void ExecuteShowTemplateMessagesPanel()
        {
            /*object _lock = new();
            var _usedPhones = new List<string>();
            var _usedPhonesUsers = new List<string>();
            while (true)
            {
                var result = Globals.GetAccounts(_usedPhones.ToArray(), true, _lock);

                if (result.Length == 0)
                {
                    if (_usedPhones.Count != 0)
                    {
                        _usedPhones.Clear();
                        continue;
                    }
                    else
                    {
                        await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Ошибка", "аккаунт не был найден", false));
                        return;
                    }
                }

                var (phone, path) = result[0];

                if (_usedPhones.Contains(phone))
                {
                    await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Ошибка", "Дубликат аккаунта", false));
                    continue;
                }

                _usedPhones.Add(phone);

                var countSendedMessages = 0;

                var client = new Client(phone, path);

                try
                {
                    await client.Web!.Init(false, $@"{path}\{new DirectoryInfo(path).Name}", "");
                }
                catch (Exception ex)
                {
                    await client.Web!.Free();
                    await Globals.TryMove(path, $@"{Globals.LogoutDirectory.FullName}\{phone}");
                    ++DashboardView.GetInstance().DeniedTasksStart;
                    continue;
                }

                await Task.Delay(10_000);

                try
                {
                    foreach (var peopleReal in await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhones))
                    {
                        if (string.IsNullOrEmpty(peopleReal) || _usedPhonesUsers.Contains(peopleReal))
                            continue;

                        if (!await client.Web!.IsConnected())
                            throw new Exception("Client has disconected");

                        _usedPhonesUsers.Add(peopleReal);

                        if (await client.Web!.SendText(peopleReal, SelectWord(string.Join('\n', DashboardView.GetInstance().Text.Split('\r').ToList()))))
                        {
                            ++DashboardView.GetInstance().CompletedTasks;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    await client.Web!.Free();
                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksWork;
                    await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
                    continue;
                }

                await Task.Delay(3_000);
                await client.Web!.Free();
                await Task.Delay((int)Globals.Setup.DelaySendMessageTo * 1000);
            }


            string SelectWord(string value)
            {
                var backValue = value;
                foreach (var match in new Regex(@"\{random=(.*?)\}", RegexOptions.Multiline).Matches(backValue))
                {
                    var arrText = match.ToString()!.Split("||").Select(val => val.Replace("{", "").Replace("}", "").Replace(Globals.TagRandom, "")).ToArray();
                    backValue = backValue.Replace(match.ToString()!, arrText[new Random().Next(0, arrText.Length)]);
                }
                return new Regex(@"\{([^)]*)\}").Replace(backValue, "").Replace("\"", "").Replace("\'", "");
            }
            return;*/
            var builder = new StringBuilder();
            if (_webNewsletter.TemplateMessagesInfo.Count > 0)
            {
                var arr = _webNewsletter.TemplateMessagesInfo;
                for (var i = 0; i < arr.Count; i++)
                    builder.AppendLine($"{arr.ElementAt(i).Key}: (Осталось {arr.ElementAt(i).Value.AllPhones}) (Выполнено {arr.ElementAt(i).Value.CurrentPhones})");
            }
            else
            {
                builder.AppendLine($"Отправлено: {_emPrep.TemplateWarm.sendedCount}");
                builder.AppendLine($"Прогрето: {_emPrep.TemplateWarm.warmedAccount}");
            }

            PopupExtensions.ShowPopup(MainPage.GetInstance(), new Message("Общее", builder.ToString(), false));
        }

        private async void ExecuteShowDetailBanPanel()
        {
            var builder = new StringBuilder();

            builder.AppendLine($"На старте: {DeniedTasksStart}");
            builder.AppendLine($"При работе: {DeniedTasksWork}");

            if (_emPrep.TemplateWarm.sendedBan > 0 || _emPrep.TemplateWarm.sendedBanPeople > 0)
            {
                builder.AppendLine($"Людям: {_emPrep.TemplateWarm.sendedBanPeople}");
                builder.AppendLine($"Ботам: {_emPrep.TemplateWarm.sendedBan}");
            }

            PopupExtensions.ShowPopup(MainPage.GetInstance(), new Message("Бананы🍌 ", builder.ToString(), false));
        }

        private async void ExecuteOpenControlPanel()
        {
            await Globals.SaveSetup();

            if (!_isFree)
            {
                PopupExtensions.ShowPopup(MainPage.GetInstance(), new Message("Информация", "Заявка на остановку была создана", false));
                _emPrep.IsStop = true;
                _newsletter.IsStop = true;
                _webPrep.IsStop = true;
                _webNewsletter.IsStop = true;
                _webNewsletterLong.IsStop = true;
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
            DeniedTasksStart = 0;
            DeniedTasksWork = 0;

            Globals.KillChromeDriverProcesses();

            if ((result.Value.Warm || result.Value.CheckBan || result.Value.CheckNumberValid) && result.Value.IsWeb)
            {
                if (!File.Exists(Globals.Setup.PathToFileTextWarm) || !Directory.Exists(Globals.Setup.PathToFolderAccounts) || (Globals.Setup.CountThreads <= 1 && (!result.Value.CheckBan && !result.Value.CheckNumberValid && !result.Value.WarmMethodIlya)) || Globals.Setup.CountGroups < 1 || Globals.Setup.CountGroups > 9 || Globals.Setup.RepeatCounts < 1 || (result.Value.CheckNumberValid && !File.Exists(Globals.Setup.PathToCheckNumbers)))
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

            if (result.Value.IsNewsLetter && !result.Value.WarmMethodLong && result.Value.IsWeb)
            {
                if (!File.Exists(Globals.Setup.PathToFilePhones) || !Directory.Exists(Globals.Setup.PathToFolderAccounts) || Globals.Setup.CountThreads < 1 || Globals.Setup.CountMessages < 1 || string.IsNullOrEmpty(Text) || Text.Length < 5)
                {
                    await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Ошибка", "Похоже вы не настроили мою девочку перед рассылкой", false));
                    return;
                }

                foreach (Match template in new Regex(@"\{tag=(.*?)\rtext=(.*?)\rphones=(.*?)\}").Matches(Text))
                    result.Value.TemplateMessages.Add(new TemplateMessage()
                    {
                        Tag = template.Groups[1].Value,
                        Text = template.Groups[2].Value,
                        PathPhones = new FileInfo(template.Groups[3].Value)
                    });

                if (result.Value.TemplateMessages.Count >= 1)
                {
                    if ((MessageCloseStatus)(await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", $"Проверьте шаблоны, верны?\n{string.Join("\n", result.Value.TemplateMessages.Select(template => template.Tag))}", true))) != MessageCloseStatus.Ok)
                        return;
                }

                _isFree = false;
                var _activeTask = Task.Run(async () =>
                {
                    await _webNewsletter.Run(result.Value);
                });

                await _activeTask;
                await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", "Рассылка была завершена", false));
                _isFree = true;
            }

            if (result.Value.IsNewsLetter && result.Value.WarmMethodLong && result.Value.IsWeb)
            {
                if (!File.Exists(Globals.Setup.PathToFilePhones) || !Directory.Exists(Globals.Setup.PathToFolderAccounts) || Globals.Setup.CountThreads < 1 || Globals.Setup.CountMessages < 1 || string.IsNullOrEmpty(Text) || Text.Length < 5)
                {
                    await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Ошибка", "Похоже вы не настроили мою девочку перед рассылкой", false));
                    return;
                }

                foreach (Match template in new Regex(@"\{tag=(.*?)\rtext=(.*?)\rphones=(.*?)\}").Matches(Text))
                    result.Value.TemplateMessages.Add(new TemplateMessage()
                    {
                        Tag = template.Groups[1].Value,
                        Text = template.Groups[2].Value,
                        PathPhones = new FileInfo(template.Groups[3].Value)
                    });

                if (result.Value.TemplateMessages.Count >= 1)
                {
                    if ((MessageCloseStatus)(await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", $"Проверьте шаблоны, верны?\n{string.Join("\n", result.Value.TemplateMessages.Select(template => template.Tag))}", true))) != MessageCloseStatus.Ok)
                        return;
                }

                _isFree = false;
                var _activeTask = Task.Run(async () =>
                {
                    await _webNewsletterLong.Run(result.Value);
                });

                await _activeTask;
                await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", "Рассылка была завершена", false));
                _isFree = true;
            }

            if (result.Value.IsNewsLetter && !result.Value.IsWeb)
            {
                if (!File.Exists(Globals.Setup.PathToFilePhones) || !Directory.Exists(Globals.Setup.PathToFolderAccounts) || !Directory.Exists(Globals.Setup.PathToDownloadsMemu) || ManagerView.GetInstance().Emulators.Count(emulator => emulator.IsEnabled) < 1 || Globals.Setup.CountMessages < 1 || string.IsNullOrEmpty(Text) || Text.Length < 5)
                {
                    await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Ошибка", "Похоже вы не настроили мою девочку перед рассылкой", false));
                    return;
                }

                foreach (Match template in new Regex(@"\{tag=(.*?)\rtext=(.*?)\rphones=(.*?)\}").Matches(Text))
                    result.Value.TemplateMessages.Add(new TemplateMessage()
                    {
                        Tag = template.Groups[1].Value,
                        Text = template.Groups[2].Value,
                        PathPhones = new FileInfo(template.Groups[3].Value)
                    });

                if (result.Value.TemplateMessages.Count >= 1)
                {
                    if ((MessageCloseStatus)(await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", $"Проверьте шаблоны, верны?\n{string.Join("\n", result.Value.TemplateMessages.Select(template => template.Tag))}", true))) != MessageCloseStatus.Ok)
                        return;
                }

                _isFree = false;
                var _activeTask = Task.Run(async () =>
                {
                    await _newsletter.Run(result.Value);
                });

                await _activeTask;
                await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", "Рассылка была завершена", false));
                _isFree = true;
            }

            if ((result.Value.Warm || result.Value.CheckBan || result.Value.Scaning || result.Value.WarmMethodIlya) && !result.Value.IsWeb)
            {
                if (!File.Exists(Globals.Setup.PathToFileNames) || (!File.Exists(Globals.Setup.PathToFileTextWarm) && (result.Value.Warm || result.Value.WarmMethodIlya)) || !Directory.Exists(Globals.Setup.PathToFolderAccounts) || !Directory.Exists(Globals.Setup.PathToDownloadsMemu) || (ManagerView.GetInstance().Emulators.Count(emulator => emulator.IsEnabled) < Globals.Setup.CountGroups * Globals.Setup.CountThreads && (!result.Value.CheckBan && !result.Value.WarmMethodIlya)) || Globals.Setup.CountGroups < 1 || Globals.Setup.CountGroups > 9 || (Globals.Setup.CountMessages < 1 && (!result.Value.CheckBan && !result.Value.CheckNumberValid && !result.Value.WarmMethodIlya)) || Globals.Setup.RepeatCounts < 1 
                    || ((result.Value.WarmMethodIlya) && (!Directory.Exists(Globals.Setup.PathToFolderAccountsAdditional) || !File.Exists(Globals.Setup.PathToFilePeoples) || !File.Exists(Globals.Setup.PathToFilePhonesContacts) || !File.Exists(Globals.Setup.PathToFilePhones) || Globals.Setup.CountMessageWarm < 1 || Globals.Setup.CountMessageWarmNewsletter < 1)))// || Globals.Setup.CountCritAliveAccountsToStopWarm < 1)))
                {
                    await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Ошибка", "Похоже вы не настроили мою девочку перед прогревом", false));
                    return;
                }

                _isFree = false;
                var _activeTask = Task.Run(async () =>
                {
                    await _emPrep.Run(File.Exists(Globals.Setup.PathToFileTextWarm) ? await File.ReadAllTextAsync(Globals.Setup.PathToFileTextWarm) : string.Empty, result.Value);
                });

                await _activeTask;
                await PopupExtensions.ShowPopupAsync(MainPage.GetInstance(), new Message("Информация", "Прогрев был завершен", false));
                _isFree = true;
            }

            Globals.KillChromeDriverProcesses();
        }
    }
}
