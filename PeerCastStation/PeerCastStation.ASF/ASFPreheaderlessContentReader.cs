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

  public class ASFPreheaderlessContentReaderFactory
    : IContentReaderFactory
  {
    public string Name { get { return "ASF Preheaderless  (WMV or WMA)"; } }

    public IContentReader Create(Channel channel)
    {
      return new ASFPreheaderlessContentReader(channel);
    }

    public bool TryParseContentType(byte[] header_bytes, out string content_type, out string mime_type)
    {
      using (var stream=new MemoryStream(header_bytes)) {
        try {
          for (var chunks=0; chunks<8; chunks++) {
            var chunk = ASFChunk.Read(stream);
            if (chunk.KnownType!=ASFChunk.ChunkType.Header) continue;
            var header = ASFHeader.Read(chunk);
            if (header.Streams.Any(type => type==ASFHeader.StreamType.Video)) {
              content_type = "WMV";
              mime_type = "video/x-ms-wmv";
            }
            else if (header.Streams.Any(type => type==ASFHeader.StreamType.Audio)) {
              content_type = "WMA";
              mime_type = "audio/x-ms-wma";
            }
            else {
              content_type = "ASF";
              mime_type = "video/x-ms-asf";
            }
            return true;
          }
        }
        catch (EndOfStreamException) {
        }
      }
      content_type = null;
      mime_type    = null;
      return false;
    }

  }

  [Plugin]
  public class ASFPreheaderlessContentReaderPlugin
    : PluginBase
  {
    override public string Name { get { return "ASF Preheaderless Content Reader"; } }

    private ASFPreheaderlessContentReaderFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new ASFPreheaderlessContentReaderFactory();
      Application.PeerCast.ContentReaderFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.ContentReaderFactories.Remove(factory);
    }
  }
}
