#pragma warning disable IDE0034
using TimelineProperties = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionTimelineProperties;
using MediaProperties = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties;
using PlaybackStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;
using PlaybackInfo = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackInfo;
using SessionManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using AutoRepeatMode = Windows.Media.MediaPlaybackAutoRepeatMode;
using System.Collections.Generic;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using Windows.Media.Control;
using System.Threading;
using System.Drawing;
using System.Linq;
using System.IO;
using System;

namespace WNPReduxAdapterLibrary
{
  public static class Extensions
  {
    public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
    {
      if (dictionary.TryGetValue(key, out TValue value))
      {
        return value;
      }
      else
      {
        return defaultValue;
      }
    }
  }
  public class WNPReduxNative
  {
    private static bool isStarted = false;
    private static int port = 0;
    static SessionManager manager = null;
    static int lastPositionSeconds = 0;
    private static Timer optimisticUpdateTimer;
    private static readonly object timerLock = new object();
    static readonly string idPrefix = "WNPReduxNativeWindows_";
    static readonly Dictionary<string, Session> sessions = new Dictionary<string, Session>();
    static readonly bool isWindows10 = Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Build >= 19041 && Environment.OSVersion.Version.Build < 22000;

    public static void Start(int port)
    {
      if (isStarted) return;
      isStarted = true;
      WNPReduxNative.port = port;
      WNPHttpServer.OnMessageHook += OnMessageHook;
      new Thread(StartWindowsAPIThreaded).Start();
      optimisticUpdateTimer = new Timer(PerformOptimisticUpdate, null, Timeout.Infinite, Timeout.Infinite);
    }

    public static void Stop()
    {
      if (!isStarted) return;
      isStarted = false;
      WNPHttpServer.OnMessageHook -= OnMessageHook;
      optimisticUpdateTimer.Dispose();
      optimisticUpdateTimer = null;
      if (manager != null)
      {
        manager.SessionsChanged -= SessionsChanged_Native;
        manager = null;
      }
      foreach (Session session in sessions.Values)
      {
        session.MediaPropertiesChanged -= MediaPropertiesChanged_Native;
        session.PlaybackInfoChanged -= PlaybackInfoChanged_Native;
        session.TimelinePropertiesChanged -= TimelinePropertiesChanged_Native;
      }
      sessions.Clear();
    }

    private static void PerformOptimisticUpdate(object state)
    {
      if (!isStarted) return;

      lock (timerLock)
      {
        if (WNPRedux.MediaInfo._ID.StartsWith(idPrefix) && WNPRedux.MediaInfo.State == MediaInfo.StateMode.PLAYING)
        {
          WNPRedux.MediaInfo.PositionSeconds = (++lastPositionSeconds);
          WNPRedux.MediaInfo.Position = WNPRedux.TimeInSecondsToString(WNPRedux.MediaInfo.PositionSeconds);

          if (WNPRedux.MediaInfo.DurationSeconds > 0)
          {
            WNPRedux.MediaInfo.PositionPercent = ((double)WNPRedux.MediaInfo.PositionSeconds / WNPRedux.MediaInfo.DurationSeconds) * 100.0;
          }
          else
          {
            WNPRedux.MediaInfo.PositionPercent = 100;
          }
        }
      }
    }

    private static MediaInfo GetMediaInfo(string app_id)
    {
      string id = idPrefix + app_id;
      MediaInfo mediaInfo = WNPRedux.GetMediaInfo(id);
      mediaInfo.PlayerName = "Windows Media Session";
      mediaInfo.IsNative = true;
      return mediaInfo;
    }

    static async void OnMessageHook(string message)
    {
      if (!WNPRedux.MediaInfo._ID.StartsWith(idPrefix)) return;
      try
      {
        MediaInfo mediaInfo = GetMediaInfo(WNPRedux.MediaInfo._ID.Replace(idPrefix, ""));
        sessions.TryGetValue(mediaInfo._ID.Replace(idPrefix, ""), out Session session);
        if (session == null) return;

        string[] parts = message.ToUpper().Split(' ');
        string type = parts[0];
        string data = parts.Length > 1 ? parts[1] : "";

        switch (type)
        {
          case "TRY_SET_STATE":
            {
              if (data == "PLAYING") await session.TryPlayAsync();
              else await session.TryPauseAsync();
              break;
            }
          case "TRY_SKIP_PREVIOUS": await session.TrySkipPreviousAsync(); break;
          case "TRY_SKIP_NEXT": await session.TrySkipNextAsync(); break;
          case "TRY_SET_POSITION": await session.TryChangePlaybackPositionAsync(Convert.ToInt64(data.Split(':').First()) * 10_000_000); break;
          case "TRY_SET_VOLUME": break;
          case "TRY_TOGGLE_REPEAT_MODE": await session.TryChangeAutoRepeatModeAsync(mediaInfo.RepeatMode == MediaInfo.RepeatModeEnum.NONE ? AutoRepeatMode.List : mediaInfo.RepeatMode == MediaInfo.RepeatModeEnum.ALL ? AutoRepeatMode.Track : AutoRepeatMode.None); break;
          case "TRY_TOGGLE_SHUFFLE_ACTIVE": await session.TryChangeShuffleActiveAsync(!mediaInfo.ShuffleActive); break;
          case "TRY_SET_RATING": break;
        }
      }
      catch (Exception ex)
      {
        WNPRedux.Log(LogType.Error, "WNPNativeWindows - Failed to execute event");
        WNPRedux.Log(LogType.Debug, $"WNPNativeWindows - Error Trace: {ex}");
      }
    }

