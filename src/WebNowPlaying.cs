using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Collections.Generic;
using WNPReduxAdapterLibrary;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Net.Http;
using System.Linq;
using System.Net;
using Rainmeter;
using System.IO;
using System;

namespace WebNowPlaying
{
  internal class Measure
  {
    enum PlayerTypes
    {
      Status,
      Player,
      Title,
      Artist,
      Album,
      Cover,
      CoverWebAddress,
      Duration,
      Position,
      Remaining,
      Progress,
      Volume,
      State,
      Rating,
      Repeat,
      Shuffle,
      SupportsPlayPause,
      SupportsSkipPrevious,
      SupportsSkipNext,
      SupportsSetPosition,
      SupportsSetVolume,
      SupportsToggleRepeatMode,
      SupportsToggleShuffleActive,
      SupportsSetRating,
      RatingSystem,
      IsUsingNativeAPIs
    }

    private static readonly string DefaultCoverOutputLocation = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Temp\\Rainmeter\\WebNowPlaying\\cover.png";
    private string CoverDefaultLocation = "";
    private static string LastDownloadedCoverUrl = "";
    private static string LastFailedCoverUrl = "";
    private PlayerTypes playerType = PlayerTypes.Status;
    private static readonly ConcurrentDictionary<string, int> CoverPathDictionary = new ConcurrentDictionary<string, int>() { [DefaultCoverOutputLocation] = 1 };
    private string _CoverPath = "";

    public static void DownloadCoverImage()
    {
      if (isInThread) return;
      string CoverUrl = WNPRedux.MediaInfo.CoverUrl;
      if (CoverUrl.Length == 0 || LastFailedCoverUrl == CoverUrl || LastDownloadedCoverUrl == CoverUrl || !Uri.IsWellFormedUriString(CoverUrl, UriKind.Absolute)) return;

      isInThread = true;
      new Thread(() => DownloadImageThread(CoverUrl)).Start();
    }

    private static readonly object _lock = new object();
    private static bool isInThread = false;
    private static void DownloadImageThread(string CoverUrl)
    {
      lock (_lock)
      {
        try
        {
          void SaveToFiles(HttpResponseMessage response)
          {
            using (Stream inputStream = response.Content.ReadAsStreamAsync().Result)
            {
              // Write to all cover paths for backwards compatibility, this also includes the default path
              foreach (KeyValuePair<string, int> entry in CoverPathDictionary)
              {
                if (entry.Value > 0)
                {
                  int retryCount = 0;
                  while (true)
                  {
                    try
                    {
                      // Make sure the path exists
                      Directory.CreateDirectory(entry.Key.Substring(0, entry.Key.LastIndexOf("\\")));
                      using (Stream fileStream = File.Open(entry.Key, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                      {
                        // WHY WAS THIS GONE? WHO KNOWS! MUST'VE BEEN BROKEN FOR WEEKS!
                        inputStream.Position = 0;
                        inputStream.CopyTo(fileStream);
                      }
                      break;
                    }
                    catch (IOException)
                    {
                      // If the file is in use, wait a bit and retry
                      if (++retryCount > 10)
                      {
                        throw;
                      }
                      Thread.Sleep(100);
                    }
                  }
                }
              }
            }
          }

          ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, ssl) => true;
          ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;

          using (var httpClientHandler = new HttpClientHandler())
          {
            httpClientHandler.AllowAutoRedirect = true;
            httpClientHandler.MaxAutomaticRedirections = 3;
            using (var httpClient = new HttpClient(httpClientHandler))
            {
              HttpResponseMessage response = httpClient.GetAsync(CoverUrl).Result;

              if (response.StatusCode == HttpStatusCode.OK)
              {
                SaveToFiles(response);
                LastDownloadedCoverUrl = CoverUrl;
              }
              else if (response.StatusCode == (HttpStatusCode)308)
              {
                string redirectUrl = response.Headers.Location.ToString();
                response = httpClient.GetAsync(redirectUrl).Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                  SaveToFiles(response);
                  LastDownloadedCoverUrl = CoverUrl;
                }
                else
                {
                  LastFailedCoverUrl = CoverUrl;
                  WNPRedux.Log(LogType.Error, $"WebNowPlaying.dll - Unable to get album art from: {CoverUrl}. Response status code: {response.StatusCode}");
                }
              }
              else
              {
                LastFailedCoverUrl = CoverUrl;
                WNPRedux.Log(LogType.Error, $"WebNowPlaying.dll - Unable to get album art from: {CoverUrl}. Response status code: {response.StatusCode}");
              }
            }
          }
        }
        catch (Exception e)
        {
          LastFailedCoverUrl = CoverUrl;
          WNPRedux.Log(LogType.Error, $"WebNowPlaying.dll - Unexpected error downloading {CoverUrl}");
          WNPRedux.Log(LogType.Debug, $"WebNowPlaying.dll - Error Trace: {e}");
        }
        isInThread = false;
      }
    }

