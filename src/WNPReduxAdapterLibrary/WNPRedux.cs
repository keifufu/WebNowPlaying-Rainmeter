#pragma warning disable IDE1006
#pragma warning disable CS1591
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System;

namespace WNPReduxAdapterLibrary
{
  public enum LogType { Debug, Warning, Error };
  public class WNPRedux
  {
    internal static int port = 0;
    private static bool _isStarted = false;
    /// <summary>
    /// Whether WNPRedux is started or not
    /// </summary>
    public static bool isStarted { get { return _isStarted; } }
    /// <summary>
    /// Info about the currently playing media.
    /// </summary>
    public static MediaInfo MediaInfo = new MediaInfo();
    /// <summary>
    /// Number of connected clients.
    /// </summary>
    public static int clients = 0;

    internal static bool _isUsingNativeAPIs = true;
    /// <summary>
    /// Whether WNPRedux is pulling info from native APIs.  
    /// This is read-only, the actual value is set by the user.  
    /// It's toggleable through the browser extensions settings panel.
    /// As of 2.0.1 it's enabled by default.
    /// Read more about it here: https://github.com/keifufu/WebNowPlaying-Redux/blob/main/NativeAPIs.md
    /// </summary>
    public static bool isUsingNativeAPIs { get { return _isUsingNativeAPIs; } }

    internal static string GetWnpPath()
    {
      // Note: I was going to clean up the old unused folder here but that might conflict with older WNP adapters, so I won't.
      if (Environment.OSVersion.Platform == PlatformID.Win32NT)
      {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebNowPlaying");
      }
      else
      {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "WebNowPlaying");
      }
    }

    internal static readonly string disableNativeAPIsPath = Path.Combine(GetWnpPath(), "disable_native_apis");

    public static readonly ConcurrentDictionary<string, MediaInfo>
        mediaInfoDictionary = new ConcurrentDictionary<string, MediaInfo>();

    private static Action<LogType, string> _logger;
    private static bool _throttleLogs = false;
    internal static string adapterVersion = "";
    /// <summary>
    /// Starts WNP if it isn't already started.
    /// <returns>No return type</returns>
    /// <example>
    /// <code>
    /// void Logger(WNPRedux.LogType type, string message) {
    ///   Console.WriteLine($"{type}: {message}");
    /// }
    /// WNPRedux.Initialize(1234, "1.0.0", logger);
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="port">Port</param>
    /// <param name="version">Adapter Version (major.minor.patch)</param>
    /// <param name="logger">Custom logger</param>
    /// <param name="throttleLogs">Prevent the same log message being logged more than once per 30 seconds</param>
    public static void Start(int port, string version, Action<LogType, string> logger, bool throttleLogs = false)
    {
      try
      {
        if (_isStarted) return;
        _isStarted = true;
        WNPRedux.port = port;
        _logger = logger;
        adapterVersion = version;
        _throttleLogs = throttleLogs;
        _isUsingNativeAPIs = !Directory.Exists(disableNativeAPIsPath);
        MediaInfo = new MediaInfo();

        WNPHttpServer.Start(port);
      }
      catch (Exception e)
      {
        Log(LogType.Error, "WNPRedux - Failed to open WebSocket");
        Log(LogType.Debug, $"WNPRedux - Error Trace: {e}");
        Stop();
      }
    }

    /// <summary>
    /// Stops WNP if it's started.
    /// </summary>
    public static void Stop()
    {
      if (!_isStarted) return;
      _isStarted = false;
      WNPHttpServer.Stop();
      MediaInfo = new MediaInfo();
      clients = 0;
    }

    private static readonly object _lock = new object();
    private static readonly Dictionary<string, int> _logCounts = new Dictionary<string, int>();
    private static readonly int _maxLogCount = 1;
    private static readonly TimeSpan _logResetTime = TimeSpan.FromSeconds(30);
    public static void Log(LogType type, string message)
    {
      if (!_throttleLogs)
      {
        _logger(type, message);
        return;
      }

      lock (_lock)
      {
        DateTime currentTime = DateTime.Now;
        string typeMessage = $"{type} {message}";

        if (!_logCounts.TryGetValue(typeMessage, out int logCount)) logCount = 0;

        if (logCount < _maxLogCount)
        {
          _logger(type, message);
          _logCounts[typeMessage] = logCount + 1;
          Task.Run(async () =>
          {
            await Task.Delay(_logResetTime);
            lock (_lock)
            {
              _logCounts.Remove(typeMessage);
            }
          });
        }
      }
    }

    public static MediaInfo GetMediaInfo(string id)
    {
      MediaInfo currentMediaInfo = mediaInfoDictionary.GetOrAdd(id, new MediaInfo());
      currentMediaInfo._ID = id;
      return currentMediaInfo;
    }

    public static void UpdateMediaInfo()
    {
      try
      {
        var iterableDictionary = mediaInfoDictionary
           .Where(kv => !kv.Value.IsNative || isUsingNativeAPIs)
           .OrderByDescending(kv => kv.Value.Timestamp);
        bool suitableMatch = false;

        foreach (KeyValuePair<string, MediaInfo> item in iterableDictionary)
        {
          // No need to check title since timestamp is only set when title is set
          if (item.Value.State == MediaInfo.StateMode.PLAYING && item.Value.Volume > 0)
          {
            MediaInfo = item.Value;
            suitableMatch = true;
            // If match found break early which should be always very early
            break;
          }
        }

        if (!suitableMatch)
        {
          MediaInfo fallbackInfo = iterableDictionary.FirstOrDefault().Value ?? new MediaInfo();
          MediaInfo = fallbackInfo;
        }
      }
      catch (Exception e)
      {
        Log(LogType.Error, "WNPRedux - Error finding new media info to display");
        Log(LogType.Debug, $"WNPRedux - Error Trace: {e}");
      }
    }

    private static string Pad(int num, int size)
    {
      return num.ToString().PadLeft(size, '0');
    }
    public static string TimeInSecondsToString(int timeInSeconds)
    {
      try
      {
        int timeInMinutes = (int)Math.Floor((double)timeInSeconds / 60);
        if (timeInMinutes < 60)
          return timeInMinutes.ToString() + ":" + Pad((timeInSeconds % 60), 2).ToString();

        return Math.Floor((double)timeInMinutes / 60).ToString() + ":" + Pad((timeInMinutes % 60), 2).ToString() + ":" + Pad((timeInSeconds % 60), 2).ToString();
      }
      catch
      {
        return "0:00";
      }
    }
  }
}

#pragma warning restore CS1591
#pragma warning restore IDE1006
