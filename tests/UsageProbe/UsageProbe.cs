using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using CodexQuota.Models;
using CodexQuota.Services;

internal static class UsageProbe
{
	private static int _updates;

	private static readonly object FakeOutputLock = new object();

	private static void Print(string name, UsageSnapshot snapshot)
	{
		Console.WriteLine(
			name +
			"|live=" + snapshot.IsLive +
			"|status=" + snapshot.StatusMessage +
			"|short=" + Window(snapshot.ShortWindow) +
			"|week=" + Window(snapshot.WeekWindow));
	}

	private static string Window(UsageWindowSnapshot window)
	{
		if (window == null)
		{
			return "null";
		}
		return string.Format(
			"{0},{1},{2:0.###},{3:0.###},{4:o}",
			window.LimitId ?? "",
			window.LimitName ?? "",
			window.UsedPercent,
			window.RemainingPercent,
			window.ObservedAt);
	}

	public static int Main(string[] args)
	{
		if (args != null && Array.Exists(args, value => string.Equals(value, "app-server", StringComparison.OrdinalIgnoreCase)))
		{
			return RunFakeAppServer();
		}
		if (args != null && Array.Exists(args, value => string.Equals(value, "--notification-test", StringComparison.OrdinalIgnoreCase)))
		{
			return RunNotificationTest();
		}
		bool once = args != null && Array.Exists(args, value => string.Equals(value, "--once", StringComparison.OrdinalIgnoreCase));
		Type clientType = typeof(CodexUsageReader).Assembly.GetType("CodexQuota.Services.CodexAppServerClient");
		MethodInfo findRuntimes = clientType.GetMethod("FindCodexRuntimes", BindingFlags.Static | BindingFlags.NonPublic);
		ICollection runtimes = (ICollection)findRuntimes.Invoke(null, null);
		Console.WriteLine("RUNTIMES|count=" + runtimes.Count + "|mode=" + (Environment.GetEnvironmentVariable("CODEX_ORBIT_ONLY_RUNTIME") ?? "auto"));
		using (CodexUsageReader reader = new CodexUsageReader(CodexUsageReader.GetDefaultSessionsPath()))
		{
			UsageSnapshot live = reader.ReadLatest();
			Print("LIVE", live);
			if (!live.IsLive || live.WeekWindow == null || !string.Equals(live.WeekWindow.LimitId, "codex", StringComparison.OrdinalIgnoreCase))
			{
				Console.Error.WriteLine("Live Codex account snapshot was not selected.");
				return 10;
			}
			if (live.ShortWindow != null && !string.IsNullOrWhiteSpace(live.ShortWindow.LimitId) && !string.Equals(live.ShortWindow.LimitId, "codex", StringComparison.OrdinalIgnoreCase))
			{
				Console.Error.WriteLine("A model-specific bucket leaked into the primary display.");
				return 11;
			}
			if (once)
			{
				Console.WriteLine("PASS|once");
				return 0;
			}

			MethodInfo localScan = typeof(CodexUsageReader).GetMethod("ScanLocalCore", BindingFlags.Instance | BindingFlags.NonPublic);
			UsageSnapshot local = (UsageSnapshot)localScan.Invoke(reader, null);
			Print("LOCAL", local);
			if (local.WeekWindow == null || !string.IsNullOrWhiteSpace(local.WeekWindow.LimitId) && !string.Equals(local.WeekWindow.LimitId, "codex", StringComparison.OrdinalIgnoreCase))
			{
				Console.Error.WriteLine("Local fallback did not keep the primary Codex quota.");
				return 12;
			}

			reader.SnapshotChanged += delegate(object sender, UsageSnapshot update)
			{
				Interlocked.Increment(ref _updates);
				Print("UPDATE", update);
			};
			reader.StartWatching();
			Thread.Sleep(7500);
			UsageSnapshot refreshed = reader.ReadLatest();
			Print("REFRESHED", refreshed);
			if (!refreshed.IsLive || refreshed.WeekWindow == null)
			{
				Console.Error.WriteLine("Periodic live refresh did not remain available.");
				return 13;
			}
		}
		Console.WriteLine("PASS|updates=" + _updates);
		return 0;
	}

