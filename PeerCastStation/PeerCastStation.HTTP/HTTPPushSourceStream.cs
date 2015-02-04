using System;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;

namespace PeerCastStation.HTTP
{
  public class HTTPPushSourceStreamFactory
    : SourceStreamFactoryBase
  {
    public override string Name { get { return "HTTP Push"; } }
    public override string Scheme { get { return "http"; } }
    public override SourceStreamType Type {
      get { return SourceStreamType.Broadcast; }
    }
    public override Uri DefaultUri {
      get { return new Uri("http://localhost/"); }
    }

    public override ISourceStream Create(Channel channel, Uri source, IContentReader reader)
    {
      return new HTTPPushSourceStream(this.PeerCast, channel, source, reader);
    }

    public HTTPPushSourceStreamFactory(PeerCast peercast)
      : base(peercast)
    {
    }
  }

  public class HTTPPushSourceConnection
    : SourceConnectionBase
  {
    private IContentReader contentReader;
    private TcpClient client = null;
    private HTTPRequest request = null;
    bool useContentBitrate;

    private enum ConnectionState
    {
      Waiting,
      Connected,
      Receiving,
      Error,
      Closed,
    };
    private ConnectionState state = ConnectionState.Waiting;

    private class ConnectionStoppedExcception : ApplicationException { }
    private class BindErrorException
      : ApplicationException
    {
      public BindErrorException(string message)
        : base(message)
      {
      }
    }

    public HTTPPushSourceConnection(
        PeerCast peercast,
        Channel channel,
        Uri source_uri,
        IContentReader content_reader,
        bool use_content_bitrate)
      : base(peercast, channel, source_uri)
    {
      contentReader = content_reader;
      useContentBitrate = use_content_bitrate;
    }

    protected override void DoPost(Host from, Atom packet)
    {
      //Do nothing
    }

    private IPEndPoint GetBindAddress(Uri uri)
    {
      IPAddress address;
      if (uri.HostNameType==UriHostNameType.IPv4 ||
          uri.HostNameType==UriHostNameType.IPv6) {
        address = IPAddress.Parse(uri.Host);
      }
      else {
        try {
          address = Dns.GetHostAddresses(uri.DnsSafeHost)
            .OrderBy(addr => addr.AddressFamily)
            .FirstOrDefault();
          if (address==null) return null;
        }
        catch (SocketException) {
          return null;
        }
      }
      return new IPEndPoint(address, uri.Port<0 ? 80 : uri.Port);
    }

    protected override StreamConnection DoConnect(Uri source)
    {
      TcpClient client = null;
      var bind_addr = GetBindAddress(source);
      if (bind_addr==null) {
        throw new BindErrorException(String.Format("Cannot resolve bind address: {0}", source.DnsSafeHost));
      }
      var listener = new TcpListener(bind_addr);
      try {
        listener.Start(1);
        Logger.Debug("Listening on {0}", bind_addr);
        var ar = listener.BeginAcceptTcpClient(null, null);
        WaitAndProcessEvents(ar.AsyncWaitHandle, stopped => {
          if (ar.IsCompleted) {
            client = listener.EndAcceptTcpClient(ar);
          }
          return null;
        });
        Logger.Debug("Client accepted");
      }
      catch (SocketException) {
        throw new BindErrorException(String.Format("Cannot bind address: {0}", bind_addr));
      }
      finally {
        listener.Stop();
      }
      if (client!=null) {
        this.client = client;
        return new StreamConnection(client.GetStream(), client.GetStream());
      }
      else {
        return null;
      }
    }

    protected override void DoClose(StreamConnection connection)
    {
      connection.Close();
      client.Close();
      state = ConnectionState.Closed;
    }

    public override void Run()
    {
      SynchronizationContext.SetSynchronizationContext(this.SyncContext);

      this.state = ConnectionState.Waiting;
      try {
        OnStarted();
        if (connection!=null && !IsStopped) {
          if (Handshake()) {
            state = ConnectionState.Connected;
          }
          else {
            throw new IOException("Handshake Error");
          }
          DoProcess();
        }
        this.state = ConnectionState.Closed;
      }
      catch (BindErrorException e) {
        Logger.Error(e);
        DoStop(StopReason.NoHost);
        this.state = ConnectionState.Error;
      }
      catch (IOException e) {
        Logger.Error(e);
        DoStop(StopReason.ConnectionError);
        this.state = ConnectionState.Error;
      }
      catch (ConnectionStoppedExcception) {
        this.state = ConnectionState.Closed;
      }
      SyncContext.ProcessAll();
      OnStopped();
    }

    private void ReadHTTPRequest()
    {
      WaitAndProcessEvents(connection.ReceiveWaitHandle, stopped => {
        request = null;
        try {
          connection.Recv(stream => {
            request = HTTPRequestReader.Read(stream);
          });
        }
        catch (IOException) {
          // データが足りない場合
          return connection.ReceiveWaitHandle;
        }

        if (request==null)
          return connection.ReceiveWaitHandle; // データが無かった場合
        else
          return null; // ループ終了
      });
    }

    void SkipBytes(int length)
    {
      if (length == 0) return;

      WaitAndProcessEvents(connection.ReceiveWaitHandle, stopped => {
        connection.Recv(stream => {
          while (length>0) {
            var ret = stream.ReadByte();

            if (ret<0) {
              break;
            }
            length--;
          }
        });
        if (length>0) {
          return connection.ReceiveWaitHandle;
        }
        else {
          return null;
        }
      });
    }

    // SETUPリクエストを受け取ってPUSH-STARTまでここでやる。
    private bool Handshake()
    {
      ReadHTTPRequest();

      if (!(request.Method=="POST" &&
            request.Headers["CONTENT-TYPE"]=="application/x-wms-pushsetup")) {
        Stop(StopReason.ConnectionError);
        return false;
      }

      var length = int.Parse(request.Headers["CONTENT-LENGTH"]);

      SkipBytes(length);

      Logger.Debug("request read");

      // SETUP応答
      connection.Send(stream =>
      {
        // わかったふりをする
        var script = new String[] {
          "HTTP/1.1 204 No Content\r\n",
          "Server: Cougar/9.01.01.3814\r\n",
          "Cache-Control: no-cache\r\n",
          "Supported: com.microsoft.wm.srvppair, com.microsoft.wm.sswitch, com.microsoft.wm.predstrm, com.microsoft.wm.fastcache, com.microsoft.wm.startupprofile\r\n",
          "Content-Length: 0\r\n",
          "Connection: Keep-Alive\r\n",
          "\r\n"
        };
        foreach (var line in script) {
          var buf = Encoding.ASCII.GetBytes(line);
          stream.Write(buf, 0, buf.Length);
        }
      });

      // PUSH-START要求
      ReadHTTPRequest();

      if (!(request.Method=="POST" &&
            request.Headers["CONTENT-TYPE"]=="application/x-wms-pushstart")) {
        Stop(StopReason.ConnectionError);
        return false;
      }

      return true;
    }

    protected override void DoProcess()
    {
      WaitAndProcessEvents(connection.ReceiveWaitHandle, stopped => {
        if (!stopped) {
          state = ReceiveBody();
          return connection.ReceiveWaitHandle;
        }
        else {
          return null;
        }
      });
    }

    private ChannelInfo UpdateChannelInfo(ChannelInfo a, ChannelInfo b)
    {
      var base_atoms = new AtomCollection(a.Extra);
      var new_atoms  = new AtomCollection(b.Extra);
      if (!useContentBitrate) {
        new_atoms.RemoveByName(Atom.PCP_CHAN_INFO_BITRATE);
      }
      base_atoms.Update(new_atoms);
      return new ChannelInfo(base_atoms);
    }

    private ChannelTrack UpdateChannelTrack(ChannelTrack a, ChannelTrack b)
    {
      var base_atoms = new AtomCollection(a.Extra);
      base_atoms.Update(b.Extra);
      return new ChannelTrack(base_atoms);
    }

    long lastPosition = 0;
    System.Diagnostics.Stopwatch receiveTimeout = null;
    private ConnectionState ReceiveBody()
    {
      if (receiveTimeout==null) {
        receiveTimeout = new System.Diagnostics.Stopwatch();
        receiveTimeout.Start();
      }
      try {
        connection.Recv(stream => {
          if (stream.Length > 0) {
            var data = contentReader.Read(stream);
            if (data.ChannelInfo!=null) {
              Channel.ChannelInfo = UpdateChannelInfo(Channel.ChannelInfo, data.ChannelInfo);
            }
            if (data.ChannelTrack!=null) {
              Channel.ChannelTrack = UpdateChannelTrack(Channel.ChannelTrack, data.ChannelTrack);
            }
            if (data.ContentHeader!=null) {
              Channel.ContentHeader = data.ContentHeader;
              Channel.Contents.Clear();
              lastPosition = data.ContentHeader.Position;
            }
            if (data.Contents!=null) {
              foreach (var content in data.Contents) {
                Channel.Contents.Add(content);
                lastPosition = content.Position;
              }
            }
            receiveTimeout.Reset();
            receiveTimeout.Start();
          }
        });
        if (receiveTimeout.ElapsedMilliseconds>60000) {
          Logger.Error("Recv content timed out");
          Stop(StopReason.ConnectionError);
          return ConnectionState.Error;
        }
      }
      catch (IOException) {
        Stop(StopReason.ConnectionError);
        return ConnectionState.Error;
      }
      return ConnectionState.Receiving;
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      ConnectionStatus status;
      switch (state) {
      case ConnectionState.Waiting:   status = ConnectionStatus.Connecting; break;
      case ConnectionState.Connected: status = ConnectionStatus.Connecting; break;
      case ConnectionState.Receiving: status = ConnectionStatus.Connected;  break;
      case ConnectionState.Error:     status = ConnectionStatus.Error;      break;
      default:                        status = ConnectionStatus.Idle;       break;
      }
      IPEndPoint endpoint = null;
      if (client!=null && client.Connected) {
        endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
      }
      string server_name = "";
      if (request==null || !request.Headers.TryGetValue("SERVER", out server_name)) {
        server_name = "";
      }
      return new ConnectionInfo(
        "HTTP Push Source",
        ConnectionType.Source,
        status,
        SourceUri.ToString(),
        endpoint,
        (endpoint!=null && endpoint.Address.IsSiteLocal()) ? RemoteHostStatus.Local : RemoteHostStatus.None,
        lastPosition,
        RecvRate,
        SendRate,
        null,
        null,
        server_name);
    }

    protected bool WaitAndProcessEvents(WaitHandle wait_handle, Func<bool, WaitHandle> on_signal)
    {
      var handles = new WaitHandle[] {
        SyncContext.EventHandle,
        null,
      };
      bool event_processed = false;
      while (wait_handle!=null) {
        handles[1] = wait_handle;
        var idx = WaitHandle.WaitAny(handles);
        if (idx==0) {
          SyncContext.ProcessAll();
          if (IsStopped) {
            wait_handle = on_signal(IsStopped);
          }
          event_processed = true;
        }
        else {
          wait_handle = on_signal(IsStopped);
        }
      }
      if (!event_processed) {
        SyncContext.ProcessAll();
      }
      return true;
    }

    protected bool WaitAndProcessEvents(IAsyncResult ar, Func<IAsyncResult, bool, IAsyncResult> on_signal)
    {
      if (ar==null) {
        SyncContext.ProcessAll();
        return true;
      }
      return WaitAndProcessEvents(ar.AsyncWaitHandle, stopped => {
        ar = on_signal(ar, stopped);
        if (ar!=null) return ar.AsyncWaitHandle;
        else return null;
      });
    }
  }