    static async void StartWindowsAPIThreaded()
    {
      manager = await SessionManager.RequestAsync();
      SessionsChanged_Native(manager);
      manager.SessionsChanged += SessionsChanged_Native;
    }

    static void SessionsChanged_Native(SessionManager manager, SessionsChangedEventArgs e = null)
    {
      foreach (Session session in sessions.Values)
      {
        session.MediaPropertiesChanged -= MediaPropertiesChanged_Native;
        session.PlaybackInfoChanged -= PlaybackInfoChanged_Native;
        session.TimelinePropertiesChanged -= TimelinePropertiesChanged_Native;
      }
      sessions.Clear();

      IReadOnlyList<Session> _sessions = manager.GetSessions();
      List<string> newSessions = new List<string>();
      foreach (Session session in _sessions)
      {
        if (IsAppIdBlacklisted(session.SourceAppUserModelId)) continue;
        MediaPropertiesChanged_Native(session);
        PlaybackInfoChanged_Native(session);
        TimelinePropertiesChanged_Native(session);
        session.MediaPropertiesChanged += MediaPropertiesChanged_Native;
        session.PlaybackInfoChanged += PlaybackInfoChanged_Native;
        session.TimelinePropertiesChanged += TimelinePropertiesChanged_Native;
        sessions.Add(session.SourceAppUserModelId, session);
        newSessions.Add(session.SourceAppUserModelId);
      }

      List<string> keysToRemove = new List<string>();
      foreach (KeyValuePair<string, MediaInfo> entry in WNPRedux.mediaInfoDictionary)
      {
        if (entry.Key.StartsWith(idPrefix))
        {
          if (!newSessions.Contains(entry.Key.Replace(idPrefix, "")))
          {
            keysToRemove.Add(entry.Key);
          }
        }
      }
      foreach (string key in keysToRemove)
      {
        WNPRedux.mediaInfoDictionary.TryRemove(key, out _);
      }
      WNPRedux.UpdateMediaInfo();
    }

    static bool IsAppIdBlacklisted(string appId)
    {
      // Finding the AppID of apps is pretty simple, just run
      // `Get-StartApps | Where { $_.Name -eq "Firefox" }`

      string id = appId.ToLower();
      if (id.Contains("chrome")) return true;
      else if (id.Contains("chromium")) return true;
      else if (id.Contains("msedge")) return true;
      else if (id.Contains("opera")) return true;
      else if (id.Contains("brave")) return true;
      else if (id.Contains("vivaldi")) return true;
      else if (id.Contains("308046B0AF4A39CB".ToLower())) return true; // firefox
      else if (id.Contains("6F193CCC56814779".ToLower())) return true; // firefox nightly
      else if (id.Contains("6F940AC27A98DD61".ToLower())) return true; // waterfox
      else if (id.Contains("A3665BA0C7D475A".ToLower())) return true; // pale moon

      return false;
    }

    static async void MediaPropertiesChanged_Native(Session session, MediaPropertiesChangedEventArgs e = null)
    {
      // Sometimes, we get "The device is not ready". This can easily be replicated by opening foobar2000 while this is running.
      try
      {
        MediaProperties info = await session.TryGetMediaPropertiesAsync();
        string CoverUrl = await WriteThumbnail(info.Thumbnail, session.SourceAppUserModelId);
        MediaInfo mediaInfo = GetMediaInfo(session.SourceAppUserModelId);
        mediaInfo.CoverUrl = CoverUrl;
        mediaInfo.Title = info.Title;
        mediaInfo.Artist = info.Artist;
        mediaInfo.Album = info.AlbumTitle;
        WNPRedux.UpdateMediaInfo();
      }
      catch
      {

      }
    }

