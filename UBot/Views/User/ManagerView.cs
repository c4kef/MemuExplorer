using MemuLib.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using UBot.Controls;
using UBot.Pages;

namespace UBot.Views.User
{
    public struct DataEmulator
    {
        public int Index { get; set; }
        public bool IsEnabled { get; set; }
        public Color CurrentColor { get; set; }

        public static bool operator == (DataEmulator obj1, DataEmulator obj2)
        {
            return obj1.Index == obj2.Index && obj1.IsEnabled == obj2.IsEnabled && obj1.CurrentColor.Equals(obj2.CurrentColor);
        }

        public static bool operator != (DataEmulator obj1, DataEmulator obj2) => obj1.Index != obj2.Index || obj1.IsEnabled != obj2.IsEnabled || !Equals(obj1.CurrentColor, obj2.CurrentColor);
    }

    public class ManagerView : BaseView, INotifyPropertyChanged
    {
        public ManagerView()
        {
            AddEmulator = new Command<ImageButton>(async (button) => await AddEmulatorExecute(button));
            SelectEmulator = new Command<DataEmulator>(async (data) => await SelectEmulatorExecute(data));
            ActionEmulator = new Command<object>(async (action) => await ActionEmulatorExecute(int.Parse(action.ToString())));

            Instance = this;

            _ = Task.Factory.StartNew(UpdateListEmulators, TaskCreationOptions.LongRunning);
        }

        #region variables
        public DataEmulator SelectedEmulator { get; set; }

        public ImageSource ScreenPicture { get; set; }

        public Command ActionEmulator { get; set; }

        public Command SelectEmulator { get; set; }

        public Command AddEmulator { get; set; }

        public ObservableCollection<DataEmulator> Emulators { get; set; }

        private bool IsBusy;

        private static ManagerView Instance;
        #endregion

        public static ManagerView GetInstance() => Instance;

        private async Task UpdateListEmulators()
        {
            try
            {
                while (true)
                {

                    OnPropertyChanged(nameof(Emulators));

                    await Task.Delay(1_500);

                    if (IsBusy)
                        continue;

                    var emulators = new List<DataEmulator>();

                    var indexDevices = (await MemuCmd.ExecMemuc("listvms")).Split('\n').Select(line => line.Split(',')[0]).Where(index => int.TryParse(index, out _)).Select(int.Parse).ToArray();

                    IsBusy = true;

                    if (indexDevices.Length == 0)
                    {
                        Emulators.Clear();
                        continue;
                    }

                    foreach (var index in indexDevices)
                    {
                        var isEnabled = !(await MemuCmd.ExecMemuc($"isvmrunning -i {index}")).Contains("Not Running");

                        emulators.Add(new DataEmulator()
                        {
                            Index = index,
                            CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), isEnabled ? "Active" : "NotActive"),
                            IsEnabled = isEnabled
                        });
                    }

                    if (Emulators is null)
                        Emulators = new ObservableCollection<DataEmulator>(emulators.ToArray());
                    else
                    {
                        //Удаляем которых уже нет
                        foreach (var emulator in Emulators.ToArray().Where(_emulator => !indexDevices.Contains(_emulator.Index)))
                            Emulators.Remove(emulator);

                        //Добавляем эмуляторы
                        foreach (var emulator in emulators)
                        {
                            var _emulatorIndex = Emulators.ToList().FindIndex(_emulator => _emulator.Index == emulator.Index);

                            if (_emulatorIndex == -1)
                            {
                                Emulators.Add(emulator);
                                Emulators.Sort((a, b) => a.Index.CompareTo(b.Index));
                            }
                            else if (Emulators != null && Emulators[_emulatorIndex] != emulator)
                                Emulators[_emulatorIndex] = emulator;
                        }

                    }

                    emulators.Clear();
                    IsBusy = false;
                }
            }
            catch (Exception ex)
            {
                Log.Write($"Ошибка работы менеджера устройств: {ex.Message}");
            }
        }

        private async Task SelectEmulatorExecute(DataEmulator data)
        {
            if (!data.IsEnabled)
            {
                _ = Task.Run(async () => await ActionEmulatorExecute(0, data));
                return;
            }

            SelectedEmulator = data;
            OnPropertyChanged(nameof(SelectedEmulator));
        }

        private async Task ActionEmulatorExecute(int action, DataEmulator? data = null)
        {
            if (SelectedEmulator.Index == -1)
                return;

            IsBusy = true;
            var emulator = Emulators.ToList().FindIndex(emulator => emulator.Index == ((data is not null) ? data?.Index : SelectedEmulator.Index));

            Emulators[emulator] = new DataEmulator()
            {
                Index = Emulators[emulator].Index,
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Busy"),
                IsEnabled = true
            };

            OnPropertyChanged(nameof(Emulators));

            switch (action)
            {
                case 0:
                    await Memu.Start(Emulators[emulator].Index);
                    break;
                case 1:
                    await Memu.Stop(Emulators[emulator].Index);
                    break;
            }
            
            IsBusy = false;
        }

        private async Task AddEmulatorExecute(ImageButton button)
        {
            if (IsBusy || !File.Exists(Globals.Setup.PathToFileImage))
                return;

            IsBusy = true;
            button.BackgroundColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "Busy");
            Emulators ??= new ObservableCollection<DataEmulator>();

            Emulators.Add(new DataEmulator()
            {
                CurrentColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "NotActive"),
                Index = await Memu.Create(),
                IsEnabled = false
            });

            OnPropertyChanged(nameof(Emulators));
            await Memu.Import(Globals.Setup.PathToFileImage);
            button.BackgroundColor = (Color)ResourceHelper.FindResource(MainPage.GetInstance(), "NotActive");
            IsBusy = false;
        }
    }
}