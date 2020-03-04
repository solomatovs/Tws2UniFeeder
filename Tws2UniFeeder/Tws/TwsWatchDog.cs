using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WinApi.User32;
using WinApi.Gdi32;
using WinApi.Kernel32;
using Microsoft.Extensions.Hosting;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Tws2UniFeeder
{
    internal enum TwsWindowStatus
    {
        MainWindowIsNotDrawn,
        UnknownStatus,
        LoginInput,
        AuthentificateProcess,
        AuthentificateFailed,
        EnterSecurityCode,
        ExistingSessionDetected,
        ReloginIsRequired,
        StartingApplication,
        Success
    }

    public class TwsWatchDog : BackgroundService
    {
        private readonly string processName = "tws";
        private readonly IOptionsMonitor<TwsWatchDogOption> option;
        private readonly IBackground<string> state;
        private int twsErrorMessageCount = 0;
        private readonly ILogger logger;
        public TwsWatchDog(IOptionsMonitor<TwsWatchDogOption> option, IBackground<string> state, ILoggerFactory loggerFactory)
        {
            this.option = option;
            this.state = state;
            this.logger = loggerFactory.CreateLogger<TwsWatchDog>();
        }

        private string WorkingDirectory(TwsWatchDogOption option)
        {
            return Directory.GetDirectoryRoot(option.Path);
        }

        private IEnumerable<Process> SearchTwsProcess()
        {
            var result = new List<Process>();
            try
            {
                foreach (var p in Process.GetProcessesByName(processName).Where(p => p.MainModule.FileName == option.CurrentValue.Path))
                    result.Add(p);
            }
            catch (Exception e)
            {
                logger.LogDebug("Search process exception {0}: {1}", e.GetType().Name, e.Message);
            }

            return result;
        }

        private Process RunNewProcess()
        {
            var start = new ProcessStartInfo(processName)
            {
                WorkingDirectory = WorkingDirectory(option.CurrentValue),
                FileName = option.CurrentValue.Path,
                UseShellExecute = false,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal
            };

            logger.LogDebug("FileName {0}, UseShellExecute {1}, CreateNoWindow {2}, RedirectStandardOutput {3}, RedirectStandardInput {4}, Arguments {5}, WorkingDirectory {6}, UserName {7}", start.FileName, start.UseShellExecute, start.CreateNoWindow, start.RedirectStandardOutput, start.RedirectStandardInput, start.Arguments, start.WorkingDirectory, start.UserName);

            return Process.Start(start);
        }

        private bool RestartTwsProcess()
        {
            logger.LogInformation("Runing Restart TWS...");
            var processs = SearchTwsProcess();
            
            foreach(var process in processs)
            {
                logger.LogDebug("Found running TWS Process {0}", process.Id);
                try
                {
                    logger.LogInformation("Kill TWS Process {0}...", process.Id);
                    process.Kill(true);
                    logger.LogInformation("TWS Process killed");

                }
                catch (Exception e)
                {
                    logger.LogError("TWS Process {0} not killed and restart faild. error: {1}:{2}", process.Id, e.GetType().Name, e.Message);
                }
            }

            try
            {
                logger.LogDebug("Try start new TWS process...");
                var process = RunNewProcess();
                logger.LogInformation("TWS process running successfully");
            }
            catch (Exception e)
            {
                logger.LogError("TWS Process not started. error: {1}:{2}", e.GetType().Name, e.Message);
                return false;
            }

            logger.LogInformation("TWS Restarted");
            return true;
        }

        private (TwsWindowStatus, IntPtr) TwsWindowState(Process process, TwsWatchDogOption option)
        {
            process.Refresh();

            var MainWindow = process.MainWindowHandle;
            if (MainWindow == IntPtr.Zero)
                return (TwsWindowStatus.MainWindowIsNotDrawn, IntPtr.Zero);

            IntPtr LoginWindow;
            if (process.MainWindowTitle == option.LoginWindow.Header)
                LoginWindow = process.MainWindowHandle;
            else
                LoginWindow = User32Methods.FindWindow(option.LoginWindow.Class, option.LoginWindow.Header);

            IntPtr AuthenticatingWindow;
            if (process.MainWindowTitle == option.AuthenticatingWindow.Header)
                AuthenticatingWindow = process.MainWindowHandle;
            else
                AuthenticatingWindow = User32Methods.FindWindow(option.AuthenticatingWindow.Class, option.AuthenticatingWindow.Header);

            IntPtr StartingApplicationWindow;
            if (process.MainWindowTitle == option.StartingApplicationWindow.Header)
                StartingApplicationWindow = process.MainWindowHandle;
            else
                StartingApplicationWindow = User32Methods.FindWindow(option.StartingApplicationWindow.Class, option.StartingApplicationWindow.Header);

            var EnterSecurityCodeWindow = User32Methods.FindWindow(option.EnterSecurityCodeWindow.Class, option.EnterSecurityCodeWindow.Header);
            var LoginFailedWindow = User32Methods.FindWindow(option.LoginFailedWindow.Class, option.LoginFailedWindow.Header);
            var ExistingSessionDetectedWindow = User32Methods.FindWindow(option.ExistingSessionDetectedWindow.Class, option.ExistingSessionDetectedWindow.Header);
            var ReloginIsRequiredWindow = User32Methods.FindWindow(option.ReloginIsRequiredWindow.Class, option.ReloginIsRequiredWindow.Header);

            logger.LogDebug("MainWindow: {0}, LoginWindow: {1}, AuthenticatingWindow: {2}, StartingApplicationWindow: {3}, EnterSecurityCodeWindow: {4}, LoginFailedWindow: {5}, ExistingSessionDetectedWindow: {6}, ReloginIsRequiredWindow: {7}", MainWindow, LoginWindow, AuthenticatingWindow, StartingApplicationWindow, EnterSecurityCodeWindow, LoginFailedWindow, ExistingSessionDetectedWindow, ReloginIsRequiredWindow);

            // Если есть окно процесса аутентификации, то необходимо проверить наличие других окон
            if (LoginFailedWindow != IntPtr.Zero)
            {
                return (TwsWindowStatus.AuthentificateFailed, LoginFailedWindow);
            }

            // Ппоявилось окно ввода дополнительной авторизации
            if (EnterSecurityCodeWindow != IntPtr.Zero)
            {
                return (TwsWindowStatus.EnterSecurityCode, EnterSecurityCodeWindow);
            }

            // Появилось окно с сообщением о том, что уже залогинен другой пользователь
            if (ExistingSessionDetectedWindow != IntPtr.Zero)
            {
                return (TwsWindowStatus.EnterSecurityCode, ExistingSessionDetectedWindow);
            }

            // Вылезло окно с сообщение о том, что нужно произвести авторизацию заново
            if (ReloginIsRequiredWindow != IntPtr.Zero)
            {
                return (TwsWindowStatus.ReloginIsRequired, ReloginIsRequiredWindow);
            }

            // происходит процесс загрузки приложения после успешной авторизации
            if (StartingApplicationWindow != IntPtr.Zero)
            {
                return (TwsWindowStatus.StartingApplication, StartingApplicationWindow);
            }

            // Происходит процесс аутентификации после ввода логина с паролем
            if (AuthenticatingWindow != IntPtr.Zero)
            {
                return (TwsWindowStatus.AuthentificateProcess, AuthenticatingWindow);
            }

            // Если есть окно Login и нет окна процесса авторизации, значит терминал ждёт ввода логина с паролем
            if (LoginWindow != IntPtr.Zero)
            {
                // Если нет процесса авторизации, значит терминал запросил логин с паролем
                if (AuthenticatingWindow == IntPtr.Zero)
                {
                    return (TwsWindowStatus.LoginInput, LoginWindow);
                }
                else
                {
                    return (TwsWindowStatus.AuthentificateProcess, AuthenticatingWindow);
                }
            }
            else
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                    return (TwsWindowStatus.Success, process.MainWindowHandle);
            }

            // Статус терминала неизвестен
            return (TwsWindowStatus.UnknownStatus, IntPtr.Zero);
        }

        private void AutoProcessWindowTws(TwsWatchDogOption option)
        {
            var processs = SearchTwsProcess();
            if (processs.Count() > 1)
            {
                RestartTwsProcess();
                return;
            }

            if (processs.Count() == 0)
            {
                logger.LogDebug("TWS Process not found");
                try
                {
                    logger.LogDebug("Try start new TWS process...");
                    RunNewProcess();
                    logger.LogInformation("TWS process running successfully");
                    return;
                }
                catch (Exception e)
                {
                    logger.LogError("TWS Process not started. error: {1}:{2}", e.GetType().Name, e.Message);
                    return;
                }
            }

            foreach (var process in processs)
            {
                var state = TwsWindowState(process, option);
                switch (state.Item1)
                {
                    case TwsWindowStatus.EnterSecurityCode:
                        InputHelper.SendEnterSecurityCode(state.Item2);
                        break;
                    case TwsWindowStatus.AuthentificateFailed:
                        logger.LogError("Automatic authorization error. Please sign in manually");
                        RestartTwsProcess();
                        break;
                    case TwsWindowStatus.AuthentificateProcess:
                        logger.LogInformation("Authentificating waitng...");
                        break;
                    case TwsWindowStatus.StartingApplication:
                        logger.LogInformation("Starting application waiting...");
                        break;
                    case TwsWindowStatus.LoginInput:
                        WindowInfo pwi = new WindowInfo();
                        User32Methods.GetWindowInfo(state.Item2, ref pwi);
                        logger.LogDebug("type: {1}, status: {2}, rect: {3}, style: {4}, exstyle: {5}", pwi.WindowType, pwi.WindowStatus, pwi.WindowRect, pwi.Styles, pwi.ExStyles);

                        InputHelper.SendLoginAndPassword(state.Item2, option.Login, option.Password, logger);
                        break;
                    case TwsWindowStatus.ReloginIsRequired:
                        // RestartTwsProcess();
                        InputHelper.SendExitApplicationFromReloginForm();
                        // return;
                        break;
                    case TwsWindowStatus.ExistingSessionDetected:
                        InputHelper.SendExitApplicationExistingSessionDetectedForm();
                        // RestartTwsProcess();
                        // return;
                        break;
                    case TwsWindowStatus.Success:
                        //logger.LogDebug("Tws Window Success");
                        break;
                    case TwsWindowStatus.MainWindowIsNotDrawn:
                        logger.LogInformation("Waiting main window...");
                        break;
                    default:
                        logger.LogError("Unwnown Tws Window status. Failed authentification. Please log in manually");
                        break;
                }
            }

            Thread.Sleep(TimeSpan.FromSeconds(2));
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            logger.LogInformation("Tws Process starting...");
            new Thread(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (option.CurrentValue.Enable)
                    {
                        AutoProcessWindowTws(option.CurrentValue);
                    }
                }
            })
            { IsBackground = true }.Start();
            new Thread(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (option.CurrentValue.Enable)
                    {
                        try
                        {
                            var message_error = await state.DequeueAsync(token);
                            twsErrorMessageCount++;
                            logger.LogInformation("tws error count {0}, message:  {1}", twsErrorMessageCount, message_error);

                            if (twsErrorMessageCount >= option.CurrentValue.CriticalErrorMessageBeforeRestart)
                            {
                                if (RestartTwsProcess())
                                {
                                    twsErrorMessageCount = 0;
                                }
                            }
                        }
                        catch
                        { }
                    }
                }
            })
            { IsBackground = true }.Start();
            await Task.CompletedTask;
            logger.LogInformation("Tws Process started");
        }
    }

    public static class InputHelper
    {
        public static uint Send(this IEnumerable<Input> inputs, TimeSpan AfterSleep)
        {
            var x = User32Helpers.SendInput(inputs.ToArray());
            Thread.Sleep(AfterSleep);
            return x;
        }

        public static IEnumerable<Input> AddVirtualKey(this IEnumerable<Input> inputs, VirtualKey key)
        {
            Input.InitKeyboardInput(out Input down, key, false);
            Input.InitKeyboardInput(out Input up, key, true);

            return inputs.Append(down).Append(up);
        }

        public static IEnumerable<Input> AddSpace(this IEnumerable<Input> inputs)
        {
            return inputs.AddVirtualKey(VirtualKey.SPACE);
        }

        public static IEnumerable<Input> AddTab(this IEnumerable<Input> inputs)
        {
            return inputs.AddVirtualKey(VirtualKey.TAB);
        }

        public static IEnumerable<Input> AddCtrlPlusA(this IEnumerable<Input> inputs)
        {
            Input.InitKeyboardInput(out Input down, VirtualKey.CONTROL, false);
            Input.InitKeyboardInput(out Input up, VirtualKey.CONTROL, true);
            Input.InitKeyboardInput(out Input a_down, VirtualKey.A, false);
            Input.InitKeyboardInput(out Input a_up, VirtualKey.A, true);
            return inputs.Append(down).Append(a_down).Append(up).Append(a_up);
        }
        public static IEnumerable<Input> AddMouseMove(this IEnumerable<Input> inputs, int x, int y)
        {
            Input.InitMouseInput(out Input move, x, y, MouseInputFlags.MOUSEEVENTF_ABSOLUTE | MouseInputFlags.MOUSEEVENTF_MOVE);
            return inputs.Append(move);
        }

        public static IEnumerable<Input> AddMouseClick(this IEnumerable<Input> inputs, int x, int y)
        {
            Input.InitMouseInput(out Input down, x, y, MouseInputFlags.MOUSEEVENTF_LEFTDOWN);
            Input.InitMouseInput(out Input up, x, y, MouseInputFlags.MOUSEEVENTF_LEFTUP);
            return inputs.Append(down).Append(up);
        }

        public static IEnumerable<Input> AddDelete(this IEnumerable<Input> inputs)
        {
            return inputs.AddVirtualKey(VirtualKey.DELETE);
        }

        public static IEnumerable<Input> AddText(this IEnumerable<Input> inputs, string text)
        {
            foreach (var c in text.ToCharArray())
            {
                Input.InitKeyboardInput(out Input down, c, false);
                Input.InitKeyboardInput(out Input up, c, true);
                inputs = inputs.Append(down).Append(up);
            }

            return inputs;
        }
        public static IEnumerable<Input> GenerateInput()
        {
            return new List<Input>();
        }

        // Отправка сообщений с кодом авторизации
        public static void SendEnterSecurityCode(IntPtr window)
        {
            WindowInfo pwi = new WindowInfo();
            User32Methods.GetWindowInfo(window, ref pwi);

            int x = pwi.WindowRect.Left + (pwi.WindowRect.Width * 50 / 100);
            int y = pwi.WindowRect.Top + (pwi.WindowRect.Height * 70 / 100);

            User32Methods.SetForegroundWindow(window);
            User32Methods.ShowWindow(window, ShowWindowCommands.SW_RESTORE);
            User32Methods.SetCursorPos(x, y);

            GenerateInput()
            .AddMouseClick(0, 0)
            .Send(TimeSpan.FromSeconds(1));
        }

        // Ввод данных логина с паролем от TWS счета
        public static void SendLoginAndPassword(IntPtr window, string login, string password, ILogger logger)
        {
            WindowInfo pwi = new WindowInfo();
            User32Methods.GetWindowInfo(window, ref pwi);

            int x = pwi.WindowRect.Left + (pwi.WindowRect.Width * 50 / 100);
            int y = pwi.WindowRect.Top + (pwi.WindowRect.Height * 47 / 100);

            User32Methods.ShowCursor(true);

            if(!User32Methods.SetForegroundWindow(window))
            {
                var err = Kernel32Methods.GetLastError();
                logger.LogDebug("SetForegroundWindow error {0}", err);
            }
            ;
            if (!User32Methods.ShowWindow(window, ShowWindowCommands.SW_RESTORE))
            {
                var err = Kernel32Methods.GetLastError();
                logger.LogDebug("ShowWindow error {0}", err);
            }
            if (!User32Methods.SetCursorPos(x, y))
            {
                var err = Kernel32Methods.GetLastError();
                logger.LogDebug("SetCursorPos error {0}", err);
            }

            GenerateInput()
            .AddMouseClick(0, 0)
            .AddMouseClick(0, 0)
            .Send(TimeSpan.FromSeconds(1));

            GenerateInput()
            .AddText(login)
            .Send(TimeSpan.FromSeconds(1));

            x = pwi.WindowRect.Left + (pwi.WindowRect.Width * 50 / 100);
            y = pwi.WindowRect.Top + (pwi.WindowRect.Height * 54 / 100);

            User32Methods.SetForegroundWindow(window);
            User32Methods.ShowWindow(window, ShowWindowCommands.SW_RESTORE);
            User32Methods.SetCursorPos(x, y);

            GenerateInput()
            .AddMouseClick(0, 0)
            .AddMouseClick(0, 0)
            .Send(TimeSpan.FromSeconds(1));

            GenerateInput()
            .AddText(password)
            .Send(TimeSpan.FromSeconds(1));

            x = pwi.WindowRect.Left + (pwi.WindowRect.Width * 50 / 100);
            y = pwi.WindowRect.Top + (pwi.WindowRect.Height * 73 / 100);

            User32Methods.SetForegroundWindow(window);
            User32Methods.ShowWindow(window, ShowWindowCommands.SW_RESTORE);
            User32Methods.SetCursorPos(x, y);

            GenerateInput()
            .AddMouseClick(0, 0)
            .Send(TimeSpan.FromSeconds(0));
        }

        public static void SendExitApplicationFromReloginForm()
        {
            GenerateInput()
            .AddTab()
            .Send(TimeSpan.FromSeconds(1));

            GenerateInput()
            .AddSpace()
            .Send(TimeSpan.FromSeconds(1));
        }

        public static void SendExitApplicationExistingSessionDetectedForm()
        {
            GenerateInput()
            .AddTab()
            .Send(TimeSpan.FromSeconds(1));

            GenerateInput()
            .AddSpace()
            .Send(TimeSpan.FromSeconds(1));
        }
    }
}
