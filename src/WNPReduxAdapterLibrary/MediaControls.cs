#pragma warning disable CS1591
using System.Collections.Generic;
using System.Globalization;
using System;

namespace WNPReduxAdapterLibrary
{
  public class MediaControls
  {
    public MediaControls(bool SupportsPlayPause = false, bool SupportsSkipPrevious = false, bool SupportsSkipNext = false, bool SupportsSetPosition = false, bool SupportsSetVolume = false, bool SupportsToggleRepeatMode = false, bool SupportsToggleShuffleActive = false, bool SupportsSetRating = false, RatingSystemEnum RatingSystem = RatingSystemEnum.NONE)
    {
      this.SupportsPlayPause = SupportsPlayPause;
      this.SupportsSkipPrevious = SupportsSkipPrevious;
      this.SupportsSkipNext = SupportsSkipNext;
      this.SupportsSetPosition = SupportsSetPosition;
      this.SupportsSetVolume = SupportsSetVolume;
      this.SupportsToggleRepeatMode = SupportsToggleRepeatMode;
      this.SupportsToggleShuffleActive = SupportsToggleShuffleActive;
      this.SupportsSetRating = SupportsSetRating;
      this.RatingSystem = RatingSystem;
    }

    static Dictionary<string, object> ParseJson(string jsonString)
    {
      Dictionary<string, object> result = new Dictionary<string, object>();
      jsonString = jsonString.Trim();
      if (jsonString.StartsWith("{") && jsonString.EndsWith("}"))
      {
        jsonString = jsonString.Substring(1, jsonString.Length - 2);
        string[] keyValuePairs = jsonString.Split(',');
        foreach (string pair in keyValuePairs)
        {
          string[] keyValuePair = pair.Split(':');
          string key = keyValuePair[0].Trim('"');
          string valueString = keyValuePair[1].Trim();
          object value = null;
          if (valueString.StartsWith("true") || valueString.StartsWith("false"))
          {
            value = bool.Parse(valueString);
          }
          else if (valueString.StartsWith("\""))
          {
            value = valueString.Substring(1, valueString.Length - 2);
          }
          else
          {
            if (int.TryParse(valueString, out int valueInt))
            {
              value = valueInt;
            }
          }
          result.Add(key, value);
        }
      }
      return result;
    }


    public static MediaControls FromJson(string jsonStr)
    {
      Dictionary<string, object> result = ParseJson(jsonStr);
      bool SupportsPlayPause = (bool)result["supports_play_pause"];
      bool SupportsSkipPrevious = (bool)result["supports_skip_previous"];
      bool SupportsSkipNext = (bool)result["supports_skip_next"];
      bool SupportsSetPosition = (bool)result["supports_set_position"];
      bool SupportsSetVolume = (bool)result["supports_set_volume"];
      bool SupportsToggleRepeatMode = (bool)result["supports_toggle_repeat_mode"];
      bool SupportsToggleShuffleActive = (bool)result["supports_toggle_shuffle_active"];
      bool SupportsSetRating = (bool)result["supports_set_rating"];
      RatingSystemEnum RatingSystem = (RatingSystemEnum)Enum.Parse(typeof(RatingSystemEnum), (string)result["rating_system"]);

      return new MediaControls(
        SupportsPlayPause,
        SupportsSkipPrevious,
        SupportsSkipNext,
        SupportsSetPosition,
        SupportsSetVolume,
        SupportsToggleRepeatMode,
        SupportsToggleShuffleActive,
        SupportsSetRating,
        RatingSystem
      );
    }

    public enum RatingSystemEnum { NONE, LIKE, LIKE_DISLIKE, SCALE }
    public bool SupportsPlayPause { get; set; }
    public bool SupportsSkipPrevious { get; set; }
    public bool SupportsSkipNext { get; set; }
    public bool SupportsSetPosition { get; set; }
    public bool SupportsSetVolume { get; set; }
    public bool SupportsToggleRepeatMode { get; set; }
    public bool SupportsToggleShuffleActive { get; set; }
    public bool SupportsSetRating { get; set; }
    public RatingSystemEnum RatingSystem { get; set; }

