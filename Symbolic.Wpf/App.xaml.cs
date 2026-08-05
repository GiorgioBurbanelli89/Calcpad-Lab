using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Calcpad.Wpf
{
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
        }

        private void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException -= AppDomain_UnhandledException;
            ReportUnhandledExceptionAndClose((Exception)e.ExceptionObject);
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            DispatcherUnhandledException -= Application_DispatcherUnhandledException;
            e.Handled = true;
            ReportUnhandledExceptionAndClose(e.Exception);
        }

        private static void ReportUnhandledExceptionAndClose(Exception e)
        {
            // Headless (--shot/--gif/--wshot/--pdf): volcar el crash a %TEMP% y salir DURO.
            // Nada de SaveStateAndRestart/Execute (respawn) ni Process.Start(Notepad):
            // en headless el respawn re-lanza la app SIN --shot, TryRestoreState abre un
            // MessageBox que bloquea, y se acumulan huérfanos msedgewebview2. Environment.Exit
            // termina el proceso y sus hijos WebView2 de inmediato -> sin huérfanos ni cuelgue.
            if (Calcpad.Wpf.MainWindow.IsHeadless)
            {
                try
                {
                    var dump = Path.Combine(Path.GetTempPath(), "calcpad_lab_crash.txt");
                    File.WriteAllText(dump, $"{e.GetType().FullName}\n{e.Message}\n\n{e.StackTrace}\n\nInner: {e.InnerException}");
                }
                catch { }
                Environment.Exit(1);
                return;
            }

            MainWindow main = (MainWindow)Current.MainWindow;
            var logFileName = Path.ChangeExtension(Path.GetTempFileName(), ".txt");
            var message = GetMessage(e);
            if (main.IsSaved)
            {
                message += AppMessages.ReportUnhandledExceptionAndClose_NoUnsavedData;
            }
            else
            {
                message += AppMessages.ReportUnhandledExceptionAndClose_NoUnsavedData_RecoveryAttempted;
                try
                {
                    var tempFile = Path.ChangeExtension(Path.GetRandomFileName(), ".cpd");
                    main.SaveStateAndRestart(tempFile);
                    message += AppMessages.NYourDataWasSavedBothToClipboardAndTempFile + tempFile;
                }
                catch
                {
                    message += AppMessages.ReportUnhandledExceptionAndClose_UnsavedData_RecoveryFailed;
                }
            }
            message += string.Format(AppMessages.ExceptionDetails, e);
            File.WriteAllText(logFileName, message);
            Task.Run(async () =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = logFileName,
                    UseShellExecute = true
                });
            });
            Application.Current.Shutdown();
            // Environment.Exit(System.Runtime.InteropServices.Marshal.GetHRForException(e));
        }

        private static string GetMessage(Exception e) =>
            string.Format(AppMessages.UnexpectedErrorOccurred, e.Message, e.Source);
    }
}