﻿using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.Core;

namespace PeerCastStation.GUI
{
  [Plugin(PluginType.GUI)]
  public class UserInterface
    : PluginBase,
      IUserInterfacePlugin
  {
    override public string Name { get { return "GUI by Windows.Forms"; } }
    public override bool IsUsable { get { return false; } }

    MainForm mainForm;
    Thread mainThread;
    override protected void OnStart()
    {
      System.Windows.Forms.Application.EnableVisualStyles();
      mainThread = new Thread(() => {
        mainForm = new MainForm(Application);
        if (!mainForm.IsHandleCreated) {
          //ハンドルを強制的に作らせる
          var handle = mainForm.Handle;
        }
        System.Windows.Forms.Application.ApplicationExit += (sender, args) => {
          Application.Stop();
        };
        System.Windows.Forms.Application.Run();
        mainForm = null;
      });
      mainThread.SetApartmentState(ApartmentState.STA);
      mainThread.Start();
    }

    override protected void OnStop()
    {
      if (mainForm!=null && !mainForm.IsDisposed) {
        mainForm.Invoke(new Action(() => {
          if (!mainForm.IsDisposed) {
            System.Windows.Forms.Application.ExitThread();
          }
        }));
      }
      mainThread.Join();
    }

    public void ShowNotificationMessage(NotificationMessage msg)
    {
      if (mainForm==null || mainForm.IsDisposed) return;
      mainForm.Invoke(new Action(() => {
        if (!mainForm.IsDisposed) {
          mainForm.ShowNotificationMessage(msg);
        }
      }));
    }
  }
}