    private static int GetRemainingSeconds()
    {
      int remainingSeconds = WNPRedux.MediaInfo.DurationSeconds - WNPRedux.MediaInfo.PositionSeconds;
      return remainingSeconds;
    }

    private static string GetRemaining()
    {
      int remainingSeconds = GetRemainingSeconds();
      TimeSpan remainingTime = TimeSpan.FromSeconds(remainingSeconds);

      string formattedTime;
      if (remainingTime.TotalHours < 1) formattedTime = remainingTime.ToString(@"mm\:ss");
      else formattedTime = remainingTime.ToString(@"hh\:mm\:ss");
      return formattedTime;
    }

    static int MeasureCount = 0;
    internal Measure(API api)
    {
      ++MeasureCount;
      try
      {
        Assembly assembly = Assembly.GetExecutingAssembly();
        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
        string adapterVersion = fvi.FileVersion;

        void logger(LogType type, string message)
        {
          switch (type)
          {
            case LogType.Debug:
              api.Log(API.LogType.Debug, message); break;
            case LogType.Warning:
              api.Log(API.LogType.Warning, message); break;
            case LogType.Error:
              api.Log(API.LogType.Error, message); break;
          }
        }

        WNPRedux.Start(8974, adapterVersion, logger, false);
        WNPReduxNative.Start(8974);
      }
      catch (Exception e)
      {
        WNPRedux.Log(LogType.Error, "WebNowPlaying.dll - Error initializing WNPRedux");
        WNPRedux.Log(LogType.Debug, $"WebNowPlaying.dll - Error Trace: {e}");
      }
    }

    internal virtual void Dispose()
    {
      if (_CoverPath.Length > 0)
      {
        CoverPathDictionary.AddOrUpdate(_CoverPath, 0, (key, oldValue) => oldValue - 1);
      }
      --MeasureCount;
      if (MeasureCount == 0)
      {
        WNPRedux.Stop();
        WNPReduxNative.Stop();
      }
    }

    internal virtual void Reload(API api, ref double maxValue)
    {
      string playerTypeString = api.ReadString("PlayerType", "Status");
      try
      {
        playerType = (PlayerTypes)Enum.Parse(typeof(PlayerTypes), playerTypeString, true);

        if (playerType == PlayerTypes.Cover)
        {
          string DefaultPath = api.ReadPath("DefaultPath", "");
          if (DefaultPath.Length > 0) CoverDefaultLocation = DefaultPath;

          if (_CoverPath.Length == 0)
          {
            string CoverPath = api.ReadPath("CoverPath", "");
            if (CoverPath.Length > 0)
            {
              CoverPathDictionary.AddOrUpdate(CoverPath, 1, (key, oldValue) => oldValue + 1);
              _CoverPath = CoverPath;
            }
          }
        }
        else if (playerType == PlayerTypes.Progress || playerType == PlayerTypes.Volume)
        {
          maxValue = 100;
        }
      }
      catch (Exception e)
      {
        WNPRedux.Log(LogType.Error, "WebNowPlaying.dll - Unknown PlayerType:" + playerTypeString);
        WNPRedux.Log(LogType.Debug, $"WebNowPlaying.dll - Error Trace: {e}");
        playerType = PlayerTypes.Status;
      }
    }

