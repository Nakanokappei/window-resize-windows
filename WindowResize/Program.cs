using System;
using System.Threading;
using System.Windows.Forms;

namespace WindowResize;

static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        const string mutexName = "Global\\WindowResize_SingleInstance_F7A3B2";

        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            // Already running
            MessageBox.Show(
                Strings.AlreadyRunningBody,
                "Window Resize",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApplicationContext());
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
