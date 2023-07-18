#pragma warning disable CS1591
using static WNPReduxAdapterLibrary.WNPRedux;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using System.Net;
using System.IO;
using System;

namespace WNPReduxAdapterLibrary
{
  public static class WNPHttpServer
  {
    private static bool isStarted = false;
    private static int port = 0;
    private static HttpListener listener;
    private static readonly Dictionary<string, WebSocket> clients = new Dictionary<string, WebSocket>();

    internal static void Start(int port)
    {
      if (isStarted) return;
      isStarted = true;
      WNPHttpServer.port = port;
      new Thread(StartThreaded).Start();
    }

    internal static void Stop()
    {
      if (!isStarted) return;
      isStarted = false;
      try
      {
        listener.Stop();
        listener.Close();
      }
      catch { }
      clients.Clear();
      mediaInfoDictionary.Clear();
      UpdateMediaInfo();
      WNPRedux.clients = 0;
    }

    private static async void StartThreaded()
    {
      listener = new HttpListener();
      listener.Prefixes.Add($"http://127.0.0.1:{port}/");
      listener.Start();
      while (listener.IsListening)
      {
        try
        {
          HttpListenerContext context = await listener.GetContextAsync();
          if (context.Request.IsWebSocketRequest)
          {
            ProcessWebSocketRequest(context);
          }
          else
          {
            if (context.Request.Url.AbsolutePath.StartsWith("/cover"))
            {
              HandleCoverRoute(context);
            }
            else
            {
              context.Response.StatusCode = 400;
              context.Response.Close();
            }
          }
        }
        catch { }
      }
    }

    private static async void ProcessWebSocketRequest(HttpListenerContext context)
    {
      WebSocketContext webSocketContext;
      try
      {
        webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
      }
      catch
      {
        context.Response.StatusCode = 500;
        context.Response.Close();
        return;
      }

      WebSocket webSocket = webSocketContext.WebSocket;
      string clientId = Guid.NewGuid().ToString();
      clients.Add(clientId, webSocket);
      WNPRedux.clients++;

      try
      {
        OnConnect(clientId);
        await ReceiveMessages(clientId, webSocket);
      }
      catch
      {
        OnDisconnect(clientId);
        clients.Remove(clientId);
        WNPRedux.clients--;
        try
        {
          await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        } catch { }
      }
    }

    private static async Task ReceiveMessages(string clientId, WebSocket webSocket)
    {
      var buffer = new byte[1024];

      while (webSocket.State == WebSocketState.Open)
      {
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
        {
          throw new Exception("Disconnected");
        }
        else if (result.MessageType == WebSocketMessageType.Text)
        {
          string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
          OnMessage(clientId, message);
        }
      }

      throw new Exception("Disconnected");
    }

    private static void HandleCoverRoute(HttpListenerContext context)
    {
      var request = context.Request;
      var response = context.Response;

      if (request.HttpMethod != "GET")
      {
        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
        response.Close();
        return;
      }

      string fileName = request.QueryString["name"];
      if (fileName == null || !fileName.EndsWith(".jpg"))
      {
        response.StatusCode = (int)HttpStatusCode.BadRequest;
        response.Close();
        return;
      }
      string filePath = Path.Combine(GetWnpPath(), fileName);

      if (!File.Exists(filePath))
      {
        response.StatusCode = (int)HttpStatusCode.NotFound;
        response.Close();
        return;
      }

      response.ContentType = "image/jpeg";
      response.ContentLength64 = new FileInfo(filePath).Length;
      response.AddHeader("Content-Disposition", "inline");
      response.StatusCode = (int)HttpStatusCode.OK;
      using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
      {
        stream.CopyTo(response.OutputStream);
      }
      response.Close();
    }

    public static event Action<string> OnMessageHook;
    internal static void SendMessage(string message)
    {
      OnMessageHook?.Invoke(message);
      SendMessageToWebSocket(WNPRedux.MediaInfo._ID, message);
    }

    private static void SendMessageToWebSocket(string id, string message)
    {
      if (clients.ContainsKey(id))
      {
        var client = clients[id];
        byte[] buffer = Encoding.UTF8.GetBytes(message);
        client.SendAsync(new ArraySegment<byte>(buffer, 0, message.Length), WebSocketMessageType.Text, true, CancellationToken.None);
      }
    }

