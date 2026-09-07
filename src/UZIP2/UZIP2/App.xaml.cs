using System;
using System.Windows;

namespace UZIP2
{
	/// <summary>
	/// App.xaml 的交互逻辑
	/// </summary>
	public partial class App : Application
	{
		private void Application_Startup(object sender, StartupEventArgs e)
		{
			CliArgs cli = CliArgs.Parse(e.Args);
			if (cli.ShowHelp || cli.ParseError)
			{
				MessageBox.Show(
					cli.ParseError ? ("参数无效。\n\n" + CliArgs.HelpText) : CliArgs.HelpText,
					"UZip2",
					MessageBoxButton.OK,
					cli.ParseError ? MessageBoxImage.Warning : MessageBoxImage.Information);
				Shutdown(cli.ParseError ? 1 : 0);
				return;
			}

			MainWindow mw = new MainWindow(cli);
			mw.Show();
		}
	}
}
