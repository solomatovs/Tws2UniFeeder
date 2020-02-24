using System;
using System.Threading;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WinApi.Windows;
using WinApi;
using WinApi.Core;
using WinApi.Kernel32;
using WinApi.KernelBase;
using WinApi.User32;
using NetCoreEx.Geometry;

namespace Tws2UniFeeder
{
    public class TwsProcess : ITwsProcess
    {
        private string processName = "tws";
        private IOptions<TwsOption> option;
        private ILogger logger;
        public TwsProcess(IOptions<TwsOption> option, ILoggerFactory loggerFactory)
        {
            this.option = option;
            this.logger = loggerFactory.CreateLogger<TwsProcess>();
        }

        private string WorkingDirectory(TwsOption option)
        {
            return Directory.GetDirectoryRoot(option.Path);
        }

        private Process SearchTwsProcess()
        {
            foreach (var p in Process.GetProcessesByName(processName).Where(p => p.MainModule.FileName == option.Value.Path))
            {
                return p;
            }

            return null;
        }

        private Process RunNewProcess()
        {
            var start = new ProcessStartInfo(processName)
            {
                WorkingDirectory = WorkingDirectory(option.Value),
                FileName = option.Value.Path
            };

            return Process.Start(start);
        }

        public bool TwsProcessIsRunning()
        {
            return SearchTwsProcess() != null;
        }

        public bool RestartTwsProcess()
        {
            logger.LogInformation("Runing Restart TWS...");
            var process = SearchTwsProcess();
            
            if (process == null)
                logger.LogDebug("TWS Process not found");
            else
            {
                logger.LogDebug("Found running TWS Process {0}", process.Id);
                logger.LogInformation("Kill TWS Process {0}...", process.Id);
                try
                {
                    process.Kill(true);
                }
                catch(Exception e)
                {
                    logger.LogError("TWS Process {0} not killed and restart faild. error: {1}:{2}", process.Id, e.GetType().Name, e.Message);
                    return false;
                }
                logger.LogInformation("TWS process completed successfully");
            }

            logger.LogDebug("Try start new TWS process...");
            try
            {
                process = RunNewProcess();
            }
            catch(Exception e)
            {
                logger.LogError("TWS Process not started and restart faild. error: {1}:{2}", process.Id, e.GetType().Name, e.Message);
                return false;
            }


            logger.LogInformation("Try input login and password. Tws process: {0}", process.Id);
            if(FindTwsWindowAndInputLoginPassword(process))
            {

            }
            else
            {
                logger.LogError("Failed to complete enter your username and password correctly. Restart failed");
                return false;
            }

            logger.LogInformation("TWS Restarted");
            return true;
        }

        private bool FindTwsWindowAndInputLoginPassword(Process process)
        {
            logger.LogInformation("Search Login and Password form in TWS process {0}...", process.Id);

            IntPtr SunAwtFrame;
            while ((SunAwtFrame = User32Methods.FindWindowEx(process.MainWindowHandle, IntPtr.Zero, "SunAwtFrame", null)) == IntPtr.Zero)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                process.Refresh();
            }

            while(true)
            {
                WindowInfo info = new WindowInfo();
                User32Methods.GetWindowInfo(SunAwtFrame, ref info);
                User32Methods.GetWindowRect(SunAwtFrame, out NetCoreEx.Geometry.Rectangle lpRect);

                logger.LogInformation("left: {0}, right: {1}, type: {2}, status: {3}, size: {4}, clientRect: {5}", info.WindowRect.Left, info.WindowRect.Right, info.WindowType, info.WindowStatus, info.Size, info.ClientRect);
                logger.LogInformation("lp rect: {0}", lpRect);

                if (info.WindowStatus == 0)
                {
                    User32Methods.SetActiveWindow(SunAwtFrame);
                    User32Methods.GetWindowInfo(SunAwtFrame, ref info);
                    logger.LogInformation("----- left: {0}, right: {1}, type: {2}, status: {3}, size: {4}, clientRect: {5}", info.WindowRect.Left, info.WindowRect.Right, info.WindowType, info.WindowStatus, info.Size, info.ClientRect);
                }

                // User32Methods.SetForegroundWindow
                Input[] inputs = GenerateLoginInput(SunAwtFrame, this.option.Value);
                var x = User32Helpers.SendInput(inputs);

                logger.LogInformation("send input result {0}", x);

                Thread.Sleep(TimeSpan.FromSeconds(10));
            }



            return true;
        }

        private Input[] GenerateLoginInput(IntPtr SunAwtFrame, TwsOption option)
        {
            var inputs = new List<Input>();

            WindowInfo info = new WindowInfo();
            User32Methods.GetWindowInfo(SunAwtFrame, ref info);

            // делаю активным поле для ввода логина
            int x = info.WindowRect.Left + (info.WindowRect.Width / 2);
            int y = (int)(info.WindowRect.Top + (info.WindowRect.Height * 47.35 / 100));
            Input.InitMouseInput(out Input login_click_move, x, y, MouseInputFlags.MOUSEEVENTF_ABSOLUTE | MouseInputFlags.MOUSEEVENTF_MOVE);
            Input.InitMouseInput(out Input login_click_down, x, y, MouseInputFlags.MOUSEEVENTF_LEFTDOWN);
            Input.InitMouseInput(out Input login_click_up, x, y, MouseInputFlags.MOUSEEVENTF_LEFTUP);

            inputs.Add(login_click_move);
            inputs.Add(login_click_down);
            inputs.Add(login_click_up);

            // ввод логина
            foreach (var c in option.Login.ToCharArray())
            {
                Input.InitKeyboardInput(out Input login_down, c, false);
                Input.InitKeyboardInput(out Input login_up, c, true);
                inputs.Add(login_down);
                inputs.Add(login_up);
            }

            return inputs.ToArray();
        }

        private Input[] GeneratePasswordInput(IntPtr SunAwtFrame, TwsOption option)
        {
            var inputs = new List<Input>();

            WindowInfo info = new WindowInfo();
            User32Methods.GetWindowInfo(SunAwtFrame, ref info);

            // делаю активным поле для ввода логина
            int x = info.WindowRect.Left + (info.WindowRect.Width / 2);
            int y = (int)(info.WindowRect.Top + (info.WindowRect.Height * 56.93 / 100));
            Input.InitMouseInput(out Input password_click_down, x, y, MouseInputFlags.MOUSEEVENTF_LEFTDOWN);
            Input.InitMouseInput(out Input password_click_up, x, y, MouseInputFlags.MOUSEEVENTF_LEFTUP);

            inputs.Add(password_click_down);
            inputs.Add(password_click_up);

            // ввод пароля
            //foreach (var c in option.Password.ToCharArray())
            //{
            //    Input.InitKeyboardInput(out Input password_down, c, false);
            //    Input.InitKeyboardInput(out Input password_up, c, true);
            //    inputs.Add(password_down);
            //    inputs.Add(password_up);
            //}

            return inputs.ToArray();
        }
    }
}
