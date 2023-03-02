using System;
using System.Runtime.InteropServices;
using Rainmeter;
using System.Linq;
using System.Net;
using System.IO;
using WNPReduxAdapterLibrary;

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
    private static string CoverOutputLocation = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/Temp/Rainmeter/WebNowPlaying/cover.png";
    private string CoverDefaultLocation = "";
    private static string LastDownloadedCoverUrl = "";

    private static string rainmeterFileSettingsLocation = "";

    private PlayerTypes playerType = PlayerTypes.Status;

    public static void DownloadCoverImage() {
      ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, ssl) => true;
      ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;
      string CoverUrl = WNPRedux.mediaInfo.CoverUrl;

      try {
        HttpWebRequest httpWebRequest = (HttpWebRequest) HttpWebRequest.Create(CoverUrl);
        using (HttpWebResponse httpWebResponse = (HttpWebResponse) httpWebRequest.GetResponse()) {
          using (Stream stream = httpWebResponse.GetResponseStream()) {
            Byte[] image = ReadStream(stream);
            WriteStream(image);
            LastDownloadedCoverUrl = CoverUrl;
          }
        }
      } catch (Exception e) {
        API.Log(API.LogType.Error, $"WebNowPlaying.dll - Unable to get album art from: {CoverUrl}");
        API.Log(API.LogType.Debug, $"WebNowPlaying Trace: {e.Data}");
      }
    }
    private static byte[] ReadStream(Stream input) {
      byte[] buffer = new byte[1024];
      using (MemoryStream ms = new MemoryStream()) {
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
          ms.Write(buffer, 0, read);
        }
        return ms.ToArray();
      }
    }

    private static void WriteStream(Byte[] image) {
      try {
        if (CoverOutputLocation == Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/Temp/Rainmeter/WebNowPlaying/cover.png") {
          // Make sure the path folder exists if using it
          Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/Temp/Rainmeter/WebNowPlaying");
        }

        FileStream fs = new FileStream(CoverOutputLocation, FileMode.Create, FileAccess.Write, FileShare.Read);
        BinaryWriter bw = new BinaryWriter(fs);
        try {
          bw.Write(image);
        } catch (Exception e) {
          bw.Close();
          fs.Close();
          throw e;
        } finally {
          bw.Close();
          fs.Close();
        }

      } catch (Exception e) {
        API.Log(API.LogType.Error, $"WebNowPlaying.dll - Unable to download album art to: {CoverOutputLocation}");
        API.Log(API.LogType.Debug, $"WebNowPlaying Trace: {e.Data}");
      }
    }

    static int MeasureCount = 0;

    internal Measure(API api) {
      ++MeasureCount;
      try {
        if (rainmeterFileSettingsLocation != api.GetSettingsFile())
          rainmeterFileSettingsLocation = api.GetSettingsFile();

        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
        System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
        string adapterVersion = fvi.FileVersion;

        void logger(WNPRedux.LogType type, string message) {
          switch (type) {
            case WNPRedux.LogType.DEBUG:
              API.Log(API.LogType.Debug, message); break;
            case WNPRedux.LogType.WARNING:
              API.Log(API.LogType.Warning, message); break;
            case WNPRedux.LogType.ERROR:
              API.Log(API.LogType.Error, message); break;
          }
        }

        WNPRedux.Initialize(8974, adapterVersion, logger, true);
      } catch (Exception e) {
        API.Log(API.LogType.Error, "WebNowPlaying.dll - Error initializing WNPRedux");
        API.Log(API.LogType.Debug, $"WebNowPlaying Trace: {e.Data}");
      }
    }

    internal virtual void Dispose() {
      --MeasureCount;
      if (MeasureCount == 0) WNPRedux.Close();
    }

    internal virtual void Reload(API api, ref double maxValue) {
      string playerTypeString = api.ReadString("PlayerType", "Status");
      try {
        playerType = (PlayerTypes) Enum.Parse(typeof(PlayerTypes), playerTypeString, true);

        if (playerType == PlayerTypes.Cover) {
          string temp = api.ReadPath("CoverPath", null);
          // Only set CoverOutputLocation if it hasn't already been set
          // Otherwise it changes it when a new skin loads WebNowPlaying
          bool isCoverDefaultLocation = CoverOutputLocation == Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/Temp/Rainmeter/WebNowPlaying/cover.png";
          if (isCoverDefaultLocation && temp.Length > 0) CoverOutputLocation = temp;
          temp = api.ReadPath("DefaultPath", null);
          if (temp.Length > 0) CoverDefaultLocation = temp;
        } else if (playerType == PlayerTypes.Progress) {
          maxValue = 100;
        }
      } catch (Exception e) {
        API.Log(API.LogType.Error, "WebNowPlaying.dll - Unknown PlayerType:" + playerTypeString);
        API.Log(API.LogType.Debug, $"WebNowPlaying Trace: {e.Data}");
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
            API.Log(API.LogType.Error, "WebNowPlaying.dll - Failed to parse rating number, assuming 0");
            API.Log(API.LogType.Debug, $"WebNowPlaying Trace: {e.Data}");
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
            API.Log(API.LogType.Error, $"WebNowPlaying.dll - SetPosition argument could not be converted to a double: {args}");
            API.Log(API.LogType.Debug, $"WebNowPlaying Trace: {e.Data}");
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
            API.Log(API.LogType.Error, $"WebNowPlaying.dll - SetVolume argument could not be converted to a double: {args}");
            API.Log(API.LogType.Debug, $"WebNowPlaying Trace: {e.Data}");
          }
        } else {
          API.Log(API.LogType.Warning, $"WebNowPlaying.dll - Unknown bang: {args}");
        }
      } catch (Exception e) {
        API.Log(API.LogType.Error, $"WebNowPlaying.dll - Error using bang: {args}");
        API.Log(API.LogType.Debug, $"WebNowPlaying Trace: {e.Data}");
      }
    }

    internal virtual double Update() {
      if (WNPRedux.mediaInfo.CoverUrl.Length > 0 && LastDownloadedCoverUrl != WNPRedux.mediaInfo.CoverUrl && Uri.IsWellFormedUriString(WNPRedux.mediaInfo.CoverUrl, UriKind.RelativeOrAbsolute))
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
        API.Log(API.LogType.Error, "WebNowPlaying.dll - Error doing update cycle");
        API.Log(API.LogType.Debug, $"WebNowPlaying Trace: {e.Data}");
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
              return CoverOutputLocation;
            else if (CoverDefaultLocation.Length > 0)
              return CoverDefaultLocation;
            return CoverOutputLocation;
          case PlayerTypes.CoverWebAddress:
            return WNPRedux.mediaInfo.CoverUrl;
          case PlayerTypes.Position:
            return WNPRedux.mediaInfo.Position;
          case PlayerTypes.Duration:
            return WNPRedux.mediaInfo.Duration;
        }
      } catch (Exception e) {
        API.Log(API.LogType.Error, "WebNowPlaying.dll - Error doing getString cycle");
        API.Log(API.LogType.Debug, $"WebNowPlaying Trace: {e.Data}");
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
