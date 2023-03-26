using System;
using System.Runtime.InteropServices;
using Rainmeter;
using System.Linq;
using System.Net.Http;
using System.IO;
using WNPReduxAdapterLibrary;
using System.Net;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace WebNowPlaying {
  internal class Measure {
    enum PlayerTypes {
      Status,
      Player,
      Title,
      Artist,
      Album,
      Cover,
      CoverWebAddress,
      Duration,
      Position,
      Progress,
      Volume,
      State,
      Rating,
      Repeat,
      Shuffle
    }

    // Default cover art location, if not set by the skin
    private static readonly string DefaultCoverOutputLocation = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Temp\\Rainmeter\\WebNowPlaying\\cover.png";
    private string CoverDefaultLocation = "";
    private static string LastDownloadedCoverUrl = "";
    private static string LastFailedCoverUrl = "";
    private PlayerTypes playerType = PlayerTypes.Status;
    private static readonly ConcurrentDictionary<string, int> CoverPathDictionary = new ConcurrentDictionary<string, int>() { [DefaultCoverOutputLocation] = 1 };
    private string _CoverPath = "";

    public static void DownloadCoverImage() {
      if (isInThread) return;
      string CoverUrl = WNPRedux.mediaInfo.CoverUrl;
      if (CoverUrl.Length == 0 || LastFailedCoverUrl == CoverUrl || LastDownloadedCoverUrl == CoverUrl || !Uri.IsWellFormedUriString(CoverUrl, UriKind.RelativeOrAbsolute)) return;

      isInThread = true;
      Thread thread = new Thread(DownloadImageThread);
      thread.Start();
    }

    private static readonly object _lock = new object();
    private static bool isInThread = false;
    private static void DownloadImageThread() {
      lock (_lock) {
        string CoverUrl = WNPRedux.mediaInfo.CoverUrl;
        try {

          ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, ssl) => true;
          ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;

          void SaveToFiles(Stream inputStream) {
            // Write to all cover paths for backwards compatibility, this also includes the default path
            foreach (KeyValuePair<string, int> entry in CoverPathDictionary) {
              if (entry.Value > 0) {
                // Make sure the path exists
                try {
                  Directory.CreateDirectory(entry.Key.Substring(0, entry.Key.LastIndexOf("\\")));
                  using (Stream outputStream = File.OpenWrite(entry.Key)) {
                    inputStream.Position = 0;
                    inputStream.CopyTo(outputStream);
                  }
                } catch (Exception e) {
                  WNPRedux.Log(WNPRedux.LogType.Error, $"WebNowPlaying.dll - Failed to write to path: {entry.Key}");
                  WNPRedux.Log(WNPRedux.LogType.Debug, $"WebNowPlaying Trace: {e}");
                }
              }
            }
          }

          using (var httpClientHandler = new HttpClientHandler()) {
            httpClientHandler.AllowAutoRedirect = true;
            httpClientHandler.MaxAutomaticRedirections = 3;
            using (var httpClient = new HttpClient(httpClientHandler)) {
              HttpResponseMessage response = httpClient.GetAsync(CoverUrl).Result;

              if (response.StatusCode == HttpStatusCode.OK) {
                using (Stream inputStream = response.Content.ReadAsStreamAsync().Result)
                  SaveToFiles(inputStream);
                LastDownloadedCoverUrl = CoverUrl;
              } else if (response.StatusCode == (HttpStatusCode)308) {
                string redirectUrl = response.Headers.Location.ToString();
                response = httpClient.GetAsync(redirectUrl).Result;

                if (response.StatusCode == HttpStatusCode.OK) {
                  using (Stream inputStream = response.Content.ReadAsStreamAsync().Result)
                    SaveToFiles(inputStream);
                  LastDownloadedCoverUrl = CoverUrl;
                } else {
                  LastFailedCoverUrl = CoverUrl;
                }
              } else {
                LastFailedCoverUrl = CoverUrl;
                WNPRedux.Log(WNPRedux.LogType.Error, $"WebNowPlaying.dll - Unable to get album art from: {CoverUrl}. Response status code: {response.StatusCode}");
              }
            }
          }
        } catch (Exception e) {
          WNPRedux.Log(WNPRedux.LogType.Error, $"WebNowPLaying.dll - Unexpected error downloading {CoverUrl}");
          WNPRedux.Log(WNPRedux.LogType.Debug, $"WebNowPlaying Trace: {e}");
        }
      }
        
      isInThread = false;
    }

    static int MeasureCount = 0;
    internal Measure(API api) {
      ++MeasureCount;
      try {
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
        System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
        string adapterVersion = fvi.FileVersion;

        void logger(WNPRedux.LogType type, string message) {
          switch (type) {
            case WNPRedux.LogType.Debug:
              API.Log(API.LogType.Debug, message); break;
            case WNPRedux.LogType.Warning:
              API.Log(API.LogType.Warning, message); break;
            case WNPRedux.LogType.Error:
              API.Log(API.LogType.Error, message); break;
          }
        }

        WNPRedux.Initialize(8974, adapterVersion, logger, true);
      } catch (Exception e) {
        WNPRedux.Log(WNPRedux.LogType.Error, "WebNowPlaying.dll - Error initializing WNPRedux");
        WNPRedux.Log(WNPRedux.LogType.Debug, $"WebNowPlaying Trace: {e}");
      }
    }

    internal virtual void Dispose() {
      if (_CoverPath.Length > 0) {
        CoverPathDictionary.AddOrUpdate(_CoverPath, 0, (key, oldValue) => oldValue - 1);
      }
      --MeasureCount;
      if (MeasureCount == 0) WNPRedux.Close();
    }

    internal virtual void Reload(API api, ref double maxValue) {
      string playerTypeString = api.ReadString("PlayerType", "Status");
      try {
        playerType = (PlayerTypes) Enum.Parse(typeof(PlayerTypes), playerTypeString, true);

        if (playerType == PlayerTypes.Cover) {
          string DefaultPath = api.ReadPath("DefaultPath", "");
          if (DefaultPath.Length > 0) CoverDefaultLocation = DefaultPath;

          if (_CoverPath.Length == 0) {
            string CoverPath = api.ReadPath("CoverPath", "");
            if (CoverPath.Length > 0) {
              CoverPathDictionary.AddOrUpdate(CoverPath, 1, (key, oldValue) => oldValue + 1);
              _CoverPath = CoverPath;
            }
          }
        } else if (playerType == PlayerTypes.Progress || playerType == PlayerTypes.Volume) {
          maxValue = 100;
        }
      } catch (Exception e) {
        WNPRedux.Log(WNPRedux.LogType.Error, "WebNowPlaying.dll - Unknown PlayerType:" + playerTypeString);
        WNPRedux.Log(WNPRedux.LogType.Debug, $"WebNowPlaying Trace: {e}");
        playerType = PlayerTypes.Status;
      }
    }

    internal void ExecuteBang(string args) {
      try {
        string bang = args.ToLowerInvariant();

        if (bang.Equals("playpause")) WNPRedux.mediaEvents.TogglePlaying();
        else if (bang.Equals("next")) WNPRedux.mediaEvents.Next();
        else if (bang.Equals("previous")) WNPRedux.mediaEvents.Previous();
        else if (bang.Equals("repeat")) WNPRedux.mediaEvents.ToggleRepeat();
        else if (bang.Equals("shuffle")) WNPRedux.mediaEvents.ToggleShuffle();
        else if (bang.Equals("togglethumbsup")) WNPRedux.mediaEvents.ToggleThumbsUp();
        else if (bang.Equals("togglethumbsdown")) WNPRedux.mediaEvents.ToggleThumbsDown();
        else if (bang.Contains("rating ")) {
          try {
            WNPRedux.mediaEvents.SetRating(Convert.ToInt16(bang.Substring(bang.LastIndexOf(" ") + 1)));
          } catch (Exception e) {
            WNPRedux.Log(WNPRedux.LogType.Error, "WebNowPlaying.dll - Failed to parse rating number, assuming 0");
            WNPRedux.Log(WNPRedux.LogType.Debug, $"WebNowPlaying Trace: {e}");
            WNPRedux.mediaEvents.SetRating(0);
          }
        } else if (bang.Contains("setposition ")) {
          try {
            if (bang.Contains("+")) {
              double percent = Convert.ToDouble(bang.Substring(bang.IndexOf("+") + 1));
              WNPRedux.mediaEvents.ForwardPositionPercent(percent);
            } else if (bang.Contains("-")) {
              double percent = Convert.ToDouble(bang.Substring(bang.IndexOf("-") + 1));
              WNPRedux.mediaEvents.RevertPositionPercent(percent);
            } else {
              double percent = Convert.ToDouble(bang.Substring(bang.IndexOf("setposition ") + 12));
              WNPRedux.mediaEvents.SetPositionPercent(percent);
            }
          } catch (Exception e) {
            WNPRedux.Log(WNPRedux.LogType.Error, $"WebNowPlaying.dll - SetPosition argument could not be converted to a double: {args}");
            WNPRedux.Log(WNPRedux.LogType.Debug, $"WebNowPlaying Trace: {e}");
          }
        } else if (bang.Contains("setvolume ")) {
          try {
            if (bang.Contains('+')) {
              int volume = Convert.ToInt32(bang.Substring(bang.IndexOf("+") + 1));
              WNPRedux.mediaEvents.SetVolume(WNPRedux.mediaInfo.Volume + volume);
            } else if (bang.Contains("-")) {
              int volume = Convert.ToInt32(bang.Substring(bang.IndexOf("-") + 1));
              WNPRedux.mediaEvents.SetVolume(WNPRedux.mediaInfo.Volume - volume);
            } else {
              int volume = Convert.ToInt32(bang.Substring(bang.IndexOf(" ") + 1));
              WNPRedux.mediaEvents.SetVolume(volume);
            }
          } catch (Exception e) {
            WNPRedux.Log(WNPRedux.LogType.Error, $"WebNowPlaying.dll - SetVolume argument could not be converted to a double: {args}");
            WNPRedux.Log(WNPRedux.LogType.Debug, $"WebNowPlaying Trace: {e}");
          }
        } else {
          WNPRedux.Log(WNPRedux.LogType.Warning, $"WebNowPlaying.dll - Unknown bang: {args}");
        }
      } catch (Exception e) {
        WNPRedux.Log(WNPRedux.LogType.Error, $"WebNowPlaying.dll - Error using bang: {args}");
        WNPRedux.Log(WNPRedux.LogType.Debug, $"WebNowPlaying Trace: {e}");
      }
    }

    internal virtual double Update() {
      DownloadCoverImage();

      try {
        switch (playerType) {
          case PlayerTypes.State:
            switch (WNPRedux.mediaInfo.State) {
              case WNPRedux.MediaInfo.StateMode.STOPPED: return 0;
              case WNPRedux.MediaInfo.StateMode.PLAYING: return 1;
              case WNPRedux.MediaInfo.StateMode.PAUSED: return 2;
            }
            break;
          case PlayerTypes.Status:
            return WNPRedux.clients > 0 ? 1 : 0;
          case PlayerTypes.Volume:
            return WNPRedux.mediaInfo.Volume;
          case PlayerTypes.Rating:
            return WNPRedux.mediaInfo.Rating;
          case PlayerTypes.Repeat:
            switch (WNPRedux.mediaInfo.RepeatState) {
              case WNPRedux.MediaInfo.RepeatMode.NONE: return 0;
              case WNPRedux.MediaInfo.RepeatMode.ONE: return 1;
              case WNPRedux.MediaInfo.RepeatMode.ALL: return 2;
            }
            break;
          case PlayerTypes.Shuffle:
            return WNPRedux.mediaInfo.Shuffle ? 1 : 0;
          case PlayerTypes.Progress:
            return WNPRedux.mediaInfo.PositionPercent;
          case PlayerTypes.Position:
            return WNPRedux.mediaInfo.PositionSeconds;
          case PlayerTypes.Duration:
            return WNPRedux.mediaInfo.DurationSeconds;
        }
      } catch (Exception e) {
        WNPRedux.Log(WNPRedux.LogType.Error, "WebNowPlaying.dll - Error doing update cycle");
        WNPRedux.Log(WNPRedux.LogType.Debug, $"WebNowPlaying Trace: {e}");
      }

      return 0.0;
    }

    internal string GetString() {
      try {
        switch (playerType) {
          case PlayerTypes.Player:
            return WNPRedux.mediaInfo.Player;
          case PlayerTypes.Title:
            return WNPRedux.mediaInfo.Title;
          case PlayerTypes.Artist:
            return WNPRedux.mediaInfo.Artist;
          case PlayerTypes.Album:
            return WNPRedux.mediaInfo.Album;
          case PlayerTypes.Cover:
            if (WNPRedux.mediaInfo.CoverUrl.Length > 0 && LastDownloadedCoverUrl == WNPRedux.mediaInfo.CoverUrl && Uri.IsWellFormedUriString(WNPRedux.mediaInfo.CoverUrl, UriKind.RelativeOrAbsolute))
              return (_CoverPath.Length > 0 ? _CoverPath : DefaultCoverOutputLocation).Replace("/", "\\");
            else if (CoverDefaultLocation.Length > 0 && !isInThread)
              return CoverDefaultLocation.Replace("/", "\\");
            return (_CoverPath.Length > 0 ? _CoverPath : DefaultCoverOutputLocation).Replace("/", "\\");
          case PlayerTypes.CoverWebAddress:
            return WNPRedux.mediaInfo.CoverUrl;
          case PlayerTypes.Position:
            return WNPRedux.mediaInfo.Position;
          case PlayerTypes.Duration:
            return WNPRedux.mediaInfo.Duration;
        }
      } catch (Exception e) {
        WNPRedux.Log(WNPRedux.LogType.Error, "WebNowPlaying.dll - Error doing getString cycle");
        WNPRedux.Log(WNPRedux.LogType.Debug, $"WebNowPlaying Trace: {e}");
      }

      return null;
    }
  }
  public static class Plugin {
    static IntPtr StringBuffer = IntPtr.Zero;

    [DllExport]
    public static void Initialize(ref IntPtr data, IntPtr rm) {
      data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure(new Rainmeter.API(rm))));
    }

    [DllExport]
    public static void Finalize(IntPtr data) {
      Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
      measure.Dispose();

      // Now just keeps the websocket server open to limit reconnects
      GCHandle.FromIntPtr(data).Free();

      if (StringBuffer != IntPtr.Zero) {
        Marshal.FreeHGlobal(StringBuffer);
        StringBuffer = IntPtr.Zero;
      }
    }

    [DllExport]
    public static void Reload(IntPtr data, IntPtr rm, ref double maxValue) {
      Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
      measure.Reload(new Rainmeter.API(rm), ref maxValue);
    }

    [DllExport]
    public static double Update(IntPtr data) {
      Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
      return measure.Update();
    }

    [DllExport]
    public static IntPtr GetString(IntPtr data) {
      Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
      if (StringBuffer != IntPtr.Zero) {
        Marshal.FreeHGlobal(StringBuffer);
        StringBuffer = IntPtr.Zero;
      }

      string stringValue = measure.GetString();
      if (stringValue != null) {
        StringBuffer = Marshal.StringToHGlobalUni(stringValue);
      }

      return StringBuffer;
    }
    [DllExport]
    public static void ExecuteBang(IntPtr data, IntPtr args) {
      Measure measure = (Measure) GCHandle.FromIntPtr(data).Target;
      measure.ExecuteBang(Marshal.PtrToStringUni(args));
    }
  }
}