	private static int RunNotificationTest()
	{
		int pureTestResult = RunPureTests();
		if (pureTestResult != 0)
		{
			return pureTestResult;
		}
		string previousMode = Environment.GetEnvironmentVariable("CODEX_ORBIT_ONLY_RUNTIME");
		string previousPath = Environment.GetEnvironmentVariable("CODEX_ORBIT_CODEX_PATH");
		try
		{
			Environment.SetEnvironmentVariable("CODEX_ORBIT_ONLY_RUNTIME", "override");
			Environment.SetEnvironmentVariable("CODEX_ORBIT_CODEX_PATH", Process.GetCurrentProcess().MainModule.FileName);
			using ManualResetEventSlim updated = new ManualResetEventSlim(false);
			using (CodexUsageReader reader = new CodexUsageReader(CodexUsageReader.GetDefaultSessionsPath()))
			{
				reader.SnapshotChanged += delegate(object sender, UsageSnapshot snapshot)
				{
					if (snapshot != null && snapshot.IsLive && snapshot.WeekWindow != null && Math.Abs(snapshot.WeekWindow.UsedPercent - 30.0) < 0.001)
					{
						updated.Set();
					}
				};
				reader.StartWatching();
				UsageSnapshot initial = reader.ReadLatest();
				Print("FAKE_INITIAL", initial);
				if (!initial.IsLive || initial.WeekWindow == null || Math.Abs(initial.WeekWindow.UsedPercent - 25.0) > 0.001)
				{
					Console.Error.WriteLine("Fake initial rate-limit snapshot was not read.");
					return 20;
				}
				if (!CachedAccountIsMinimized(reader))
				{
					Console.Error.WriteLine("Account response retained fields that are not required by the widget.");
					return 22;
				}
				Stopwatch stopwatch = Stopwatch.StartNew();
				if (!updated.Wait(5000))
				{
					Console.Error.WriteLine("Rate-limit notification was not applied within five seconds.");
					FieldInfo appServerField = typeof(CodexUsageReader).GetField("_appServer", BindingFlags.Instance | BindingFlags.NonPublic);
					object appServer = appServerField?.GetValue(reader);
					FieldInfo errorField = appServer?.GetType().GetField("_lastErrorLine", BindingFlags.Instance | BindingFlags.NonPublic);
					FieldInfo protocolField = appServer?.GetType().GetField("_lastProtocolLine", BindingFlags.Instance | BindingFlags.NonPublic);
					Console.Error.WriteLine("Fake server stage: " + (errorField?.GetValue(appServer) ?? "none"));
					Console.Error.WriteLine("Last protocol line: " + (protocolField?.GetValue(appServer) ?? "none"));
					return 21;
				}
				Console.WriteLine("PASS|notification_ms=" + stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
				return 0;
			}
		}
		finally
		{
			Environment.SetEnvironmentVariable("CODEX_ORBIT_ONLY_RUNTIME", previousMode);
			Environment.SetEnvironmentVariable("CODEX_ORBIT_CODEX_PATH", previousPath);
		}
	}

	private static int RunPureTests()
	{
		DateTimeOffset observedAt = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
		JavaScriptSerializer json = new JavaScriptSerializer();
		string line = json.Serialize(new Dictionary<string, object>
		{
			{ "timestamp", observedAt.ToString("o", CultureInfo.InvariantCulture) },
			{ "type", "event_msg" },
			{
				"payload",
				new Dictionary<string, object>
				{
					{ "type", "token_count" },
					{
						"rate_limits",
						new Dictionary<string, object>
						{
							{ "plan_type", "plus" },
							{ "limit_id", "codex" },
							{
								"primary",
								new Dictionary<string, object>
								{
									{ "used_percent", 25.0 },
									{ "window_minutes", 300 },
									{ "resets_at", observedAt.AddHours(5.0).ToUnixTimeSeconds() }
								}
							},
							{
								"secondary",
								new Dictionary<string, object>
								{
									{ "used_percent", 60.0 },
									{ "window_minutes", 10080 },
									{ "resets_at", observedAt.AddDays(7.0).ToUnixTimeSeconds() }
								}
							}
						}
					}
				}
			}
		});
		using (CodexUsageReader reader = new CodexUsageReader(Path.Combine(Path.GetTempPath(), "codex-orbit-test-missing")))
		{
			IList<UsageWindowSnapshot> windows = reader.ParseLine(line, "fixture.jsonl");
			if (windows.Count != 2 || windows[0].WindowMinutes != 300 || Math.Abs(windows[0].RemainingPercent - 75.0) > 0.001 || windows[1].WindowMinutes != 10080)
			{
				Console.Error.WriteLine("Local snapshot parser regression.");
				return 30;
			}
			if (reader.ParseLine("{ malformed", "fixture.jsonl").Count != 0)
			{
				Console.Error.WriteLine("Malformed local snapshot was not rejected.");
				return 31;
			}
		}

		WindowDisplay danger = WindowDisplay.From(new UsageWindowSnapshot
		{
			WindowMinutes = 10080,
			UsedPercent = 92.0,
			ObservedAt = observedAt,
			ResetsAt = observedAt.AddDays(1.0),
			LimitId = "codex"
		}, observedAt);
		if (!danger.Exists || danger.Alert != AlertLevel.Danger || danger.RoundedPercent != 8)
		{
			Console.Error.WriteLine("Quota alert threshold regression.");
			return 32;
		}

		if (PlanInfo.ResolveKind("prolite", "auto") != PlanKind.Pro5x || PlanInfo.ResolveKind("pro", "auto") != PlanKind.Pro20x || PlanInfo.ResolveKind("ent26", "auto") != PlanKind.Enterprise)
		{
			Console.Error.WriteLine("Plan mapping regression.");
			return 33;
		}
		if (PlanInfo.ResolveKind("self_serve_business_prolite", "auto") != PlanKind.Business || PlanInfo.ResolveKind("self_serve_business_usage_based", "auto") != PlanKind.Business || PlanInfo.ResolveKind("enterprise_cbp_automation", "auto") != PlanKind.Enterprise || PlanInfo.ResolveKind("edu", "auto") != PlanKind.Enterprise)
		{
			Console.Error.WriteLine("Current App Server plan mapping regression.");
			return 36;
		}

		Type appType = typeof(CodexUsageReader).Assembly.GetType("CodexQuota.App");
		MethodInfo sanitizeDiagnostic = appType?.GetMethod("SanitizeDiagnostic", BindingFlags.Static | BindingFlags.NonPublic);
		string fakeToken = "github" + "_pat_" + new string('A', 24);
		string fakeApiKey = "sk-" + new string('B', 24);
		string privatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "private", "trace.txt");
		string privateLocalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "private", "trace.txt");
		string sanitized = (string)sanitizeDiagnostic?.Invoke(null, new object[] { privatePath + " " + privateLocalPath + " " + fakeToken + " " + fakeApiKey });
		string truncated = (string)sanitizeDiagnostic?.Invoke(null, new object[] { new string('X', 40000) });
		if (string.IsNullOrWhiteSpace(sanitized) || sanitized.IndexOf(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase) >= 0 || sanitized.IndexOf(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.OrdinalIgnoreCase) >= 0 || sanitized.Contains(fakeToken) || sanitized.Contains(fakeApiKey) || !sanitized.Contains("%USERPROFILE%") || !sanitized.Contains("%LOCALAPPDATA%") || !sanitized.Contains("[REDACTED_GITHUB_TOKEN]") || !sanitized.Contains("[REDACTED_API_KEY]") || string.IsNullOrWhiteSpace(truncated) || !truncated.EndsWith("[DIAGNOSTIC_TRUNCATED]", StringComparison.Ordinal) || truncated.Length > 32800)
		{
			Console.Error.WriteLine("Diagnostic redaction regression.");
			return 37;
		}

