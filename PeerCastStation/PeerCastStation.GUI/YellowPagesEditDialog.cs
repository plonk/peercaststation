﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PeerCastStation.Core;

namespace PeerCastStation.GUI
{
  public partial class YellowPagesEditDialog : Form
  {
    public string YPName   { get; set; }
    public Uri    Uri      { get; set; }
    public string Protocol { get; set; }

    private class YellowPageFactoryItem
    {
      public IYellowPageClientFactory Factory { get; private set; }
      public YellowPageFactoryItem(IYellowPageClientFactory factory)
      {
        this.Factory = factory;
      }
      public override string ToString()
      {
        return this.Factory.Name;
      }
    }

    public YellowPagesEditDialog(PeerCast peerCast)
    {
      InitializeComponent();
      ypProtocolList.Items.AddRange(peerCast.YellowPageFactories.Select(factory => new YellowPageFactoryItem(factory)).ToArray());
    }

    private void okButton_Click(object sender, EventArgs e)
    {
      YPName   = ypNameText.Text;
      var protocol_item = ypProtocolList.SelectedItem as YellowPageFactoryItem;
      Protocol = protocol_item!=null ? protocol_item.Factory.Protocol : null;
      if (!String.IsNullOrEmpty(YPName) && !String.IsNullOrEmpty(Protocol)) {
        Uri uri;
        var md = System.Text.RegularExpressions.Regex.Match(ypAddressText.Text, @"\A([^:/]+)(:(\d+))?\Z");
        if (md.Success &&
            Uri.CheckHostName(md.Groups[1].Value)!=UriHostNameType.Unknown &&
            Uri.TryCreate(Protocol + "://" + ypAddressText.Text, UriKind.Absolute, out uri) &&
            protocol_item.Factory.CheckURI(uri)) {
          Uri = uri;
          DialogResult = DialogResult.OK;
        }
        else if (Uri.TryCreate(ypAddressText.Text, UriKind.Absolute, out uri) &&
                 protocol_item.Factory.CheckURI(uri)) {
          Uri = uri;
          DialogResult = DialogResult.OK;
        }
      }
    }
  }
}
