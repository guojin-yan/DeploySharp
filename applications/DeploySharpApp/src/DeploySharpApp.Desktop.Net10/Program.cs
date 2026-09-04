using System;
using System.Windows;

namespace DeploySharpApp.Desktop.Net10
{
    public sealed class App : System.Windows.Application
    {
        [STAThread]
        public static void Main() { var app = new App(); app.Run(new MainWindow()); }
    }
}
