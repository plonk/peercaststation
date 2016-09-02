﻿using System;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;

namespace PeerCastStation
{
  public class ChannelNotifier
    : IChannelMonitor
  {
    private static TimeSpan messageExpires = TimeSpan.FromMinutes(1);
    public static TimeSpan MessageExpires {
      get { return messageExpires; }
      set { messageExpires = value; }
    }
    private System.Diagnostics.Stopwatch messageExpireTimer = new System.Diagnostics.Stopwatch();
    private NotificationMessage lastMessage;
    private PeerCastApplication app;
    public ChannelNotifier(PeerCastApplication app)
    {
      this.app = app;
      this.messageExpireTimer.Start();
      this.app.PeerCast.ChannelAdded   += (sender, args) => {
        args.Channel.Closed += OnChannelClosed;
        args.Channel.ChannelInfoChanged += OnChannelInfoChanged;
      };
      this.app.PeerCast.ChannelRemoved += (sender, args) => {
        args.Channel.Closed -= OnChannelClosed;
        args.Channel.ChannelInfoChanged -= OnChannelInfoChanged;
      };
    }

    private void OnChannelInfoChanged(object sender, ChannelInfoChangedEventArgs args)
    {
      if (args.OldValue.Comment == args.NewValue.Comment)
        return;

      var comment = args.NewValue.Comment;

      if (String.IsNullOrWhiteSpace(comment))
        return;

      var name = args.NewValue.Name ?? "(null)";
      String title;
      if ((sender as Channel).IsBroadcasting) {
        title = $"{name}でメッセージを送信";
      }
      else {
        title = $"{name}からのメッセージ";
      }
      var text = $"「{comment}」";

      var msg = new NotificationMessage(title, text, NotificationMessageType.Info);
      NotifyMessage(msg);
    }

    public void OnChannelClosed(object sender, StreamStoppedEventArgs args)
    {
      var channel = (Channel)sender;
      switch (args.StopReason) {
      case StopReason.OffAir: {
          var msg = new NotificationMessage(
            channel.ChannelInfo.Name,
            "チャンネルが終了しました",
            NotificationMessageType.Info);
          NotifyMessage(msg);
        }
        break;
      case StopReason.NoHost:
      case StopReason.ConnectionError: {
          var msg = new NotificationMessage(
            channel.ChannelInfo.Name,
            "チャンネルに接続できませんでした",
            NotificationMessageType.Error);
          NotifyMessage(msg);
        }
        break;
      }
    }

    private void NotifyMessage(NotificationMessage msg)
    {
      lock (messageExpireTimer) {
        if (messageExpireTimer.Elapsed>=MessageExpires) {
          lastMessage = null;
          messageExpireTimer.Reset();
          messageExpireTimer.Start();
        }
        if (lastMessage==null || !lastMessage.Equals(msg)) {
          foreach (var ui in this.app.Plugins.Where(p => p is IUserInterfacePlugin)) {
            ((IUserInterfacePlugin)ui).ShowNotificationMessage(msg);
          }
          lastMessage = msg;
        }
      }
    }

    public void OnTimer()
    {
    }
  }
}
