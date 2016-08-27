using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net;

namespace PeerCastStation {

  // IPアドレス/ホスト名のパターンマッチを行なう。MonoだとGetHostEntry等
  // で逆引きが行なわれないので、ホスト名のパターンは使えない。
  public class IPAddressMatcher
  {
    enum PatternType {
      Hostname,
      IPv4Address
    }

    public IPAddressMatcher(String expression)
    {
      _stringRepresentation = expression;
      var subexpressions = expression.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(exp => exp.Trim());
      Regex pattern;
      PatternType type;

      foreach (var exp in subexpressions) {
        var escaped = Regex.Escape(exp);

        if (new Regex(@"^[\d.]+$").Match(exp).Success) {
          type = PatternType.IPv4Address;
        } else if (new Regex(@"^[A-z.\-_]+$").Match(exp).Success) {
          type = PatternType.Hostname;
        } else {
          throw new FormatException();
        }

        if (new Regex(@"\.$").Match(exp).Success) {
          // 前方一致
          pattern = new Regex($"^{escaped}.+$");
        }
        else if (new Regex(@"^\.").Match(exp).Success) {
          // 後方一致
          pattern = new Regex($"^.+{escaped}$");
        }
        else {
          // 完全一致
          pattern = new Regex($"^{escaped}$");
        }

        _expressions.Add(Tuple.Create(type, pattern));
      }
    }

    public bool Match(IPAddress addr)
    {
      var addr_str = addr.ToString();
      foreach (var pair in _expressions) {
        var type = pair.Item1;
        var regex = pair.Item2;

        if (type == PatternType.IPv4Address) {
          if (regex.Match(addr_str).Success) {
            return true;
          }
        } else if (type == PatternType.Hostname) {
          var host_entry = Dns.GetHostEntry(addr);
          if (regex.Match(host_entry.HostName).Success) {
            return true;
          }
        }
      }
      return false;
    }

    public override string ToString()
    {
      return _stringRepresentation;
    }

    private List<Tuple<PatternType, Regex>> _expressions = new List<Tuple<PatternType, Regex>>();
    private string _stringRepresentation;
  }
}