    internal void ExecuteBang(string args)
    {
      try
      {
        string bang = args.ToLowerInvariant();

        if (bang.Equals("playpause")) WNPRedux.MediaInfo.Controls.TryTogglePlayPause();
        else if (bang.Equals("next")) WNPRedux.MediaInfo.Controls.TrySkipNext();
        else if (bang.Equals("previous")) WNPRedux.MediaInfo.Controls.TrySkipPrevious();
        else if (bang.Equals("repeat")) WNPRedux.MediaInfo.Controls.TryToggleRepeat();
        else if (bang.Equals("shuffle")) WNPRedux.MediaInfo.Controls.TryToggleShuffleActive();
        else if (bang.Equals("togglethumbsup")) WNPRedux.MediaInfo.Controls.TrySetRating(WNPRedux.MediaInfo.Rating == 5 ? 0 : 5);
        else if (bang.Equals("togglethumbsdown")) WNPRedux.MediaInfo.Controls.TrySetRating(WNPRedux.MediaInfo.Rating == 1 ? 0 : 1);
        else if (bang.Contains("rating "))
        {
          try
          {
            WNPRedux.MediaInfo.Controls.TrySetRating(Convert.ToInt16(bang.Substring(bang.LastIndexOf(" ") + 1)));
          }
          catch (Exception e)
          {
            WNPRedux.Log(LogType.Error, $"WebNowPlaying.dll - Rating argument could not be converted to an integer: {args}");
            WNPRedux.Log(LogType.Debug, $"WebNowPlaying.dll - Error Trace: {e}");
          }
        }
        else if (bang.Contains("setposition "))
        {
          try
          {
            if (bang.Contains("+"))
            {
              double percent = Convert.ToDouble(bang.Substring(bang.IndexOf("+") + 1), CultureInfo.InvariantCulture);
              WNPRedux.MediaInfo.Controls.TryForwardPositionPercent(percent);
            }
            else if (bang.Contains("-"))
            {
              double percent = Convert.ToDouble(bang.Substring(bang.IndexOf("-") + 1), CultureInfo.InvariantCulture);
              WNPRedux.MediaInfo.Controls.TryRevertPositionPercent(percent);
            }
            else
            {
              double percent = Convert.ToDouble(bang.Substring(bang.IndexOf("setposition ") + 12), CultureInfo.InvariantCulture);
              WNPRedux.MediaInfo.Controls.TrySetPositionPercent(percent);
            }
          }
          catch (Exception e)
          {
            WNPRedux.Log(LogType.Error, $"WebNowPlaying.dll - SetPosition argument could not be converted to a double: {args}");
            WNPRedux.Log(LogType.Debug, $"WebNowPlaying.dll - Error Trace: {e}");
          }
        }
        else if (bang.Contains("setvolume "))
        {
          try
          {
            if (bang.Contains('+'))
            {
              int volume = Convert.ToInt32(bang.Substring(bang.IndexOf("+") + 1));
              WNPRedux.MediaInfo.Controls.TrySetVolume(WNPRedux.MediaInfo.Volume + volume);
            }
            else if (bang.Contains("-"))
            {
              int volume = Convert.ToInt32(bang.Substring(bang.IndexOf("-") + 1));
              WNPRedux.MediaInfo.Controls.TrySetVolume(WNPRedux.MediaInfo.Volume - volume);
            }
            else
            {
              int volume = Convert.ToInt32(bang.Substring(bang.IndexOf(" ") + 1));
              WNPRedux.MediaInfo.Controls.TrySetVolume(volume);
            }
          }
          catch (Exception e)
          {
            WNPRedux.Log(LogType.Error, $"WebNowPlaying.dll - SetVolume argument could not be converted to a double: {args}");
            WNPRedux.Log(LogType.Debug, $"WebNowPlaying.dll - Error Trace: {e}");
          }
        }
        else
        {
          WNPRedux.Log(LogType.Warning, $"WebNowPlaying.dll - Unknown bang: {args}");
        }
      }
      catch (Exception e)
      {
        WNPRedux.Log(LogType.Error, $"WebNowPlaying.dll - Error using bang: {args}");
        WNPRedux.Log(LogType.Debug, $"WebNowPlaying.dll - Error Trace: {e}");
      }
    }

