using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using CodexQuota.Models;

namespace CodexQuota.Services;

public sealed class CodexUsageReader : IDisposable
{
	private sealed class CachedFile
	{
		public long Length;

		public DateTime LastWriteUtc;

		public UsageWindowSnapshot Short;

		public UsageWindowSnapshot Week;

		public string PlanType;

		public int PlanPriority;

		public bool SeenInScan;
	}

	public const int ShortWindowMinutes = 300;

	public const int WeekWindowMinutes = 10080;

	public const int ShortWindowMaxMinutes = 1440;

	private const int MaxFilesToScan = 160;

	private const int TailBytesPerFile = 262144;

	private const int MaxLinesPerFile = 400;

	private const int MaxFileAgeDays = 10;

	private const int DebounceMilliseconds = 300;

	private const int SafetyIntervalSeconds = 5;

	private const int MinimumLiveRefreshMilliseconds = 2000;

	private const int FailedLiveRetrySeconds = 15;

	private const int LiveSnapshotGraceSeconds = 30;

	private static readonly byte[] RateLimitsMarker = Encoding.ASCII.GetBytes("\"rate_limits\"");

	[ThreadStatic]
	private static JavaScriptSerializer _threadJson;

	private readonly string _sessionsPath;

	private readonly object _scanLock = new object();

	private readonly object _watcherLock = new object();

	private readonly Dictionary<string, CachedFile> _cache = new Dictionary<string, CachedFile>(StringComparer.OrdinalIgnoreCase);

	private readonly CodexAppServerClient _appServer;

	private FileSystemWatcher _watcher;

	private Timer _debounceTimer;

	private Timer _safetyTimer;

	private UsageSnapshot _lastRaised;

	private UsageSnapshot _lastLiveSnapshot;

	private IDictionary<string, object> _lastLiveRateLimitsResponse;

	private DateTimeOffset _lastLiveAttempt;

	private DateTimeOffset _lastLiveSuccess;

	private DateTimeOffset _lastLiveFailure;

	private volatile bool _disposed;

	public string SessionsPath => _sessionsPath;

	private static JavaScriptSerializer Json
	{
		get
		{
			if (_threadJson == null)
			{
				_threadJson = new JavaScriptSerializer
				{
					MaxJsonLength = int.MaxValue,
					RecursionLimit = 128
				};
			}
			return _threadJson;
		}
	}

	public event EventHandler<UsageSnapshot> SnapshotChanged;

	public CodexUsageReader(string sessionsPath)
	{
		_sessionsPath = sessionsPath;
		_appServer = new CodexAppServerClient();
		_appServer.RateLimitsChanged += AppServer_RateLimitsChanged;
		_debounceTimer = new Timer(OnDebounceElapsed, null, -1, -1);
	}

