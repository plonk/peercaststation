using System;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using System.Collections.Generic;

namespace PeerCastStation.ASF
{
  public class ASFPreheaderlessContentReader
    : ASFContentReader, IContentReader
  {
    public new string Name { get { return "ASF Preheaderless (WMV or WMA)"; } }
    private UInt32 seq_num = 0;

    public ASFPreheaderlessContentReader(Channel channel)
      : base(channel)
    {
    }

    protected override ASFChunk ReadNextChunk(Stream stream)
    {
      var chunk = ASFChunk.ReadPreheaderless(stream, seq_num);
      seq_num++;
      return chunk;
    }
  }

  public class ASFPushContentReaderFactory
    : IContentReaderFactory
  {
    public string Name { get { return "ASF Preheaderless  (WMV or WMA)"; } }

    public IContentReader Create(Channel channel)
    {
      return new ASFPreheaderlessContentReader(channel);
    }
  }

  [Plugin]
  public class ASFPushContentReaderPlugin
    : PluginBase
  {
    override public string Name { get { return "ASF Preheaderless Content Reader"; } }

    private ASFPushContentReaderFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new ASFPushContentReaderFactory();
      Application.PeerCast.ContentReaderFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.ContentReaderFactories.Remove(factory);
    }
  }
}