    internal virtual double Update()
    {
      DownloadCoverImage();

      try
      {
        switch (playerType)
        {
          case PlayerTypes.State:
            switch (WNPRedux.MediaInfo.State)
            {
              case MediaInfo.StateMode.STOPPED: return 0;
              case MediaInfo.StateMode.PLAYING: return 1;
              case MediaInfo.StateMode.PAUSED: return 2;
            }
            break;
          case PlayerTypes.Status:
            return WNPRedux.MediaInfo._ID.Length > 0 ? 1 : 0;
          case PlayerTypes.Volume:
            return WNPRedux.MediaInfo.Volume;
          case PlayerTypes.Rating:
            return WNPRedux.MediaInfo.Rating;
          case PlayerTypes.Repeat:
            switch (WNPRedux.MediaInfo.RepeatMode)
            {
              case MediaInfo.RepeatModeEnum.NONE: return 0;
              case MediaInfo.RepeatModeEnum.ONE: return 1;
              case MediaInfo.RepeatModeEnum.ALL: return 2;
            }
            break;
          case PlayerTypes.Shuffle:
            return WNPRedux.MediaInfo.ShuffleActive ? 1 : 0;
          case PlayerTypes.Progress:
            return WNPRedux.MediaInfo.PositionPercent;
          case PlayerTypes.Position:
            return WNPRedux.MediaInfo.PositionSeconds;
          case PlayerTypes.Duration:
            return WNPRedux.MediaInfo.DurationSeconds;
          case PlayerTypes.Remaining:
            return GetRemainingSeconds();
          case PlayerTypes.SupportsPlayPause:
            return WNPRedux.MediaInfo.Controls.SupportsPlayPause ? 1 : 0;
          case PlayerTypes.SupportsSkipPrevious:
            return WNPRedux.MediaInfo.Controls.SupportsSkipPrevious ? 1 : 0;
          case PlayerTypes.SupportsSkipNext:
            return WNPRedux.MediaInfo.Controls.SupportsSkipNext ? 1 : 0;
          case PlayerTypes.SupportsSetPosition:
            return WNPRedux.MediaInfo.Controls.SupportsSetPosition ? 1 : 0;
          case PlayerTypes.SupportsSetVolume:
            return WNPRedux.MediaInfo.Controls.SupportsSetVolume ? 1 : 0;
          case PlayerTypes.SupportsToggleRepeatMode:
            return WNPRedux.MediaInfo.Controls.SupportsToggleRepeatMode ? 1 : 0;
          case PlayerTypes.SupportsToggleShuffleActive:
            return WNPRedux.MediaInfo.Controls.SupportsToggleShuffleActive ? 1 : 0;
          case PlayerTypes.SupportsSetRating:
            return WNPRedux.MediaInfo.Controls.SupportsSetRating ? 1 : 0;
          case PlayerTypes.RatingSystem:
            return WNPRedux.MediaInfo.Controls.RatingSystem == MediaControls.RatingSystemEnum.NONE ? 0
              : WNPRedux.MediaInfo.Controls.RatingSystem == MediaControls.RatingSystemEnum.LIKE ? 1
              : WNPRedux.MediaInfo.Controls.RatingSystem == MediaControls.RatingSystemEnum.LIKE_DISLIKE ? 2
              : WNPRedux.MediaInfo.Controls.RatingSystem == MediaControls.RatingSystemEnum.SCALE ? 3 : 0;
          case PlayerTypes.IsUsingNativeAPIs:
            return WNPRedux.isUsingNativeAPIs ? 1 : 0;
        }
      }
      catch (Exception e)
      {
        WNPRedux.Log(LogType.Error, "WebNowPlaying.dll - Error doing update cycle");
        WNPRedux.Log(LogType.Debug, $"WebNowPlaying.dll - Error Trace: {e}");
      }

      return 0.0;
    }

