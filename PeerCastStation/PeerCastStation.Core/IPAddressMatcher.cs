using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net;
using System.Diagnostics;

namespace PeerCastStation {
  // IPアドレス/ホスト名のパターンマッチを行なう。MonoだとGetHostEntry等
  // で逆引きが行なわれないので、ホスト名のパターンは使えない。
  public class IPAddressMatcher {
    public IPAddressMatcher(String expression)
    {
      _stringRepresentation = expression;
      var subexpressions = expression.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(exp => exp.Trim());

      foreach (var exp in subexpressions) {
        var match = new Regex(@"^(\d+)\.(\d+)\.(\d+)\.(\d+)(?:/(\d+))?$").Match(exp);

        if (match.Success) {
          var xs = Enumerable.Range(1, 4).Select(i => uint.Parse(match.Groups[i].Value)).ToArray();
          foreach (var x in xs) {
            if (x > 255) {
              throw new FormatException("octet out of range");
            }
          }
          var ip_address = ToUInt32(xs[0], xs[1], xs[2], xs[3]);
          int netmask_len;
          if (match.Groups[5].Value == "") {
            netmask_len = 32;
          } else {
            netmask_len = int.Parse(match.Groups[5].Value);
          }
          if (netmask_len > 32) {
            throw new FormatException("netmask out of range");
          }

          _networks.Add(Tuple.Create(ip_address, Netmask(netmask_len)));
        } else {
          throw new FormatException("syntax error");
        }
      }
    }

    private uint ToUInt32(uint a, uint b, uint c, uint d)
    {
      return a << 24 | b << 16 | c << 8 | d;
    }

    private uint Netmask(int bits) {
      Debug.Assert(bits >= 0 && bits <= 32);
      return 0xffffffff >> (32 - bits) << (32 - bits);
    }

    public bool Match(IPAddress addr)
    {
      if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        return false;

      var bytes = addr.GetAddressBytes();
      var ip_address = ToUInt32(bytes[0], bytes[1], bytes[2], bytes[3]);

      foreach (var network in _networks) {
        if ((ip_address & network.Item2) ==  network.Item1)
          return true;
      }
      return false;
    }

    public override string ToString()
    {
      return _stringRepresentation;
    }

    private List<Tuple<uint, uint>> _networks = new List<Tuple<uint, uint>>();
    private string _stringRepresentation;
  }
}