    private enum Events
    {
      TRY_SET_STATE,
      TRY_SKIP_PREVIOUS,
      TRY_SKIP_NEXT,
      TRY_SET_POSITION,
      TRY_SET_VOLUME,
      TRY_TOGGLE_REPEAT_MODE,
      TRY_TOGGLE_SHUFFLE_ACTIVE,
      TRY_SET_RATING
    }
    /// <summary>
    /// Tries to play the current media
    /// </summary>
    public void TryPlay() { WNPHttpServer.SendMessage($"{Events.TRY_SET_STATE} PLAYING"); }
    /// <summary>
    /// Tries to pause the current media
    /// </summary>
    public void TryPause() { WNPHttpServer.SendMessage($"{Events.TRY_SET_STATE} PAUSED"); }
    /// <summary>
    /// Tries to play/pause the current media
    /// </summary>
    public void TryTogglePlayPause()
    {
      if (WNPRedux.MediaInfo.State == MediaInfo.StateMode.PLAYING) TryPause();
      else TryPlay();
    }
    /// <summary>
    /// Try to skip to the previous media/section
    /// </summary>
    public void TrySkipPrevious() { WNPHttpServer.SendMessage($"{Events.TRY_SKIP_PREVIOUS}"); }
    /// <summary>
    /// Try to skip to the next media/section
    /// </summary>
    public void TrySkipNext() { WNPHttpServer.SendMessage($"{Events.TRY_SKIP_NEXT}"); }
    /// <summary>
    /// Try to set the medias playback progress in seconds
    /// </summary>
    /// <param name="seconds"></param>.
    public void TrySetPositionSeconds(int seconds)
    {
      int positionInSeconds = seconds;
      if (positionInSeconds < 0) positionInSeconds = 0;
      if (positionInSeconds > WNPRedux.MediaInfo.DurationSeconds) positionInSeconds = WNPRedux.MediaInfo.DurationSeconds;
      double positionInPercent = (double)positionInSeconds / WNPRedux.MediaInfo.DurationSeconds; // Doesn't cry about dividing by zero as it's a double
                                                                                                 // This makes sure it always gives us 0.0, not 0,0 (dot instead of comma, regardless of localization)
      string positionInPercentString = positionInPercent.ToString(CultureInfo.InvariantCulture);

      WNPHttpServer.SendMessage($"{Events.TRY_SET_POSITION} {positionInSeconds}:{positionInPercentString}");
    }
    /// <summary>
    /// Try to revert the medias playback progress by x seconds
    /// </summary>
    /// <param name="seconds"></param>.
    public void TryRevertPositionSeconds(int seconds)
    {
      TrySetPositionSeconds(WNPRedux.MediaInfo.PositionSeconds - seconds);
    }
    /// <summary>
    /// Try to forward the medias playback progress by x seconds
    /// </summary>
    /// <param name="seconds"></param>.
    public void TryForwardPositionSeconds(int seconds)
    {
      TrySetPositionSeconds(WNPRedux.MediaInfo.PositionSeconds + seconds);
    }
    /// <summary>
    /// Try to set the medias playback progress in percent
    /// </summary>
    /// <param name="percent"></param>
    public void TrySetPositionPercent(double percent)
    {
      int seconds = (int)Math.Round((percent / 100) * WNPRedux.MediaInfo.DurationSeconds);
      TrySetPositionSeconds(seconds);
    }
    /// <summary>
    /// Try to revert the medias playback progress by x percent
    /// </summary>
    /// <param name="percent"></param>.
    public void TryRevertPositionPercent(double percent)
    {
      int seconds = (int)Math.Round((percent / 100) * WNPRedux.MediaInfo.DurationSeconds);
      TrySetPositionSeconds(WNPRedux.MediaInfo.PositionSeconds - seconds);
    }
    /// <summary>
    /// Try to forward the medias playback progress by x percent
    /// </summary>
    /// <param name="percent"></param>.
    public void TryForwardPositionPercent(double percent)
    {
      int seconds = (int)Math.Round((percent / 100) * WNPRedux.MediaInfo.DurationSeconds);
      TrySetPositionSeconds(WNPRedux.MediaInfo.PositionSeconds + seconds);
    }
    /// <summary>
    /// Try to set the medias volume from 1-100
    /// </summary>
    /// <param name="volume">Number from 0-100</param>
    public void TrySetVolume(int volume)
    {
      int newVolume = volume;
      if (volume < 0) newVolume = 0;
      if (volume > 100) newVolume = 100;
      WNPHttpServer.SendMessage($"{Events.TRY_SET_VOLUME} {newVolume}");
    }
    /// <summary>
    /// Try to toggle through repeat modes
    /// </summary>
    public void TryToggleRepeat() { WNPHttpServer.SendMessage($"{Events.TRY_TOGGLE_REPEAT_MODE}"); }
    /// <summary>
    /// Try to toggle shuffle mode
    /// </summary>
    public void TryToggleShuffleActive() { WNPHttpServer.SendMessage($"{Events.TRY_TOGGLE_SHUFFLE_ACTIVE}"); }
    /// <summary>
    /// Try to set the rating from 0-5 on websites that support it.
    /// Falls back to:
    /// 0 = no rating
    /// 1-2 = Thumbs Down
    /// 3-5 = Thumbs Up
    /// </summary>
    /// <param name="rating">Number from 0-5</param>
    public void TrySetRating(int rating) { WNPHttpServer.SendMessage($"{Events.TRY_SET_RATING} {rating}"); }
  }
}
#pragma warning restore CS1591
