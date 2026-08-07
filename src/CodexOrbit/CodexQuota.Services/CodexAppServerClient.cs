using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace CodexQuota.Services;

internal sealed class CodexLiveResult
{
	public IDictionary<string, object> AccountResponse { get; set; }

	public IDictionary<string, object> RateLimitsResponse { get; set; }

	public string RuntimeName { get; set; }
}

internal sealed class CodexRateLimitsChangedEventArgs : EventArgs
{
	public IDictionary<string, object> RateLimitsResponse { get; set; }
}

/// <summary>
/// Talks to an installed Codex app-server. The app-server owns authentication,
/// token refresh, and the quota request, so Codex Orbit never reads credentials.
/// Multiple Windows and WSL runtimes are tried independently of whether the
/// first-party CLI or desktop UI is currently running.
/// </summary>
internal sealed class CodexAppServerClient : IDisposable
{
	private sealed class PendingRequest : IDisposable
	{
		public readonly ManualResetEventSlim Completed = new ManualResetEventSlim(false);

		public IDictionary<string, object> Response;

		public string Failure;

		public void Dispose()
		{
			Completed.Dispose();
		}
	}

	private sealed class RuntimeCandidate
	{
		public string Key;

		public string DisplayName;

		public string FileName;

		public string Arguments;

		public string CopyFrom;

		public string CleanupRoot;

		public int StartupTimeoutMilliseconds = 4000;

		public Encoding OutputEncoding;

		public Encoding ErrorEncoding;

		public bool Prepare()
		{
			if (string.IsNullOrWhiteSpace(CopyFrom))
			{
				return true;
			}
			try
			{
				FileInfo source = new FileInfo(CopyFrom);
				if (!source.Exists || source.Length <= 0)
				{
					return false;
				}
				FileInfo destination = new FileInfo(FileName);
				if (destination.Exists && FilesMatch(source, destination))
				{
					CleanupOldCopies(destination);
					return true;
				}
				Directory.CreateDirectory(destination.DirectoryName);
				string temporaryPath = destination.FullName + ".tmp-" + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
				try
				{
					File.Copy(source.FullName, temporaryPath, overwrite: true);
					if (File.Exists(destination.FullName))
					{
						File.Delete(destination.FullName);
					}
					File.Move(temporaryPath, destination.FullName);
				}
				finally
				{
					try
					{
						if (File.Exists(temporaryPath))
						{
							File.Delete(temporaryPath);
						}
					}
					catch
					{
					}
				}
				FileInfo prepared = new FileInfo(destination.FullName);
				bool valid = prepared.Exists && FilesMatch(source, prepared);
				if (valid)
				{
					CleanupOldCopies(prepared);
				}
				return valid;
			}
			catch
			{
				return false;
			}
		}

		private static bool FilesMatch(FileInfo source, FileInfo destination)
		{
			if (!source.Exists || !destination.Exists || source.Length != destination.Length)
			{
				return false;
			}
			using SHA256 sha256 = SHA256.Create();
			byte[] sourceHash;
			byte[] destinationHash;
			using (FileStream sourceStream = source.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				sourceHash = sha256.ComputeHash(sourceStream);
			}
			using (FileStream destinationStream = destination.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				destinationHash = sha256.ComputeHash(destinationStream);
			}
			return sourceHash.SequenceEqual(destinationHash);
		}

