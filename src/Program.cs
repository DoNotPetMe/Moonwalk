using System;
using System.IO;
using System.Windows.Forms;

namespace MoonwalkPro;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string dir = AppContext.BaseDirectory;
        string cfgPath = Path.Combine(dir, "config.ini");
        Config.EnsureDefault(cfgPath);
        var config = Config.Load(cfgPath);

        Application.Run(new TrayContext(config));
    }
}
