using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace UZIP2
{
	/// <summary>
	/// 监听文件夹：新文件稳定后回调；忙时由外部排队消费。
	/// </summary>
	public class FolderWatch
	{
		FileSystemWatcher watcher;
		readonly object gate = new object();
		readonly Dictionary<string, DateTime> pending = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
		readonly Dictionary<string, int> retryCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		readonly List<string> readyQueue = new List<string>();
		Timer debounceTimer;
		Action<string> onFileReady;
		volatile bool stopping;
		const int DebounceMs = 800;
		const int MaxReadyRetries = 20; // ~10s extra after debounce

		public bool IsRunning
		{
			get { return watcher != null && watcher.EnableRaisingEvents; }
		}

		public void Start(string folder, Action<string> onReady)
		{
			Stop();
			if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder) || onReady == null)
				return;
			stopping = false;
			onFileReady = onReady;
			watcher = new FileSystemWatcher(folder);
			watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size;
			watcher.IncludeSubdirectories = false;
			watcher.Created += OnChanged;
			watcher.Changed += OnChanged;
			watcher.Renamed += OnRenamed;
			watcher.Error += OnWatcherError;
			watcher.EnableRaisingEvents = true;
			debounceTimer = new Timer(OnDebounce, null, Timeout.Infinite, Timeout.Infinite);
		}

		public void Stop()
		{
			stopping = true;
			onFileReady = null;

			FileSystemWatcher w = watcher;
			watcher = null;
			if (w != null)
			{
				try { w.EnableRaisingEvents = false; } catch { }
				try
				{
					w.Created -= OnChanged;
					w.Changed -= OnChanged;
					w.Renamed -= OnRenamed;
					w.Error -= OnWatcherError;
				}
				catch { }
				try { w.Dispose(); } catch { }
			}

			Timer t = debounceTimer;
			debounceTimer = null;
			if (t != null)
			{
				try { t.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
				try { t.Dispose(); } catch { }
			}

			lock (gate)
			{
				pending.Clear();
				retryCount.Clear();
				readyQueue.Clear();
			}
		}

		public string DequeueReady()
		{
			lock (gate)
			{
				while (readyQueue.Count > 0)
				{
					string p = readyQueue[0];
					readyQueue.RemoveAt(0);
					if (File.Exists(p)) return p;
				}
				return null;
			}
		}

		public int ReadyCount
		{
			get { lock (gate) { return readyQueue.Count; } }
		}

		void OnWatcherError(object sender, ErrorEventArgs e)
		{
			// ponytail: 缓冲区溢出后尽量重启当前 watcher
			if (stopping) return;
			FileSystemWatcher w = watcher;
			if (w == null) return;
			string path = null;
			try { path = w.Path; } catch { return; }
			Action<string> cb = onFileReady;
			if (string.IsNullOrEmpty(path) || cb == null) return;
			try
			{
				Start(path, cb);
			}
			catch { }
		}

		void OnRenamed(object sender, RenamedEventArgs e)
		{
			NotePath(e.FullPath);
		}

		void OnChanged(object sender, FileSystemEventArgs e)
		{
			NotePath(e.FullPath);
		}

		void NotePath(string path)
		{
			if (stopping || string.IsNullOrEmpty(path)) return;
			try
			{
				if (Directory.Exists(path)) return;
			}
			catch { return; }

			lock (gate)
			{
				pending[path] = DateTime.UtcNow;
			}
			SafeTimerChange(DebounceMs);
		}

		void SafeTimerChange(int dueMs)
		{
			Timer t = debounceTimer;
			if (stopping || t == null) return;
			try
			{
				t.Change(dueMs, Timeout.Infinite);
			}
			catch (ObjectDisposedException) { }
		}

		void OnDebounce(object state)
		{
			if (stopping) return;

			List<string> due = new List<string>();
			lock (gate)
			{
				DateTime now = DateTime.UtcNow;
				var keys = new List<string>(pending.Keys);
				foreach (string k in keys)
				{
					if ((now - pending[k]).TotalMilliseconds >= DebounceMs - 50)
					{
						due.Add(k);
						pending.Remove(k);
					}
				}
				if (pending.Count > 0)
					SafeTimerChange(DebounceMs);
			}

			foreach (string path in due)
			{
				if (stopping) return;
				if (!WaitFileReady(path, 10))
				{
					int n = 0;
					lock (gate)
					{
						retryCount.TryGetValue(path, out n);
						n++;
						if (n <= MaxReadyRetries && File.Exists(path))
						{
							retryCount[path] = n;
							pending[path] = DateTime.UtcNow;
							SafeTimerChange(DebounceMs);
						}
						else
						{
							retryCount.Remove(path);
						}
					}
					continue;
				}

				lock (gate)
				{
					retryCount.Remove(path);
					bool has = false;
					for (int i = 0; i < readyQueue.Count; i++)
					{
						if (string.Equals(readyQueue[i], path, StringComparison.OrdinalIgnoreCase))
						{ has = true; break; }
					}
					if (!has) readyQueue.Add(path);
				}
				var cb = onFileReady;
				if (!stopping && cb != null) cb(path);
			}
		}

		static bool WaitFileReady(string path, int tries)
		{
			for (int i = 0; i < tries; i++)
			{
				try
				{
					if (!File.Exists(path)) return false;
					using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
						return true;
				}
				catch
				{
					Thread.Sleep(500);
				}
			}
			return false;
		}
	}
}