    internal string GetString()
    {
      try
      {
        switch (playerType)
        {
          case PlayerTypes.Player:
            return WNPRedux.MediaInfo.PlayerName;
          case PlayerTypes.Title:
            return WNPRedux.MediaInfo.Title;
          case PlayerTypes.Artist:
            return WNPRedux.MediaInfo.Artist;
          case PlayerTypes.Album:
            return WNPRedux.MediaInfo.Album;
          case PlayerTypes.Cover:
            if (WNPRedux.MediaInfo.CoverUrl.Length > 0 && LastDownloadedCoverUrl == WNPRedux.MediaInfo.CoverUrl && Uri.IsWellFormedUriString(WNPRedux.MediaInfo.CoverUrl, UriKind.Absolute))
              return (_CoverPath.Length > 0 ? _CoverPath : DefaultCoverOutputLocation).Replace("/", "\\");
            else if (CoverDefaultLocation.Length > 0 && !isInThread)
              return CoverDefaultLocation.Replace("/", "\\");
            return (_CoverPath.Length > 0 ? _CoverPath : DefaultCoverOutputLocation).Replace("/", "\\");
          case PlayerTypes.CoverWebAddress:
            if (LastFailedCoverUrl != WNPRedux.MediaInfo.CoverUrl && LastDownloadedCoverUrl != WNPRedux.MediaInfo.CoverUrl && isInThread)
              return LastDownloadedCoverUrl;
            else
              return WNPRedux.MediaInfo.CoverUrl;
          case PlayerTypes.Position:
            return WNPRedux.MediaInfo.Position;
          case PlayerTypes.Duration:
            return WNPRedux.MediaInfo.Duration;
          case PlayerTypes.Remaining:
            return GetRemaining();
          case PlayerTypes.RatingSystem:
            return WNPRedux.MediaInfo.Controls.RatingSystem.ToString();
        }
      }
      catch (Exception e)
      {
        WNPRedux.Log(LogType.Error, "WebNowPlaying.dll - Error doing getString cycle");
        WNPRedux.Log(LogType.Debug, $"WebNowPlaying.dll - Error Trace: {e}");
      }

      return null;
    }
  }
  public static class Plugin
  {
    static IntPtr StringBuffer = IntPtr.Zero;

    [DllExport]
    public static void Initialize(ref IntPtr data, IntPtr rm)
    {
      data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure(new API(rm))));
    }

    [DllExport]
    public static void Finalize(IntPtr data)
    {
      Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
      measure.Dispose();

      GCHandle.FromIntPtr(data).Free();

      if (StringBuffer != IntPtr.Zero)
      {
        Marshal.FreeHGlobal(StringBuffer);
        StringBuffer = IntPtr.Zero;
      }
    }

    [DllExport]
    public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
    {
      Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
      measure.Reload(new API(rm), ref maxValue);
    }

    [DllExport]
    public static double Update(IntPtr data)
    {
      Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
      return measure.Update();
    }

    [DllExport]
    public static IntPtr GetString(IntPtr data)
    {
      Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
      if (StringBuffer != IntPtr.Zero)
      {
        Marshal.FreeHGlobal(StringBuffer);
        StringBuffer = IntPtr.Zero;
      }

      string stringValue = measure.GetString();
      if (stringValue != null)
      {
        StringBuffer = Marshal.StringToHGlobalUni(stringValue);
      }

      return StringBuffer;
    }
    [DllExport]
    public static void ExecuteBang(IntPtr data, IntPtr args)
    {
      Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
      measure.ExecuteBang(Marshal.PtrToStringUni(args));
    }
  }
}