		long tracked = observedAt.AddHours(1.0).UtcTicks;
		long notified = 0L;
		UsageWindowSnapshot resetWindow = new UsageWindowSnapshot
		{
			ObservedAt = observedAt.AddHours(1.0),
			ResetsAt = observedAt.AddDays(7.0),
			LimitId = "codex"
		};
		if (!ResetNotifyTracker.Evaluate(resetWindow, observedAt.AddHours(1.0), ref tracked, notified, out var deadline, out var changed) || deadline != observedAt.AddHours(1.0).UtcTicks || !changed)
		{
			Console.Error.WriteLine("Reset notification tracker regression.");
			return 34;
		}

		Type placementType = typeof(CodexUsageReader).Assembly.GetType("CodexQuota.Services.WindowPlacement");
		MethodInfo resize = placementType?.GetMethod("ResizeKeepingNearestEdge", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		double resizedLeft = (double)resize.Invoke(null, new object[5] { 1666.0, 244.0, 356.0, 0.0, 1920.0 });
		if (Math.Abs(resizedLeft - 1554.0) > 0.001)
		{
			Console.Error.WriteLine("Mini-window edge anchoring regression.");
			return 35;
		}

		Console.WriteLine("PASS|pure");
		return 0;
	}

	private static bool CachedAccountIsMinimized(CodexUsageReader reader)
	{
		FieldInfo appServerField = typeof(CodexUsageReader).GetField("_appServer", BindingFlags.Instance | BindingFlags.NonPublic);
		object appServer = appServerField?.GetValue(reader);
		FieldInfo accountField = appServer?.GetType().GetField("_cachedAccountResponse", BindingFlags.Instance | BindingFlags.NonPublic);
		if (!(accountField?.GetValue(appServer) is IDictionary<string, object> response) || ContainsKey(response, "email"))
		{
			return false;
		}
		if (!(response.TryGetValue("result", out var resultValue) && resultValue is IDictionary<string, object> result && result.TryGetValue("account", out var accountValue) && accountValue is IDictionary<string, object> account))
		{
			return false;
		}
		return string.Equals(Convert.ToString(account["type"], CultureInfo.InvariantCulture), "chatgpt", StringComparison.Ordinal) && string.Equals(Convert.ToString(account["planType"], CultureInfo.InvariantCulture), "pro", StringComparison.Ordinal) && account.Count == 2 && result.Count == 2 && result.ContainsKey("requiresOpenaiAuth");
	}

