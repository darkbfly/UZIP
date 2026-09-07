using System;
using System.Collections.Generic;
using System.Text;

namespace UZIP2
{
	/// <summary>
	/// 命令行参数：extract / compress / auto / watch，以及 -o / -p / -q / -h
	/// </summary>
	public class CliArgs
	{
		public enum CliMode
		{
			None = 0,
			Auto = 1,
			Extract = 2,
			Compress = 3,
			Watch = 4
		}

		public bool IsCliJob;
		public bool IsCliWatch;
		public bool ShowHelp;
		public bool Quiet;
		public bool ParseError;
		public CliMode Mode = CliMode.None;
		public string OutPath;
		public string Password;
		public List<string> Paths = new List<string>();

		public static string HelpText
		{
			get
			{
				var sb = new StringBuilder();
				sb.AppendLine("UZip2 命令行用法");
				sb.AppendLine();
				sb.AppendLine("  UZIP2.exe extract <档案...> [-o 目录] [-p 密码] [-q]");
				sb.AppendLine("  UZIP2.exe compress <文件/目录...> [-o 目录] [-p 密码] [-q]");
				sb.AppendLine("  UZIP2.exe auto <路径...> [-o 目录] [-p 密码] [-q]");
				sb.AppendLine("  UZIP2.exe watch <文件夹> [-o 目录] [-p 密码] [-q]");
				sb.AppendLine("  UZIP2.exe <路径...>          等价于 auto");
				sb.AppendLine("  UZIP2.exe -h | --help");
				sb.AppendLine();
				sb.AppendLine("选项:");
				sb.AppendLine("  -o <路径>   输出目录");
				sb.AppendLine("  -p <密码>   本次额外尝试的密码");
				sb.AppendLine("  -q          静默：隐藏主窗（watch 常驻托盘；单次任务结束后退出）");
				sb.AppendLine();
				sb.AppendLine("说明: watch 仅自动解压监听到的压缩包");
				return sb.ToString();
			}
		}

		public static CliArgs Parse(string[] args)
		{
			var r = new CliArgs();
			if (args == null || args.Length == 0)
				return r;

			for (int i = 0; i < args.Length; i++)
			{
				string a = args[i];
				if (a == null || a.Length == 0) continue;

				string al = a.ToLowerInvariant();
				if (al == "-h" || al == "--help" || al == "/?" || al == "help")
				{
					r.ShowHelp = true;
					return r;
				}
				if (al == "-q" || al == "--quiet")
				{
					r.Quiet = true;
					continue;
				}
				if (al == "-o" || al == "--out")
				{
					if (i + 1 >= args.Length) { r.ParseError = true; return r; }
					r.OutPath = args[++i];
					continue;
				}
				if (al == "-p" || al == "--password")
				{
					if (i + 1 >= args.Length) { r.ParseError = true; return r; }
					r.Password = args[++i];
					continue;
				}
				if (al == "extract" || al == "e")
				{
					if (r.Mode != CliMode.None) { r.ParseError = true; return r; }
					r.Mode = CliMode.Extract;
					continue;
				}
				if (al == "compress" || al == "c")
				{
					if (r.Mode != CliMode.None) { r.ParseError = true; return r; }
					r.Mode = CliMode.Compress;
					continue;
				}
				if (al == "auto" || al == "a")
				{
					if (r.Mode != CliMode.None) { r.ParseError = true; return r; }
					r.Mode = CliMode.Auto;
					continue;
				}
				if (al == "watch" || al == "w")
				{
					if (r.Mode != CliMode.None) { r.ParseError = true; return r; }
					r.Mode = CliMode.Watch;
					continue;
				}
				if (a.StartsWith("-"))
				{
					r.ParseError = true;
					return r;
				}
				r.Paths.Add(a);
			}

			if (r.Mode == CliMode.Watch)
			{
				if (r.Paths.Count != 1) { r.ParseError = true; return r; }
				r.IsCliWatch = true;
				return r;
			}

			if (r.Paths.Count == 0)
			{
				if (r.Mode != CliMode.None || r.Quiet || r.OutPath != null || r.Password != null)
					r.ParseError = true;
				return r;
			}

			if (r.Mode == CliMode.None)
				r.Mode = CliMode.Auto;
			r.IsCliJob = true;
			return r;
		}
	}
}