		private void CleanupOldCopies(FileInfo destination)
		{
			if (string.IsNullOrWhiteSpace(CleanupRoot) || destination?.Directory == null)
			{
				return;
			}
			try
			{
				string root = Path.GetFullPath(CleanupRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string destinationDirectory = Path.GetFullPath(destination.Directory.FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string prefix = root + Path.DirectorySeparatorChar;
				if (!destinationDirectory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
				foreach (string directory in Directory.EnumerateDirectories(root, "OpenAI.Codex_*", SearchOption.TopDirectoryOnly))
				{
					string fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
					if (string.Equals(fullPath, destinationDirectory, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					try
					{
						Directory.Delete(fullPath, recursive: true);
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}
	}

	private readonly object _stateLock = new object();

	private readonly object _writeLock = new object();

	private readonly object _pendingLock = new object();

	private readonly object _accountCacheLock = new object();

	private readonly Dictionary<long, PendingRequest> _pending = new Dictionary<long, PendingRequest>();

	private Process _process;

	private StreamWriter _input;

	private Thread _readerThread;

	private Thread _errorThread;

	private RuntimeCandidate _activeRuntime;

	private long _nextRequestId;

	private bool _initialized;

	private volatile bool _disposed;

	private string _lastProtocolLine;

	private string _lastErrorLine;

	private IDictionary<string, object> _cachedAccountResponse;

	private string _cachedAccountRuntimeKey;

	public string LastFailure { get; private set; }

	public event EventHandler<CodexRateLimitsChangedEventArgs> RateLimitsChanged;

	public CodexLiveResult ReadLive(int timeoutMilliseconds)
	{
		if (_disposed || timeoutMilliseconds <= 0)
		{
			return null;
		}
		DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1000, timeoutMilliseconds));
		HashSet<string> rejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		while (!_disposed && DateTime.UtcNow < deadline)
		{
			int remaining = Math.Max(250, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
			if (!EnsureStarted(remaining, rejected))
			{
				return null;
			}
			RuntimeCandidate runtime = _activeRuntime;
			IDictionary<string, object> account = GetCachedAccount(runtime?.Key);
			if (account == null)
			{
				remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
				if (remaining <= 0)
				{
					break;
				}
				int accountTimeout = Math.Max(250, Math.Min(remaining, 4000));
				account = SendRequestCore("account/read", new Dictionary<string, object>
				{
					{ "refreshToken", false }
				}, accountTimeout);
				if (HasResult(account))
				{
					SetCachedAccount(runtime?.Key, account);
				}
			}
			remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
			if (remaining <= 0)
			{
				break;
			}
			int rateTimeout = Math.Max(250, Math.Min(remaining, 4000));
			IDictionary<string, object> rateLimits = SendRequestCore("account/rateLimits/read", null, rateTimeout);
			if (HasResult(rateLimits))
			{
				LastFailure = null;
				return new CodexLiveResult
				{
					AccountResponse = account,
					RateLimitsResponse = rateLimits,
					RuntimeName = runtime?.DisplayName ?? "Codex"
				};
			}
			LastFailure = DescribeFailure(account, rateLimits);
			if (runtime != null)
			{
				rejected.Add(runtime.Key);
			}
			lock (_stateLock)
			{
				StopProcessNoLock();
			}
		}
		if (string.IsNullOrWhiteSpace(LastFailure))
		{
			LastFailure = "Codex 实时服务响应超时";
		}
		return null;
	}

	private bool EnsureStarted(int timeoutMilliseconds, ISet<string> rejected)
	{
		lock (_stateLock)
		{
			if (_disposed)
			{
				return false;
			}
			if (_initialized && _process != null && !_process.HasExited && _activeRuntime != null && !rejected.Contains(_activeRuntime.Key))
			{
				return true;
			}
			StopProcessNoLock();
			IList<RuntimeCandidate> candidates = FindCodexRuntimes();
			if (candidates.Count == 0)
			{
				LastFailure = "未找到可用的 Codex CLI、App 或 WSL 运行时";
				return false;
			}
			DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1000, timeoutMilliseconds));
			foreach (RuntimeCandidate candidate in candidates)
			{
				if (rejected.Contains(candidate.Key) || DateTime.UtcNow >= deadline)
				{
					continue;
				}
				int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
				if (remaining <= 0)
				{
					break;
				}
				int startupBudget = Math.Max(500, Math.Min(candidate.StartupTimeoutMilliseconds, remaining));
				if (!StartAndInitializeNoLock(candidate, startupBudget))
				{
					rejected.Add(candidate.Key);
					continue;
				}
				return true;
			}
			if (string.IsNullOrWhiteSpace(LastFailure))
			{
				LastFailure = "发现了 Codex，但无法启动实时账户服务";
			}
			return false;
		}
	}

	private bool StartAndInitializeNoLock(RuntimeCandidate runtime, int timeoutMilliseconds)
	{
		_lastProtocolLine = null;
		_lastErrorLine = null;
		if (!runtime.Prepare())
		{
			LastFailure = runtime.DisplayName + " 运行时准备失败";
			return false;
		}
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = runtime.FileName,
				Arguments = runtime.Arguments,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = runtime.OutputEncoding ?? Encoding.UTF8,
				StandardErrorEncoding = runtime.ErrorEncoding ?? runtime.OutputEncoding ?? Encoding.UTF8
			};
			Process process = new Process
			{
				StartInfo = startInfo,
				EnableRaisingEvents = true
			};
			if (!StartWithBomlessUtf8Input(process))
			{
				process.Dispose();
				return false;
			}
			_process = process;
			_activeRuntime = runtime;
			_input = process.StandardInput;
			_input.AutoFlush = true;
			_readerThread = new Thread((ThreadStart)delegate
			{
				ReadOutputLoop(process);
			})
			{
				IsBackground = true,
				Name = "CodexOrbit.AppServer.Output"
			};
			_errorThread = new Thread((ThreadStart)delegate
			{
				DrainErrorLoop(process);
			})
			{
				IsBackground = true,
				Name = "CodexOrbit.AppServer.Error"
			};
			_readerThread.Start();
			_errorThread.Start();

			Dictionary<string, object> parameters = new Dictionary<string, object>
			{
				{
					"clientInfo",
					new Dictionary<string, object>
					{
						{ "name", "codex-orbit" },
						{ "title", "Codex Orbit" },
						{ "version", typeof(CodexAppServerClient).Assembly.GetName().Version.ToString(3) }
					}
				},
				{
					"capabilities",
					new Dictionary<string, object>
					{
						{ "experimentalApi", true },
						{ "requestAttestation", false },
						{ "optOutNotificationMethods", new string[0] }
					}
				}
			};
			IDictionary<string, object> response = SendRequestCore("initialize", parameters, timeoutMilliseconds);
			if (!HasResult(response))
			{
				string detail = !string.IsNullOrWhiteSpace(_lastErrorLine) ? _lastErrorLine : _lastProtocolLine;
				LastFailure = runtime.DisplayName + " 与实时协议不兼容或启动超时" + (string.IsNullOrWhiteSpace(detail) ? "" : "：" + Shorten(detail));
				StopProcessNoLock();
				return false;
			}
			WriteMessage(new Dictionary<string, object>
			{
				{ "method", "initialized" }
			});
			_initialized = true;
			return true;
		}
		catch
		{
			LastFailure = runtime.DisplayName + " 无法启动";
			StopProcessNoLock();
			return false;
		}
	}

	private static bool StartWithBomlessUtf8Input(Process process)
	{
		Encoding previous = null;
		bool changed = false;
		try
		{
			try
			{
				previous = Console.InputEncoding;
				Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
				changed = true;
			}
			catch (IOException)
			{
			}
			return process.Start();
		}
		finally
		{
			if (changed)
			{
				try
				{
					Console.InputEncoding = previous;
				}
				catch (IOException)
				{
				}
			}
		}
	}

	private IDictionary<string, object> SendRequestCore(string method, object parameters, int timeoutMilliseconds)
	{
		if (_disposed)
		{
			return null;
		}
		long id = Interlocked.Increment(ref _nextRequestId);
		using PendingRequest pendingRequest = new PendingRequest();
		lock (_pendingLock)
		{
			_pending[id] = pendingRequest;
		}
		try
		{
			Dictionary<string, object> message = new Dictionary<string, object>
			{
				{ "id", id },
				{ "method", method }
			};
			if (parameters != null)
			{
				message["params"] = parameters;
			}
			WriteMessage(message);
			if (!pendingRequest.Completed.Wait(Math.Max(250, timeoutMilliseconds)) || !string.IsNullOrEmpty(pendingRequest.Failure))
			{
				return null;
			}
			return pendingRequest.Response;
		}
		catch
		{
			return null;
		}
		finally
		{
			lock (_pendingLock)
			{
				_pending.Remove(id);
			}
		}
	}

	private void WriteMessage(IDictionary<string, object> message)
	{
		string value = new JavaScriptSerializer
		{
			MaxJsonLength = int.MaxValue,
			RecursionLimit = 64
		}.Serialize(message);
		lock (_writeLock)
		{
			if (_disposed || _input == null)
			{
				throw new IOException("Codex app-server is not connected.");
			}
			_input.WriteLine(value);
			_input.Flush();
		}
	}

	private void ReadOutputLoop(Process process)
	{
		try
		{
			string line;
			while (!_disposed && !process.HasExited && (line = process.StandardOutput.ReadLine()) != null)
			{
				if (string.IsNullOrWhiteSpace(line))
				{
					continue;
				}
				_lastProtocolLine = line;
				IDictionary<string, object> message;
				try
				{
					message = new JavaScriptSerializer
					{
						MaxJsonLength = int.MaxValue,
						RecursionLimit = 128
					}.DeserializeObject(line) as IDictionary<string, object>;
				}
				catch
				{
					continue;
				}
				if (message == null)
				{
					continue;
				}
				if (TryReadRequestId(message, out var id))
				{
					PendingRequest pendingRequest = null;
					lock (_pendingLock)
					{
						_pending.TryGetValue(id, out pendingRequest);
					}
					if (pendingRequest != null)
					{
						pendingRequest.Response = message;
						try
						{
							pendingRequest.Completed.Set();
						}
						catch (ObjectDisposedException)
						{
						}
					}
					continue;
				}
				string method = GetString(message, "method");
				if (string.Equals(method, "account/rateLimits/updated", StringComparison.Ordinal))
				{
					IDictionary<string, object> parameters = GetDictionary(message, "params");
					IDictionary<string, object> response = (parameters == null) ? null : new Dictionary<string, object>
					{
						{ "result", parameters }
					};
					ThreadPool.QueueUserWorkItem(delegate
					{
						try
						{
							RateLimitsChanged?.Invoke(this, new CodexRateLimitsChangedEventArgs
							{
								RateLimitsResponse = response
							});
						}
						catch
						{
						}
					});
				}
				else if (string.Equals(method, "account/updated", StringComparison.Ordinal))
				{
					ClearCachedAccount();
				}
			}
		}
		catch
		{
		}
		finally
		{
			FailPendingRequests("Codex app-server disconnected.");
			lock (_stateLock)
			{
				if (ReferenceEquals(_process, process))
				{
					_initialized = false;
				}
			}
		}
	}

	private void DrainErrorLoop(Process process)
	{
		try
		{
			string line;
			while (!_disposed && !process.HasExited && (line = process.StandardError.ReadLine()) != null)
			{
				if (!string.IsNullOrWhiteSpace(line))
				{
					_lastErrorLine = line;
				}
			}
		}
		catch
		{
		}
	}

	private static bool TryReadRequestId(IDictionary<string, object> message, out long id)
	{
		id = 0L;
		if (!message.TryGetValue("id", out var value) || value == null)
		{
			return false;
		}
		try
		{
			id = Convert.ToInt64(value, CultureInfo.InvariantCulture);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static IList<RuntimeCandidate> FindCodexRuntimes()
	{
		List<RuntimeCandidate> candidates = new List<RuntimeCandidate>();
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string onlyRuntime = (Environment.GetEnvironmentVariable("CODEX_ORBIT_ONLY_RUNTIME") ?? "").Trim().ToLowerInvariant();
		Func<string, bool> include = kind => string.IsNullOrEmpty(onlyRuntime) || string.Equals(onlyRuntime, kind, StringComparison.Ordinal);
		Action<string, string> addNative = delegate(string path, string name)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return;
			}
			try
			{
				string fullPath = Path.GetFullPath(path.Trim().Trim('"'));
				if (!File.Exists(fullPath) || fullPath.IndexOf(Path.Combine("WindowsApps", "OpenAI.Codex_"), StringComparison.OrdinalIgnoreCase) >= 0 || !seen.Add("native|" + fullPath))
				{
					return;
				}
				candidates.Add(new RuntimeCandidate
				{
					Key = "native|" + fullPath,
					DisplayName = name,
					FileName = fullPath,
					Arguments = "app-server"
				});
			}
			catch
			{
			}
		};
		Action<string, string> addCommand = delegate(string path, string name)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return;
			}
			try
			{
				string fullPath = Path.GetFullPath(path.Trim().Trim('"'));
				if (!File.Exists(fullPath) || !seen.Add("command|" + fullPath))
				{
					return;
				}
				string commandProcessor = Environment.GetEnvironmentVariable("COMSPEC");
				if (string.IsNullOrWhiteSpace(commandProcessor))
				{
					commandProcessor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
				}
				candidates.Add(new RuntimeCandidate
				{
					Key = "command|" + fullPath,
					DisplayName = name,
					FileName = commandProcessor,
					Arguments = "/d /s /c \"\"" + fullPath.Replace("\"", "\"\"") + "\" app-server\""
				});
			}
			catch
			{
			}
		};

		string overridePath = Environment.GetEnvironmentVariable("CODEX_ORBIT_CODEX_PATH");
		if (!string.IsNullOrWhiteSpace(overridePath))
		{
			int overrideStartIndex = candidates.Count;
			if (overridePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || overridePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
			{
				addCommand(overridePath, "自定义 Codex CLI");
			}
			else
			{
				addNative(overridePath, "自定义 Codex CLI");
			}
			for (int i = overrideStartIndex; i < candidates.Count; i++)
			{
				candidates[i].StartupTimeoutMilliseconds = 11000;
			}
		}
		if (string.Equals(onlyRuntime, "override", StringComparison.Ordinal))
		{
			return candidates;
		}

		try
		{
			if (include("running"))
			{
				foreach (Process process in Process.GetProcessesByName("codex"))
				{
					try
					{
						string processPath = process.MainModule?.FileName;
						addNative(processPath, RuntimeNameForPath(processPath));
					}
					catch
					{
					}
					finally
					{
						process.Dispose();
					}
				}
			}
		}
		catch
		{
		}

		string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		if (include("app") && !string.IsNullOrWhiteSpace(localAppData))
		{
			AddExecutablesUnder(Path.Combine(localAppData, "OpenAI", "Codex", "bin"), "Codex App", addNative);
		}
		if (include("local") && !string.IsNullOrWhiteSpace(localAppData))
		{
			AddExecutablesUnder(Path.Combine(localAppData, "CodexQuota", "runtime"), "Codex Orbit 本地运行时", addNative);
		}

		string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		if (include("cli") && !string.IsNullOrWhiteSpace(appData))
		{
			AddExecutablesUnder(Path.Combine(appData, "npm", "node_modules", "@openai", "codex"), "Codex CLI", addNative);
		}

		string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (include("local") && !string.IsNullOrWhiteSpace(userProfile))
		{
			AddExecutablesUnder(Path.Combine(userProfile, ".codex", "bin"), "Codex 本地运行时", addNative);
		}

		string path = Environment.GetEnvironmentVariable("PATH") ?? "";
		if (include("path"))
		{
			foreach (string part in path.Split(Path.PathSeparator))
			{
				string directory = part.Trim().Trim('"');
				if (string.IsNullOrWhiteSpace(directory))
				{
					continue;
				}
				addNative(Path.Combine(directory, "codex.exe"), "PATH 中的 Codex CLI");
				addCommand(Path.Combine(directory, "codex.cmd"), "PATH 中的 Codex CLI");
				addCommand(Path.Combine(directory, "codex.bat"), "PATH 中的 Codex CLI");
			}
		}

		if (include("extension") && !string.IsNullOrWhiteSpace(userProfile))
		{
			foreach (string root in new[]
			{
				Path.Combine(userProfile, ".vscode", "extensions"),
				Path.Combine(userProfile, ".vscode-insiders", "extensions"),
				Path.Combine(userProfile, ".vscode-oss", "extensions"),
				Path.Combine(userProfile, ".cursor", "extensions"),
				Path.Combine(userProfile, ".windsurf", "extensions")
			})
			{
				AddExtensionRuntimes(root, addNative);
			}
		}

		if (include("package"))
		{
			AddPackagedAppRuntime(candidates, seen, localAppData);
		}
		if (include("wsl"))
		{
			AddWslRuntimes(candidates, seen);
		}
		return candidates;
	}

	private static string RuntimeNameForPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return "Codex";
		}
		if (path.IndexOf(Path.Combine("OpenAI", "Codex"), StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "Codex App";
		}
		if (path.IndexOf(Path.Combine("node_modules", "@openai", "codex"), StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "Codex CLI";
		}
		return "正在运行的 Codex";
	}

	private static void AddExecutablesUnder(string root, string displayName, Action<string, string> add)
	{
		try
		{
			if (!Directory.Exists(root))
			{
				return;
			}
			foreach (string path in Directory.EnumerateFiles(root, "codex.exe", SearchOption.AllDirectories)
				.OrderByDescending(File.GetLastWriteTimeUtc)
				.Take(12))
			{
				add(path, displayName);
			}
		}
		catch
		{
		}
	}

	private static void AddExtensionRuntimes(string root, Action<string, string> add)
	{
		try
		{
			if (!Directory.Exists(root))
			{
				return;
			}
			foreach (string directory in Directory.EnumerateDirectories(root, "openai.chatgpt-*", SearchOption.TopDirectoryOnly)
				.OrderByDescending(Directory.GetLastWriteTimeUtc)
				.Take(4))
			{
				AddExecutablesUnder(directory, "Codex 编辑器扩展", add);
			}
		}
		catch
		{
		}
	}

	private static void AddPackagedAppRuntime(ICollection<RuntimeCandidate> candidates, ISet<string> seen, string localAppData)
	{
		if (string.IsNullOrWhiteSpace(localAppData) || string.Equals(Environment.GetEnvironmentVariable("CODEX_ORBIT_DISABLE_PACKAGED_RUNTIME"), "1", StringComparison.Ordinal))
		{
			return;
		}
		try
		{
			List<string> packageRoots = new List<string>();
			using (RegistryKey packages = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
			{
				if (packages != null)
				{
					foreach (string packageId in packages.GetSubKeyNames().Where(name => name.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase)))
					{
						using RegistryKey packageKey = packages.OpenSubKey(packageId);
						string packageRoot = packageKey?.GetValue("PackageRootFolder") as string;
						if (!string.IsNullOrWhiteSpace(packageRoot))
						{
							packageRoots.Add(packageRoot);
						}
					}
				}
			}
			string windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
			try
			{
				if (Directory.Exists(windowsApps))
				{
					packageRoots.AddRange(Directory.EnumerateDirectories(windowsApps, "OpenAI.Codex_*", SearchOption.TopDirectoryOnly));
				}
			}
			catch
			{
			}
			foreach (string package in packageRoots
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderByDescending(Directory.GetLastWriteTimeUtc)
				.Take(3))
			{
				string source = Path.Combine(package, "app", "resources", "codex.exe");
				if (!File.Exists(source))
				{
					continue;
				}
				string packageName = Path.GetFileName(package);
				string destination = Path.Combine(localAppData, "CodexQuota", "runtime", packageName, "codex.exe");
				string key = "package|" + source;
				if (!seen.Add(key))
				{
					continue;
				}
				candidates.Add(new RuntimeCandidate
				{
					Key = key,
					DisplayName = "Codex App 安装包",
					FileName = destination,
					Arguments = "app-server",
					CopyFrom = source,
					CleanupRoot = Path.Combine(localAppData, "CodexQuota", "runtime"),
					StartupTimeoutMilliseconds = 6000
				});
			}
		}
		catch
		{
		}
	}

	private static void AddWslRuntimes(ICollection<RuntimeCandidate> candidates, ISet<string> seen)
	{
		if (string.Equals(Environment.GetEnvironmentVariable("CODEX_ORBIT_DISABLE_WSL"), "1", StringComparison.Ordinal))
		{
			return;
		}
		try
		{
			string wsl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
			if (!File.Exists(wsl))
			{
				return;
			}
			List<string> distributions = new List<string>();
			string defaultId = null;
			string defaultName = null;
			using (RegistryKey root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss"))
			{
				defaultId = root?.GetValue("DefaultDistribution") as string;
				if (root != null)
				{
					foreach (string id in root.GetSubKeyNames())
					{
						using RegistryKey distribution = root.OpenSubKey(id);
						string name = distribution?.GetValue("DistributionName") as string;
						if (string.IsNullOrWhiteSpace(name))
						{
							continue;
						}
						if (string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase))
						{
							defaultName = name;
							distributions.Insert(0, name);
						}
						else
						{
							distributions.Add(name);
						}
					}
				}
			}
			if (!string.IsNullOrWhiteSpace(defaultName) && seen.Add("wsl|default"))
			{
				candidates.Add(new RuntimeCandidate
				{
					Key = "wsl|default",
					DisplayName = "WSL · " + defaultName,
					FileName = wsl,
					Arguments = "-- sh -lc \"command -v codex >/dev/null 2>&1 && exec codex app-server\"",
					OutputEncoding = Encoding.UTF8,
					ErrorEncoding = Encoding.Unicode,
					StartupTimeoutMilliseconds = 8000
				});
			}
			foreach (string distribution in distributions.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (string.Equals(distribution, defaultName, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string key = "wsl|" + distribution;
				if (!seen.Add(key))
				{
					continue;
				}
				candidates.Add(new RuntimeCandidate
				{
					Key = key,
					DisplayName = "WSL · " + distribution,
					FileName = wsl,
					Arguments = "-d \"" + distribution.Replace("\"", "\\\"") + "\" -- sh -lc \"command -v codex >/dev/null 2>&1 && exec codex app-server\"",
					OutputEncoding = Encoding.UTF8,
					ErrorEncoding = Encoding.Unicode,
					StartupTimeoutMilliseconds = 8000
				});
			}
		}
		catch
		{
		}
	}

	private IDictionary<string, object> GetCachedAccount(string runtimeKey)
	{
		lock (_accountCacheLock)
		{
			if (!string.IsNullOrWhiteSpace(runtimeKey) && string.Equals(runtimeKey, _cachedAccountRuntimeKey, StringComparison.OrdinalIgnoreCase))
			{
				return _cachedAccountResponse;
			}
			return null;
		}
	}

	private void SetCachedAccount(string runtimeKey, IDictionary<string, object> response)
	{
		lock (_accountCacheLock)
		{
			_cachedAccountRuntimeKey = runtimeKey;
			_cachedAccountResponse = response;
		}
	}

	private void ClearCachedAccount()
	{
		lock (_accountCacheLock)
		{
			_cachedAccountRuntimeKey = null;
			_cachedAccountResponse = null;
		}
	}

	private static bool HasResult(IDictionary<string, object> response)
	{
		return response != null && response.ContainsKey("result") && !response.ContainsKey("error");
	}

	private static string DescribeFailure(IDictionary<string, object> accountResponse, IDictionary<string, object> rateResponse)
	{
		IDictionary<string, object> accountResult = GetDictionary(accountResponse, "result");
		IDictionary<string, object> account = GetDictionary(accountResult, "account");
		if (account == null && accountResult != null)
		{
			return "Codex 尚未登录 ChatGPT";
		}
		string accountType = GetString(account, "type");
		if (!string.IsNullOrWhiteSpace(accountType) && !string.Equals(accountType, "chatgpt", StringComparison.OrdinalIgnoreCase))
		{
			return "当前 Codex 使用 " + accountType + " 登录，无法读取 ChatGPT 套餐额度";
		}
		IDictionary<string, object> error = GetDictionary(rateResponse, "error");
		string message = GetString(error, "message");
		return string.IsNullOrWhiteSpace(message) ? "Codex 账户额度服务暂不可用" : "Codex 实时服务：" + message;
	}

	private static string Shorten(string value)
	{
		string singleLine = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
		return singleLine.Length <= 180 ? singleLine : singleLine.Substring(0, 177) + "...";
	}

	private static IDictionary<string, object> GetDictionary(IDictionary<string, object> source, string key)
	{
		if (source == null || !source.TryGetValue(key, out var value))
		{
			return null;
		}
		return value as IDictionary<string, object>;
	}

	private static string GetString(IDictionary<string, object> source, string key)
	{
		if (source == null || !source.TryGetValue(key, out var value) || value == null)
		{
			return null;
		}
		return Convert.ToString(value, CultureInfo.InvariantCulture);
	}

	private void FailPendingRequests(string failure)
	{
		PendingRequest[] requests;
		lock (_pendingLock)
		{
			requests = _pending.Values.ToArray();
		}
		foreach (PendingRequest pendingRequest in requests)
		{
			pendingRequest.Failure = failure;
			try
			{
				pendingRequest.Completed.Set();
			}
			catch (ObjectDisposedException)
			{
			}
		}
	}

	private void StopProcessNoLock()
	{
		_initialized = false;
		_activeRuntime = null;
		ClearCachedAccount();
		StreamWriter input = _input;
		_input = null;
		try
		{
			input?.Dispose();
		}
		catch
		{
		}
		Process process = _process;
		_process = null;
		if (process != null)
		{
			try
			{
				if (!process.HasExited)
				{
					process.Kill();
					process.WaitForExit(2000);
				}
			}
			catch
			{
			}
			try
			{
				process.Dispose();
			}
			catch
			{
			}
		}
		FailPendingRequests("Codex app-server stopped.");
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		lock (_stateLock)
		{
			StopProcessNoLock();
		}
	}
}