	private static bool ContainsKey(object value, string key)
	{
		if (!(value is IDictionary<string, object> dictionary))
		{
			return false;
		}
		foreach (KeyValuePair<string, object> item in dictionary)
		{
			if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase) || ContainsKey(item.Value, key))
			{
				return true;
			}
		}
		return false;
	}

	private static int RunFakeAppServer()
	{
		JavaScriptSerializer json = new JavaScriptSerializer();
		bool notificationQueued = false;
		using StreamReader input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true);
		using StreamWriter output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
		{
			AutoFlush = true
		};
		Console.SetOut(output);
		string line;
		while ((line = input.ReadLine()) != null)
		{
			IDictionary<string, object> message;
			try
			{
				message = json.DeserializeObject(line) as IDictionary<string, object>;
			}
			catch (Exception exception)
			{
				StringBuilder prefix = new StringBuilder();
				for (int i = 0; i < Math.Min(12, line.Length); i++)
				{
					if (i > 0)
					{
						prefix.Append(',');
					}
					prefix.Append(((int)line[i]).ToString("X4", CultureInfo.InvariantCulture));
				}
				Console.Error.WriteLine("FAKE_SERVER_PARSE|" + exception.GetType().Name + "|length=" + line.Length.ToString(CultureInfo.InvariantCulture) + "|prefix=" + prefix);
				continue;
			}
			if (message == null || !message.TryGetValue("method", out var methodValue))
			{
				continue;
			}
			string method = Convert.ToString(methodValue, CultureInfo.InvariantCulture);
			if (!message.TryGetValue("id", out var id))
			{
				continue;
			}
			if (string.Equals(method, "initialize", StringComparison.Ordinal))
			{
				WriteFake(json, new Dictionary<string, object>
				{
					{ "id", id },
					{ "result", new Dictionary<string, object> { { "userAgent", "codex-orbit-test/1" } } }
				});
			}
			else if (string.Equals(method, "account/read", StringComparison.Ordinal))
			{
				WriteFake(json, new Dictionary<string, object>
				{
					{ "id", id },
					{
						"result",
						new Dictionary<string, object>
						{
							{ "requiresOpenaiAuth", true },
							{ "unusedMetadata", "discard-me" },
							{
								"account",
								new Dictionary<string, object>
								{
									{ "type", "chatgpt" },
									{ "planType", "pro" },
									{ "email", "private@example.invalid" }
								}
							}
						}
					}
				});
			}
			else if (string.Equals(method, "account/rateLimits/read", StringComparison.Ordinal))
			{
				WriteFake(json, CreateFakeRateResponse(id, 25.0));
				if (!notificationQueued)
				{
					notificationQueued = true;
					Console.Error.WriteLine("FAKE_SERVER_NOTIFY|before");
					WriteFake(json, CreateFakeRateNotification(30.0));
					Console.Error.WriteLine("FAKE_SERVER_NOTIFY|after");
				}
			}
		}
		return 0;
	}

	private static IDictionary<string, object> CreateFakeRateResponse(object id, double usedPercent)
	{
		return new Dictionary<string, object>
		{
			{ "id", id },
			{ "result", CreateFakeRatePayload(usedPercent) }
		};
	}

	private static IDictionary<string, object> CreateFakeRateNotification(double usedPercent)
	{
		return new Dictionary<string, object>
		{
			{ "method", "account/rateLimits/updated" },
			{
				"params",
				new Dictionary<string, object>
				{
					{
						"rateLimits",
						new Dictionary<string, object>
						{
							{
								"primary",
								new Dictionary<string, object>
								{
									{ "usedPercent", usedPercent }
								}
							}
						}
					}
				}
			}
		};
	}

	private static IDictionary<string, object> CreateFakeRatePayload(double usedPercent)
	{
		return new Dictionary<string, object>
		{
			{
				"rateLimits",
				new Dictionary<string, object>
				{
					{ "limitId", "codex" },
					{
						"primary",
						new Dictionary<string, object>
						{
							{ "usedPercent", usedPercent },
							{ "windowDurationMins", 10080 },
							{ "resetsAt", DateTimeOffset.UtcNow.AddDays(7.0).ToUnixTimeSeconds() }
						}
					}
				}
			}
		};
	}

	private static void WriteFake(JavaScriptSerializer json, IDictionary<string, object> message)
	{
		lock (FakeOutputLock)
		{
			Console.WriteLine(json.Serialize(message));
			Console.Out.Flush();
		}
	}
}
