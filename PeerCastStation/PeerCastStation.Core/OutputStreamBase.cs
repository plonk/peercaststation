﻿// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.IO;
using System.Net;
using System.Threading;

namespace PeerCastStation.Core
{
  public abstract class OutputStreamFactoryBase
    : IOutputStreamFactory
  {
    protected PeerCast PeerCast { get; private set; }
    public OutputStreamFactoryBase(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }

    public abstract string Name { get; }
    public abstract OutputStreamType OutputStreamType { get; }
    public virtual int Priority { get { return 0; } }
    public abstract IOutputStream Create(Stream input_stream, Stream output_stream, EndPoint remote_endpoint, AccessControlInfo access_control, Guid channel_id, byte[] header);
    public abstract Guid? ParseChannelID(byte[] header);
  }

  public abstract class OutputStreamBase
    : IOutputStream
  {
    public PeerCast PeerCast { get; private set; }
    public Stream InputStream { get; private set; }
    public Stream OutputStream { get; private set; }
    public StreamConnection Connection { get; private set; }
    public AccessControlInfo AccessControl { get; private set; }
    public EndPoint RemoteEndPoint { get; private set; }
    public Channel Channel { get; private set; }
    public bool IsLocal { get; private set; }
    public int UpstreamRate
    {
      get {
        if (IsLocal || Channel==null) {
          return 0;
        }
        else {
          return GetUpstreamRate();
        }
      }
    }
    volatile bool isStopped;
    public bool IsStopped { get { return isStopped; } private set { isStopped = value; } }
    public StopReason StoppedReason { get; private set; }
    public event StreamStoppedEventHandler Stopped;
    public bool HasError { get; private set; }
    public float SendRate { get { return Connection.SendRate; } }
    public float RecvRate { get { return Connection.ReceiveRate; } }
    protected QueuedSynchronizationContext SyncContext { get; private set; }
    protected Logger Logger { get; private set; }

    public abstract ConnectionInfo GetConnectionInfo();

    private Thread mainThread;
    public OutputStreamBase(
      PeerCast peercast,
      Stream input_stream,
      Stream output_stream,
      EndPoint remote_endpoint,
      AccessControlInfo access_control,
      Channel channel,
      byte[] header)
    {
      this.PeerCast = peercast;
      this.Connection = new StreamConnection(input_stream, output_stream, header);
      this.Connection.SendTimeout = 10000;
      this.RemoteEndPoint = remote_endpoint;
      this.AccessControl = access_control;
      this.Channel = channel;
      var ip = remote_endpoint as IPEndPoint;
      this.IsLocal = ip!=null ? ip.Address.IsSiteLocal() : true;
      this.IsStopped = false;
      this.mainThread = new Thread(MainProc);
      this.mainThread.Name = String.Format("{0}:{1}", this.GetType().Name, remote_endpoint);
      this.SyncContext = new QueuedSynchronizationContext();
      this.Logger = new Logger(this.GetType());
    }

    protected virtual int GetUpstreamRate()
    {
      return 0;
    }

    protected virtual void MainProc()
    {
      SynchronizationContext.SetSynchronizationContext(this.SyncContext);
      OnStarted();
      while (!IsStopped) {
        WaitEventAny();
        DoProcess();
      }
      Cleanup();
      OnStopped();
    }

    protected virtual void Cleanup()
    {
      Connection.Close();
    }

    protected virtual void WaitEventAny()
    {
      WaitHandle.WaitAny(new WaitHandle[] {
        Connection.ReceiveWaitHandle,
        SyncContext.EventHandle,
      }, 10);
    }

    protected virtual void OnStarted()
    {
    }

    protected virtual void OnStopped()
    {
      if (Stopped!=null) {
        Stopped(this, new StreamStoppedEventArgs(this.StoppedReason));
      }
    }

    protected virtual void DoProcess()
    {
      try {
        Connection.CheckErrors();
      }
      catch (IOException e) {
        Logger.Info(e);
        OnError();
      }
      OnIdle();
      SyncContext.ProcessAll();
    }

    protected virtual void DoStart()
    {
      try {
        if ((mainThread.ThreadState & (ThreadState.Stopped | ThreadState.Unstarted))!=0) {
          IsStopped = false;
          mainThread.Start();
        }
        else {
          throw new InvalidOperationException("Output Streams is already started");
        }
      }
      catch (ThreadStateException) {
        throw new InvalidOperationException("Output Streams is already started");
      }
    }

    protected virtual void DoStop(StopReason reason)
    {
      StoppedReason = reason;
      IsStopped = true;
    }

    protected virtual void DoPost(Host from, Atom packet)
    {
    }

    protected virtual void PostAction(Action proc)
    {
      SyncContext.Post(dummy => { proc(); }, null);
    }

    protected virtual void OnIdle()
    {
    }

    protected virtual void OnError()
    {
      HasError = true;
      Stop(StopReason.ConnectionError);
    }

    public void Start()
    {
      DoStart();
    }

    public void Post(Host from, Atom packet)
    {
      if (!IsStopped) {
        PostAction(() => {
          DoPost(from, packet);
        });
      }
    }

    public void Stop()
    {
      if (!IsStopped) {
        PostAction(() => {
          DoStop(StopReason.UserShutdown);
        });
      }
    }

    public void Join()
    {
      if (mainThread!=null && mainThread.IsAlive) {
        mainThread.Join();
      }
    }

    public void Stop(StopReason reason)
    {
      if (!IsStopped) {
        PostAction(() => {
          DoStop(reason);
        });
      }
    }

    protected void Send(byte[] bytes)
    {
      try {
        Connection.Send(bytes);
      }
      catch (IOException e) {
        Logger.Info(e);
        OnError();
      }
    }

    protected void Send(Atom atom)
    {
      try {
        Connection.Send(stream => AtomWriter.Write(stream, atom));
      }
      catch (IOException e) {
        Logger.Info(e);
        OnError();
      }
    }

    protected bool Recv(Action<Stream> proc)
    {
      try {
        return Connection.Recv(proc);
      }
      catch (IOException e) {
        Logger.Info(e);
        OnError();
        return false;
      }
    }

    public abstract OutputStreamType OutputStreamType { get; }
  }
}