  public class HTTPPushSourceStream
    : SourceStreamBase
  {
    public IContentReader ContentReader { get; private set; }
    public bool UseContentBitrate { get; private set; }

    public HTTPPushSourceStream(PeerCast peercast, Channel channel, Uri source_uri, IContentReader reader)
      : base(peercast, channel, source_uri)
    {
      this.ContentReader = reader;
      this.UseContentBitrate = channel.ChannelInfo==null || channel.ChannelInfo.Bitrate==0;
    }

    public override ConnectionInfo GetConnectionInfo()
    {
      if (sourceConnection!=null) {
        return sourceConnection.GetConnectionInfo();
      }
      else {
        ConnectionStatus status;
        switch (StoppedReason) {
        case StopReason.UserReconnect: status = ConnectionStatus.Connecting; break;
        case StopReason.UserShutdown:  status = ConnectionStatus.Idle;       break;
        default:                       status = ConnectionStatus.Error;      break;
        }
        IPEndPoint endpoint = null;
        string server_name = "";
        return new ConnectionInfo(
          "HTTP Push Source",
          ConnectionType.Source,
          status,
          SourceUri.ToString(),
          endpoint,
          RemoteHostStatus.None,
          null,
          null,
          null,
          null,
          null,
          server_name);
      }
    }

    protected override ISourceConnection CreateConnection(Uri source_uri)
    {
      return new HTTPPushSourceConnection(PeerCast, Channel, source_uri, ContentReader, UseContentBitrate);
    }

    protected override void OnConnectionStopped(ConnectionStoppedEvent msg)
    {
      switch (msg.StopReason) {
      case StopReason.UserReconnect:
        break;
      case StopReason.UserShutdown:
        Stop(msg.StopReason);
        break;
      default:
        ThreadPool.QueueUserWorkItem(state => {
          Thread.Sleep(3000);
          Reconnect();
        });
        break;
      }
    }

    public override SourceStreamType Type
    {
      get { return SourceStreamType.Broadcast; }
    }

    [Plugin]
    class HTTPPushSourceStreamPlugin
      : PluginBase
    {
      override public string Name { get { return "HTTP Push Source"; } }

      private HTTPPushSourceStreamFactory factory;
      override protected void OnAttach()
      {
        if (factory==null) factory = new HTTPPushSourceStreamFactory(Application.PeerCast);
        Application.PeerCast.SourceStreamFactories.Add(factory);
      }

      override protected void OnDetach()
      {
        Application.PeerCast.SourceStreamFactories.Remove(factory);
      }
    }
  }
}