    private static void OnConnect(string clientId)
    {
      SendMessageToWebSocket(clientId, $"ADAPTER_VERSION {adapterVersion};WNPRLIB_REVISION 2");
      mediaInfoDictionary.GetOrAdd(clientId, new MediaInfo());
    }

    private static void OnDisconnect(string clientId)
    {
      mediaInfoDictionary.TryRemove(clientId, out MediaInfo temp);
      if (WNPRedux.MediaInfo._ID == temp?._ID)
        UpdateMediaInfo();
    }

    private static void OnMessage(string clientId, string message)
    {
      try
      {
        string type = message.Substring(0, message.IndexOf(" ")).ToUpper();
        string data = message.Substring(message.IndexOf(" ") + 1);

        if (type == "USE_NATIVE_APIS")
        {
          _isUsingNativeAPIs = bool.Parse(data);
          if (_isUsingNativeAPIs && Directory.Exists(disableNativeAPIsPath))
            Directory.Delete(disableNativeAPIsPath);
          else if (!_isUsingNativeAPIs && !Directory.Exists(disableNativeAPIsPath))
            Directory.CreateDirectory(disableNativeAPIsPath);
          UpdateMediaInfo();
          return;
        }

        MediaInfo mediaInfo = GetMediaInfo(clientId);

        if (type == "PLAYER_NAME") mediaInfo.PlayerName = data;
        else if (type == "IS_NATIVE") mediaInfo.IsNative = bool.Parse(data);
        else if (type == "PLAYER_CONTROLS") mediaInfo.Controls = MediaControls.FromJson(data);
        else if (type == "STATE") mediaInfo.State = (MediaInfo.StateMode)Enum.Parse(typeof(MediaInfo.StateMode), data);
        else if (type == "TITLE") mediaInfo.Title = data;
        else if (type == "ARTIST") mediaInfo.Artist = data;
        else if (type == "ALBUM") mediaInfo.Album = data;
        else if (type == "COVER_URL") mediaInfo.CoverUrl = data;
        else if (type == "DURATION_SECONDS")
        {
          mediaInfo.DurationSeconds = Convert.ToInt32(data);
          mediaInfo.Duration = TimeInSecondsToString(mediaInfo.DurationSeconds);
          mediaInfo.PositionPercent = 0;
        }
        else if (type == "POSITION_SECONDS")
        {
          mediaInfo.PositionSeconds = Convert.ToInt32(data);
          mediaInfo.Position = TimeInSecondsToString(mediaInfo.PositionSeconds);

          if (mediaInfo.DurationSeconds > 0)
          {
            mediaInfo.PositionPercent = ((double)mediaInfo.PositionSeconds / mediaInfo.DurationSeconds) * 100.0;
          }
          else
          {
            mediaInfo.PositionPercent = 0;
          }
        }
        else if (type == "VOLUME") mediaInfo.Volume = Convert.ToInt16(data);
        else if (type == "RATING") mediaInfo.Rating = Convert.ToInt16(data);
        else if (type == "REPEAT_MODE") mediaInfo.RepeatMode = (MediaInfo.RepeatModeEnum)Enum.Parse(typeof(MediaInfo.RepeatModeEnum), data);
        else if (type == "SHUFFLE_ACTIVE") mediaInfo.ShuffleActive = bool.Parse(data);
        else if (type == "ERROR") Log(LogType.Error, $"WNPRedux - Browser Error: {data}");
        else if (type == "ERRORDEBUG") Log(LogType.Debug, $"WNPRedux - Browser Error Trace: {data}");
        else Log(LogType.Warning, $"WNPRedux - Unknown message type: {type}; ({message})");

        if (type != "POSITION" && mediaInfo.Title.Length > 0)
          UpdateMediaInfo();
      }
      catch (Exception e)
      {
        Log(LogType.Error, "WNPRedux - Error parsing data from WebNowPlaying");
        Log(LogType.Debug, $"WNPRedux - Error Trace: {e}");
      }
    }
  }
}
#pragma warning restore CS1591