	public static string GetDefaultSessionsPath()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
	}

	public UsageSnapshot ReadLatest()
	{
		lock (_scanLock)
		{
			return ScanCore();
		}
	}

	private UsageSnapshot ScanCore()
	{
		UsageSnapshot usageSnapshot = ReadLiveSnapshot();
		if (usageSnapshot != null && usageSnapshot.HasAnyData)
		{
			return usageSnapshot;
		}
		UsageSnapshot usageSnapshot2 = ScanLocalCore();
		if (usageSnapshot2 != null)
		{
			if (usageSnapshot2.HasAnyData)
			{
				usageSnapshot2.StatusMessage = "非实时 · " + (_appServer.LastFailure ?? "账户服务暂不可用");
			}
			else if (!string.IsNullOrWhiteSpace(_appServer.LastFailure))
			{
				usageSnapshot2.StatusMessage = _appServer.LastFailure + " · 无可用历史快照";
			}
		}
		return usageSnapshot2;
	}

	private UsageSnapshot ScanLocalCore()
	{
		if (!Directory.Exists(_sessionsPath))
		{
			return new UsageSnapshot
			{
				StatusMessage = "未找到 Codex 会话目录"
			};
		}
		try
		{
			DateTime dateTime = DateTime.UtcNow.AddDays(-10.0);
			List<FileInfo> list = new List<FileInfo>();
			foreach (string item in EnumerateRecentSessionFiles())
			{
				FileInfo fileInfo = new FileInfo(item);
				if (fileInfo.Exists && fileInfo.Length != 0L && !(fileInfo.LastWriteTimeUtc < dateTime))
				{
					list.Add(fileInfo);
				}
			}
			list.Sort((FileInfo a, FileInfo b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
			if (list.Count > 160)
			{
				list.RemoveRange(160, list.Count - 160);
			}
			foreach (CachedFile value in _cache.Values)
			{
				value.SeenInScan = false;
			}
			UsageWindowSnapshot usageWindowSnapshot = null;
			UsageWindowSnapshot usageWindowSnapshot2 = null;
			string text = null;
			foreach (FileInfo item2 in list)
			{
				CachedFile orScanFile = GetOrScanFile(item2);
				if (orScanFile != null)
				{
					orScanFile.SeenInScan = true;
					if (IsPreferred(orScanFile.Short, usageWindowSnapshot))
					{
						usageWindowSnapshot = orScanFile.Short;
					}
					if (IsPreferred(orScanFile.Week, usageWindowSnapshot2))
					{
						usageWindowSnapshot2 = orScanFile.Week;
					}
					if (!string.IsNullOrEmpty(orScanFile.PlanType) && (text == null || orScanFile.Short != null && (usageWindowSnapshot == null || orScanFile.Short.ObservedAt >= usageWindowSnapshot.ObservedAt) || orScanFile.Week != null && (usageWindowSnapshot2 == null || orScanFile.Week.ObservedAt >= usageWindowSnapshot2.ObservedAt)))
					{
						text = orScanFile.PlanType;
					}
				}
			}
			foreach (string item3 in (from kv in _cache
				where !kv.Value.SeenInScan
				select kv.Key).ToList())
			{
				_cache.Remove(item3);
			}
			usageWindowSnapshot = DiscardStaleShortWindow(usageWindowSnapshot, usageWindowSnapshot2);
			return new UsageSnapshot
			{
				ShortWindow = usageWindowSnapshot,
				WeekWindow = usageWindowSnapshot2,
				PlanType = text,
				StatusMessage = ((usageWindowSnapshot == null && usageWindowSnapshot2 == null) ? "暂未在本地日志中找到额度信息" : "已从 Codex 本地日志同步")
			};
		}
		catch (UnauthorizedAccessException)
		{
			return new UsageSnapshot
			{
				StatusMessage = "没有权限读取 Codex 会话目录"
			};
		}
		catch (IOException)
		{
			return new UsageSnapshot
			{
				StatusMessage = "Codex 日志正在写入，请稍后重试"
			};
		}
		catch (Exception)
		{
			return new UsageSnapshot
			{
				StatusMessage = "读取本地额度时发生错误"
			};
		}
	}

	private IEnumerable<string> EnumerateRecentSessionFiles()
	{
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string item in Directory.EnumerateFiles(_sessionsPath, "rollout-*.jsonl", SearchOption.TopDirectoryOnly))
		{
			if (seen.Add(item))
			{
				yield return item;
			}
		}
		bool foundStructuredDirectory = false;
		DateTime day = DateTime.UtcNow.Date;
		for (int offset = 0; offset <= 11; offset++)
		{
			DateTime dateTime = day.AddDays(-offset);
			string path = Path.Combine(_sessionsPath, dateTime.Year.ToString("0000", CultureInfo.InvariantCulture), dateTime.Month.ToString("00", CultureInfo.InvariantCulture), dateTime.Day.ToString("00", CultureInfo.InvariantCulture));
			if (!Directory.Exists(path))
			{
				continue;
			}
			foundStructuredDirectory = true;
			foreach (string item2 in Directory.EnumerateFiles(path, "rollout-*.jsonl", SearchOption.TopDirectoryOnly))
			{
				if (seen.Add(item2))
				{
					yield return item2;
				}
			}
		}
		if (foundStructuredDirectory)
		{
			yield break;
		}
		foreach (string item3 in Directory.EnumerateFiles(_sessionsPath, "rollout-*.jsonl", SearchOption.AllDirectories))
		{
			if (seen.Add(item3))
			{
				yield return item3;
			}
		}
	}

	private CachedFile GetOrScanFile(FileInfo file)
	{
		if (_cache.TryGetValue(file.FullName, out var value) && value.Length == file.Length && value.LastWriteUtc == file.LastWriteTimeUtc)
		{
			return value;
		}
		try
		{
			value = ScanFileTail(file);
		}
		catch (IOException)
		{
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}
		_cache[file.FullName] = value;
		return value;
	}

	private CachedFile ScanFileTail(FileInfo file)
	{
		CachedFile cachedFile = new CachedFile
		{
			Length = file.Length,
			LastWriteUtc = file.LastWriteTimeUtc
		};
		long num;
		byte[] array;
		using (FileStream fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
		{
			long length = fileStream.Length;
			num = (fileStream.Position = Math.Max(0L, length - 262144));
			array = new byte[length - num];
			int i;
			int num3;
			for (i = 0; i < array.Length; i += num3)
			{
				num3 = fileStream.Read(array, i, array.Length - i);
				if (num3 == 0)
				{
					break;
				}
			}
			if (i < array.Length)
			{
				Array.Resize(ref array, i);
			}
		}
		cachedFile.Length = num + array.Length;
		int num4 = array.Length;
		int num5 = 0;
		while (num4 > 0 && num5 < 400)
		{
			int num6 = LastIndexOf(array, RateLimitsMarker, num4);
			if (num6 < 0)
			{
				break;
			}
			int num7 = Array.LastIndexOf(array, (byte)10, num6) + 1;
			int num8 = Array.IndexOf(array, (byte)10, num6);
			if (num8 < 0)
			{
				num8 = array.Length;
			}
			num4 = ((num7 > 0) ? (num7 - 1) : 0);
			num5++;
			if (num7 == 0 && num > 0)
			{
				continue;
			}
			string line = Encoding.UTF8.GetString(array, num7, num8 - num7).TrimEnd('\r');
			IList<UsageWindowSnapshot> list = ParseLineCore(line, file.FullName, out var planType);
			foreach (UsageWindowSnapshot item in list)
			{
				if (!IsPrimaryLimit(item))
				{
					continue;
				}
				if (IsShortBucket(item.WindowMinutes))
				{
					if (IsPreferred(item, cachedFile.Short))
					{
						cachedFile.Short = item;
					}
				}
				else if (IsPreferred(item, cachedFile.Week))
				{
					cachedFile.Week = item;
				}
			}
			if (!string.IsNullOrEmpty(planType) && list.Count > 0)
			{
				int num9 = list.Max((UsageWindowSnapshot x) => GetLimitPriority(x.LimitId));
				if (cachedFile.PlanType == null || num9 >= cachedFile.PlanPriority)
				{
					cachedFile.PlanType = planType;
					cachedFile.PlanPriority = num9;
				}
			}
		}
		return cachedFile;
	}

	private static bool IsShortBucket(int minutes)
	{
		return minutes <= 1440;
	}

	private static bool IsPrimaryLimit(UsageWindowSnapshot window)
	{
		if (window == null || !string.IsNullOrWhiteSpace(window.LimitName) && window.LimitName.IndexOf("Spark", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return false;
		}
		return string.IsNullOrWhiteSpace(window.LimitId) || string.Equals(window.LimitId, "codex", StringComparison.OrdinalIgnoreCase);
	}

	internal static int GetLimitPriority(string limitId)
	{
		if (string.Equals(limitId, "codex", StringComparison.OrdinalIgnoreCase))
		{
			return 3;
		}
		if (string.IsNullOrWhiteSpace(limitId))
		{
			return 2;
		}
		return 1;
	}

	private static int GetPlanPriority(string planType)
	{
		return (!string.IsNullOrWhiteSpace(planType)) ? 1 : 0;
	}

	private static bool IsPreferred(UsageWindowSnapshot candidate, UsageWindowSnapshot current)
	{
		if (candidate == null)
		{
			return false;
		}
		if (current == null)
		{
			return true;
		}
		if (candidate.ObservedAt != current.ObservedAt)
		{
			return candidate.ObservedAt > current.ObservedAt;
		}
		return GetLimitPriority(candidate.LimitId) > GetLimitPriority(current.LimitId);
	}

	internal static UsageWindowSnapshot DiscardStaleShortWindow(UsageWindowSnapshot shortWindow, UsageWindowSnapshot weekWindow)
	{
		if (shortWindow == null)
		{
			return null;
		}
		if (weekWindow != null && weekWindow.ObservedAt > shortWindow.ObservedAt)
		{
			return null;
		}
		return shortWindow;
	}

	private static int LastIndexOf(byte[] buffer, byte[] pattern, int endExclusive)
	{
		byte b = pattern[0];
		for (int num = Math.Min(endExclusive, buffer.Length) - pattern.Length; num >= 0; num--)
		{
			if (buffer[num] == b)
			{
				bool flag = true;
				for (int i = 1; i < pattern.Length; i++)
				{
					if (buffer[num + i] != pattern[i])
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					return num;
				}
			}
		}
		return -1;
	}

	private UsageSnapshot ReadLiveSnapshot()
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		if (_lastLiveFailure != DateTimeOffset.MinValue && (now - _lastLiveFailure).TotalSeconds < FailedLiveRetrySeconds)
		{
			return CreateGraceSnapshot(now);
		}
		if (_lastLiveSnapshot != null && (now - _lastLiveAttempt).TotalMilliseconds < MinimumLiveRefreshMilliseconds)
		{
			return _lastLiveSnapshot;
		}
		_lastLiveAttempt = now;
		UsageSnapshot usageSnapshot = FetchLiveSnapshot();
		if (usageSnapshot != null && usageSnapshot.HasAnyData)
		{
			_lastLiveSnapshot = usageSnapshot;
			_lastLiveSuccess = now;
			_lastLiveFailure = DateTimeOffset.MinValue;
			return usageSnapshot;
		}
		_lastLiveFailure = now;
		return CreateGraceSnapshot(now);
	}

	private UsageSnapshot CreateGraceSnapshot(DateTimeOffset now)
	{
		if (_lastLiveSnapshot != null && (now - _lastLiveSuccess).TotalSeconds <= LiveSnapshotGraceSeconds)
		{
			return new UsageSnapshot
			{
				ShortWindow = _lastLiveSnapshot.ShortWindow,
				WeekWindow = _lastLiveSnapshot.WeekWindow,
				PlanType = _lastLiveSnapshot.PlanType,
				IsLive = false,
				StatusMessage = "实时重连中 · " + (_appServer.LastFailure ?? "保留最近快照")
			};
		}
		return null;
	}

	private UsageSnapshot FetchLiveSnapshot()
	{
		try
		{
			CodexLiveResult liveResult = _appServer.ReadLive(12000);
			UsageSnapshot snapshot = BuildLiveSnapshot(liveResult?.RateLimitsResponse, liveResult?.AccountResponse, null, "实时同步 · " + (liveResult?.RuntimeName ?? "Codex 账户"));
			if (snapshot != null && snapshot.HasAnyData)
			{
				_lastLiveRateLimitsResponse = CloneDictionary(liveResult.RateLimitsResponse);
			}
			return snapshot;
		}
		catch
		{
			return null;
		}
	}

	private static UsageSnapshot BuildLiveSnapshot(IDictionary<string, object> response, IDictionary<string, object> accountResponse, string fallbackPlanType, string statusMessage)
	{
		IDictionary<string, object> result = GetDictionary(response, "result");
		if (result == null)
		{
			return null;
		}
		IDictionary<string, object> byLimitId = GetDictionary(result, "rateLimitsByLimitId");
		IDictionary<string, object> overall = null;
		if (byLimitId != null && byLimitId.TryGetValue("codex", out var codexValue))
		{
			overall = codexValue as IDictionary<string, object>;
		}
		if (overall == null)
		{
			overall = GetDictionary(result, "rateLimits");
		}
		DateTimeOffset observedAt = DateTimeOffset.UtcNow;
		List<UsageWindowSnapshot> overallWindows = ReadLiveWindows(overall, observedAt)
			.Where(IsPrimaryLimit)
			.ToList();
		UsageWindowSnapshot shortWindow = overallWindows
			.Where((UsageWindowSnapshot window) => IsShortBucket(window.WindowMinutes))
			.OrderBy((UsageWindowSnapshot window) => window.WindowMinutes)
			.FirstOrDefault();
		UsageWindowSnapshot weekWindow = overallWindows
			.Where((UsageWindowSnapshot window) => !IsShortBucket(window.WindowMinutes))
			.OrderByDescending((UsageWindowSnapshot window) => window.WindowMinutes)
			.FirstOrDefault();
		if (shortWindow == null && weekWindow == null)
		{
			return null;
		}
		IDictionary<string, object> accountResult = GetDictionary(accountResponse, "result");
		IDictionary<string, object> account = GetDictionary(accountResult, "account");
		string planType = GetString(account, "planType");
		if (string.IsNullOrWhiteSpace(planType))
		{
			planType = GetString(overall, "planType");
		}
		if (string.IsNullOrWhiteSpace(planType) && byLimitId != null)
		{
			foreach (object value in byLimitId.Values)
			{
				planType = GetString(value as IDictionary<string, object>, "planType");
				if (!string.IsNullOrWhiteSpace(planType))
				{
					break;
				}
			}
		}
		if (string.IsNullOrWhiteSpace(planType))
		{
			planType = fallbackPlanType;
		}
		return new UsageSnapshot
		{
			ShortWindow = shortWindow,
			WeekWindow = weekWindow,
			PlanType = planType,
			IsLive = true,
			StatusMessage = string.IsNullOrWhiteSpace(statusMessage) ? "实时同步 · Codex 账户" : statusMessage
		};
	}

	private static List<UsageWindowSnapshot> ReadLiveWindows(IDictionary<string, object> limitSnapshot, DateTimeOffset observedAt)
	{
		List<UsageWindowSnapshot> windows = new List<UsageWindowSnapshot>();
		if (limitSnapshot == null)
		{
			return windows;
		}
		string limitId = GetString(limitSnapshot, "limitId");
		string limitName = GetString(limitSnapshot, "limitName");
		AddLiveWindow(limitSnapshot, "primary", observedAt, limitId, limitName, windows);
		AddLiveWindow(limitSnapshot, "secondary", observedAt, limitId, limitName, windows);
		return windows;
	}

	private static void AddLiveWindow(IDictionary<string, object> limitSnapshot, string key, DateTimeOffset observedAt, string limitId, string limitName, IList<UsageWindowSnapshot> windows)
	{
		IDictionary<string, object> window = GetDictionary(limitSnapshot, key);
		if (window == null || !TryGetInt(window, "windowDurationMins", out var minutes) || minutes <= 0 || !TryGetDouble(window, "usedPercent", out var usedPercent) || !TryGetDouble(window, "resetsAt", out var resetsAt))
		{
			return;
		}
		try
		{
			windows.Add(new UsageWindowSnapshot
			{
				WindowMinutes = minutes,
				UsedPercent = Math.Max(0.0, Math.Min(100.0, usedPercent)),
				ResetsAt = DateTimeOffset.FromUnixTimeSeconds((long)resetsAt),
				ObservedAt = observedAt,
				SourceFile = "Codex account service",
				LimitId = limitId,
				LimitName = limitName
			});
		}
		catch (ArgumentOutOfRangeException)
		{
		}
	}

	public IList<UsageWindowSnapshot> ParseLine(string line, string sourceFile)
	{
		string planType;
		return ParseLineCore(line, sourceFile, out planType);
	}

	private IList<UsageWindowSnapshot> ParseLineCore(string line, string sourceFile, out string planType)
	{
		planType = null;
		List<UsageWindowSnapshot> list = new List<UsageWindowSnapshot>();
		if (string.IsNullOrWhiteSpace(line) || line.IndexOf("\"rate_limits\"", StringComparison.Ordinal) < 0 || line.IndexOf("\"token_count\"", StringComparison.Ordinal) < 0)
		{
			return list;
		}
		try
		{
			if (!(Json.DeserializeObject(line) is IDictionary<string, object> source) || GetString(source, "type") != "event_msg")
			{
				return list;
			}
			IDictionary<string, object> dictionary = GetDictionary(source, "payload");
			if (dictionary == null || GetString(dictionary, "type") != "token_count")
			{
				return list;
			}
			IDictionary<string, object> dictionary2 = GetDictionary(dictionary, "rate_limits");
			if (dictionary2 == null)
			{
				return list;
			}
			if (!DateTimeOffset.TryParse(GetString(source, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result))
			{
				result = DateTimeOffset.UtcNow;
			}
			planType = GetString(dictionary2, "plan_type");
			string limitId = GetString(dictionary2, "limit_id");
			string limitName = GetString(dictionary2, "limit_name");
			AddWindow(dictionary2, "primary", result, sourceFile, limitId, limitName, list);
			AddWindow(dictionary2, "secondary", result, sourceFile, limitId, limitName, list);
		}
		catch (ArgumentException)
		{
		}
		catch (InvalidOperationException)
		{
		}
		return list;
	}

	public void StartWatching()
	{
		if (_disposed)
		{
			return;
		}
		if (_safetyTimer == null)
		{
			_safetyTimer = new Timer(delegate
			{
				OnSafetyTick();
			}, null, TimeSpan.FromSeconds(SafetyIntervalSeconds), TimeSpan.FromSeconds(SafetyIntervalSeconds));
		}
		EnsureWatcher();
	}

	private void AppServer_RateLimitsChanged(object sender, CodexRateLimitsChangedEventArgs e)
	{
		if (_disposed)
		{
			return;
		}
		UsageSnapshot update = null;
		lock (_scanLock)
		{
			UsageSnapshot previous = _lastLiveSnapshot;
			IDictionary<string, object> mergedResponse = MergeSparseResponse(_lastLiveRateLimitsResponse, e?.RateLimitsResponse);
			update = BuildLiveSnapshot(mergedResponse, null, previous?.PlanType, previous?.StatusMessage);
			if (update != null)
			{
				if (previous != null)
				{
					update.ShortWindow = update.ShortWindow ?? previous.ShortWindow;
					update.WeekWindow = update.WeekWindow ?? previous.WeekWindow;
				}
				DateTimeOffset now = DateTimeOffset.UtcNow;
				_lastLiveSnapshot = update;
				_lastLiveRateLimitsResponse = mergedResponse;
				_lastLiveAttempt = now;
				_lastLiveSuccess = now;
				_lastLiveFailure = DateTimeOffset.MinValue;
			}
			else
			{
				_lastLiveAttempt = DateTimeOffset.MinValue;
				_lastLiveFailure = DateTimeOffset.MinValue;
			}
		}
		if (update != null)
		{
			PublishIfChanged(update);
		}
	}

	private void OnSafetyTick()
	{
		if (!_disposed)
		{
			EnsureWatcher();
			RequestRefresh();
		}
	}

	private void EnsureWatcher()
	{
		if (_disposed)
		{
			return;
		}
		lock (_watcherLock)
		{
			if (_disposed || _watcher != null || !Directory.Exists(_sessionsPath))
			{
				return;
			}
			try
			{
				FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(_sessionsPath, "*.jsonl")
				{
					IncludeSubdirectories = true,
					NotifyFilter = (NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime),
					InternalBufferSize = 65536,
					EnableRaisingEvents = false
				};
				fileSystemWatcher.Changed += OnFileChanged;
				fileSystemWatcher.Created += OnFileChanged;
				fileSystemWatcher.Renamed += OnFileRenamed;
				fileSystemWatcher.Error += OnWatcherError;
				fileSystemWatcher.EnableRaisingEvents = true;
				_watcher = fileSystemWatcher;
			}
			catch (Exception)
			{
			}
		}
	}

	public void RequestRefresh(bool forceLive = false)
	{
		if (_disposed)
		{
			return;
		}
		if (forceLive)
		{
			lock (_scanLock)
			{
				_lastLiveAttempt = DateTimeOffset.MinValue;
				_lastLiveFailure = DateTimeOffset.MinValue;
			}
		}
		Timer debounceTimer = _debounceTimer;
		if (debounceTimer == null)
		{
			return;
		}
		try
		{
			debounceTimer.Change(300, -1);
		}
		catch (ObjectDisposedException)
		{
		}
	}

	private void OnFileChanged(object sender, FileSystemEventArgs e)
	{
		string fileName = Path.GetFileName(e.FullPath);
		if (!string.IsNullOrEmpty(fileName) && fileName.StartsWith("rollout-", StringComparison.OrdinalIgnoreCase))
		{
			RequestRefresh();
		}
	}

	private void OnFileRenamed(object sender, RenamedEventArgs e)
	{
		OnFileChanged(sender, e);
	}

	private void OnWatcherError(object sender, ErrorEventArgs e)
	{
		lock (_watcherLock)
		{
			if (_watcher != null)
			{
				try
				{
					_watcher.Dispose();
				}
				catch
				{
				}
				_watcher = null;
			}
		}
		EnsureWatcher();
		RequestRefresh();
	}

	private void OnDebounceElapsed(object state)
	{
		if (_disposed)
		{
			return;
		}
		try
		{
			UsageSnapshot usageSnapshot = ReadLatest();
			PublishIfChanged(usageSnapshot);
		}
		catch (Exception exception)
		{
			App.LogError(exception);
		}
	}

	private void PublishIfChanged(UsageSnapshot snapshot)
	{
		bool publish = false;
		lock (_scanLock)
		{
			if (!_disposed && !SnapshotEquals(snapshot, _lastRaised))
			{
				_lastRaised = snapshot;
				publish = true;
			}
		}
		if (publish)
		{
			SnapshotChanged?.Invoke(this, snapshot);
		}
	}

	private static IDictionary<string, object> MergeSparseResponse(IDictionary<string, object> current, IDictionary<string, object> update)
	{
		if (update == null)
		{
			return current;
		}
		Dictionary<string, object> merged = CloneDictionary(current);
		MergeDictionary(merged, update);
		return merged;
	}

	private static Dictionary<string, object> CloneDictionary(IDictionary<string, object> source)
	{
		Dictionary<string, object> clone = new Dictionary<string, object>(StringComparer.Ordinal);
		if (source == null)
		{
			return clone;
		}
		foreach (KeyValuePair<string, object> item in source)
		{
			clone[item.Key] = CloneValue(item.Value);
		}
		return clone;
	}

	private static object CloneValue(object value)
	{
		if (value is IDictionary<string, object> dictionary)
		{
			return CloneDictionary(dictionary);
		}
		return value;
	}

	private static void MergeDictionary(IDictionary<string, object> target, IDictionary<string, object> update)
	{
		foreach (KeyValuePair<string, object> item in update)
		{
			if (item.Value == null)
			{
				continue;
			}
			if (item.Value is IDictionary<string, object> updateDictionary)
			{
				if (!(target.TryGetValue(item.Key, out var currentValue) && currentValue is IDictionary<string, object> currentDictionary))
				{
					currentDictionary = new Dictionary<string, object>(StringComparer.Ordinal);
					target[item.Key] = currentDictionary;
				}
				MergeDictionary(currentDictionary, updateDictionary);
			}
			else
			{
				target[item.Key] = item.Value;
			}
		}
	}

	private static bool SnapshotEquals(UsageSnapshot a, UsageSnapshot b)
	{
		if (a == null || b == null)
		{
			return false;
		}
		if (WindowEquals(a.ShortWindow, b.ShortWindow) && WindowEquals(a.WeekWindow, b.WeekWindow) && a.IsLive == b.IsLive && string.Equals(a.StatusMessage, b.StatusMessage, StringComparison.Ordinal))
		{
			return string.Equals(a.PlanType, b.PlanType, StringComparison.Ordinal);
		}
		return false;
	}

	private static bool WindowEquals(UsageWindowSnapshot a, UsageWindowSnapshot b)
	{
		if (a == null && b == null)
		{
			return true;
		}
		if (a == null || b == null)
		{
			return false;
		}
		if (a.WindowMinutes == b.WindowMinutes && a.UsedPercent.Equals(b.UsedPercent) && a.ResetsAt == b.ResetsAt && string.Equals(a.LimitId, b.LimitId, StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(a.LimitName, b.LimitName, StringComparison.Ordinal);
		}
		return false;
	}

	private static void AddWindow(IDictionary<string, object> rateLimits, string key, DateTimeOffset observedAt, string sourceFile, string limitId, string limitName, IList<UsageWindowSnapshot> results)
	{
		IDictionary<string, object> dictionary = GetDictionary(rateLimits, key);
		if (dictionary == null || !TryGetInt(dictionary, "window_minutes", out var value) || !TryGetDouble(dictionary, "used_percent", out var value2) || value <= 0)
		{
			return;
		}
		DateTimeOffset resetsAt;
		if (TryGetDouble(dictionary, "resets_at", out var value3))
		{
			resetsAt = DateTimeOffset.FromUnixTimeSeconds((long)value3);
		}
		else
		{
			if (!TryGetDouble(dictionary, "resets_in_seconds", out var value4))
			{
				return;
			}
			resetsAt = observedAt.AddSeconds(value4);
		}
		results.Add(new UsageWindowSnapshot
		{
			WindowMinutes = value,
			UsedPercent = value2,
			ResetsAt = resetsAt,
			ObservedAt = observedAt,
			SourceFile = sourceFile,
			LimitId = limitId,
			LimitName = limitName
		});
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

	private static bool TryGetInt(IDictionary<string, object> source, string key, out int value)
	{
		if (TryGetDouble(source, key, out var value2))
		{
			value = (int)value2;
			return true;
		}
		value = 0;
		return false;
	}

	private static bool TryGetDouble(IDictionary<string, object> source, string key, out double value)
	{
		if (source != null && source.TryGetValue(key, out var value2) && value2 != null)
		{
			return double.TryParse(Convert.ToString(value2, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
		}
		value = 0.0;
		return false;
	}

	public void Dispose()
	{
		_disposed = true;
		lock (_watcherLock)
		{
			if (_watcher != null)
			{
				try
				{
					_watcher.Dispose();
				}
				catch
				{
				}
				_watcher = null;
			}
		}
		Interlocked.Exchange(ref _debounceTimer, null)?.Dispose();
		Interlocked.Exchange(ref _safetyTimer, null)?.Dispose();
		_appServer.RateLimitsChanged -= AppServer_RateLimitsChanged;
		_appServer.Dispose();
	}
}
