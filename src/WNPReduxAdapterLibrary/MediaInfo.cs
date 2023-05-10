#pragma warning disable IDE1006
#pragma warning disable CS1591
using System;

namespace WNPReduxAdapterLibrary
{
  public class MediaInfo
  {
    private string _Title { get; set; }
    private StateMode _State { get; set; }
    private int _Volume { get; set; }

    public MediaInfo()
    {
      Controls = new MediaControls();
      _Title = "";
      _State = StateMode.STOPPED;
      _ID = "";
      IsNative = false;
      PlayerName = "";
      Artist = "";
      Album = "";
      CoverUrl = "";
      Duration = "0:00";
      DurationSeconds = 0;
      Position = "0:00";
      PositionSeconds = 0;
      PositionPercent = 0;
      Volume = 100;
      Rating = 0;
      RepeatMode = RepeatModeEnum.NONE;
      ShuffleActive = false;
      Timestamp = 0;
    }

    public enum StateMode { STOPPED, PLAYING, PAUSED }
    public enum RepeatModeEnum { NONE, ONE, ALL }
    public bool IsNative { get; set; }
    public MediaControls Controls { get; set; }
    public StateMode State
    {
      get { return _State; }
      set
      {
        if (_State == value) return;
        _State = value;
        Timestamp = (DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);
      }
    }
    public string _ID { get; set; }
    public string PlayerName { get; set; }
    public string Title
    {
      get { return _Title; }
      set
      {
        if (_Title == value) return;
        _Title = value;
        if (value.Length > 0) Timestamp = (DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);
        else Timestamp = 0;
      }
    }
    public string Artist { get; set; }
    public string Album { get; set; }
    public string CoverUrl { get; set; }
    public string Duration { get; set; }
    public int DurationSeconds { get; set; }
    public string Position { get; set; }
    public int PositionSeconds { get; set; }
    public double PositionPercent { get; set; }
    public int Volume
    {
      get { return _Volume; }
      set
      {
        if (_Volume == value) return;
        _Volume = value;
        if (State == StateMode.PLAYING)
        {
          Timestamp = (DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);
        }
      }
    }
    public int Rating { get; set; }
    public RepeatModeEnum RepeatMode { get; set; }
    public bool ShuffleActive { get; set; }
    public decimal Timestamp { get; set; }
  }
}
#pragma warning restore IDE1006
#pragma warning restore CS1591