    static async Task<string> WriteThumbnail(IRandomAccessStreamReference thumbnail, string appId)
    {
      if (thumbnail == null) return "";
      try
      {
        bool cropImage = isWindows10 && appId.ToLower().Contains("spotify");

        var stream = await thumbnail.OpenReadAsync();
        var bytes = new byte[stream.Size];
        var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(bytes);
        MemoryStream mStream = new MemoryStream(bytes);

        string folderPath = WNPRedux.GetWnpPath();
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        string coverPath = Path.Combine(folderPath, $"cover-{port}.jpg");
        string croppedCoverPath = Path.Combine(folderPath, $"cover-{port}-temp.jpg");

        File.WriteAllBytes(cropImage ? croppedCoverPath : coverPath, mStream.ToArray());
        reader.DetachStream();
        await stream.FlushAsync();

        if (cropImage)
        {
          CropCover(croppedCoverPath, coverPath);
        }

        return $"http://127.0.0.1:{port}/cover?name=cover-{port}.jpg&r={new Random().Next(999999)}";
      }
      catch
      {
        return "";
      }
    }

    private static void CropCover(string path, string savePath)
    {
      using (var b = new Bitmap(300, 300))
      {
        using (var g = Graphics.FromImage(b))
        {
          using (var cover = new Bitmap(path))
          {
            var r = new Rectangle(0, 0, 300, 300);
            g.DrawImage(cover, r, 33, 0, 233, 233, GraphicsUnit.Pixel);
          }
        }

        b.Save(savePath, ImageFormat.Jpeg);
      }
    }

    static void PlaybackInfoChanged_Native(Session session, PlaybackInfoChangedEventArgs e = null)
    {
      PlaybackInfo info = session.GetPlaybackInfo();
      MediaInfo mediaInfo = GetMediaInfo(session.SourceAppUserModelId);

      string controls = "{";
      controls += $"\"supports_play_pause\": {info.Controls.IsPlayPauseToggleEnabled.ToString().ToLower()},";
      controls += $"\"supports_skip_previous\": {info.Controls.IsPreviousEnabled.ToString().ToLower()},";
      controls += $"\"supports_skip_next\": {info.Controls.IsNextEnabled.ToString().ToLower()},";
      controls += $"\"supports_set_position\": {info.Controls.IsPlaybackPositionEnabled.ToString().ToLower()},";
      controls += $"\"supports_set_volume\": false,";
      controls += $"\"supports_toggle_repeat_mode\": {info.Controls.IsRepeatEnabled.ToString().ToLower()},";
      controls += $"\"supports_toggle_shuffle_active\": {info.Controls.IsShuffleEnabled.ToString().ToLower()},";
      controls += $"\"supports_set_rating\": false,";
      controls += $"\"rating_system\": \"NONE\"";
      controls += "}";

      mediaInfo.Controls = MediaControls.FromJson(controls);
      mediaInfo.State = info.PlaybackStatus == PlaybackStatus.Playing ? MediaInfo.StateMode.PLAYING : MediaInfo.StateMode.PAUSED;
      mediaInfo.ShuffleActive = info.IsShuffleActive ?? false;
      mediaInfo.RepeatMode = info.AutoRepeatMode == null || info.AutoRepeatMode.Value == AutoRepeatMode.None ? MediaInfo.RepeatModeEnum.NONE : info.AutoRepeatMode.Value == AutoRepeatMode.Track ? MediaInfo.RepeatModeEnum.ONE : MediaInfo.RepeatModeEnum.ALL;
      WNPRedux.UpdateMediaInfo();
    }

    static void TimelinePropertiesChanged_Native(Session session, TimelinePropertiesChangedEventArgs e = null)
    {
      TimelineProperties info = session.GetTimelineProperties();
      MediaInfo mediaInfo = GetMediaInfo(session.SourceAppUserModelId);

      mediaInfo.DurationSeconds = Convert.ToInt32(info.EndTime.TotalSeconds);
      mediaInfo.Duration = WNPRedux.TimeInSecondsToString(mediaInfo.DurationSeconds);

      if (lastPositionSeconds == Convert.ToInt32(info.Position.TotalSeconds)) return;

      lastPositionSeconds = Convert.ToInt32(info.Position.TotalSeconds);
      mediaInfo.PositionSeconds = lastPositionSeconds;
      mediaInfo.Position = WNPRedux.TimeInSecondsToString(mediaInfo.PositionSeconds);

      if (mediaInfo.DurationSeconds > 0)
      {
        mediaInfo.PositionPercent = ((double)mediaInfo.PositionSeconds / mediaInfo.DurationSeconds) * 100.0;
      }
      else
      {
        mediaInfo.PositionPercent = 100;
      }
      WNPRedux.UpdateMediaInfo();

      optimisticUpdateTimer.Change(1000, 1000);
    }
  }
}
#pragma warning restore IDE0034
