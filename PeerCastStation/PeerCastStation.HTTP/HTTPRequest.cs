﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PeerCastStation.HTTP
{
  /// <summary>
  ///クライアントからのHTTPリクエスト内容を保持するクラスです
  /// </summary>
  public class HTTPRequest
  {
    /// <summary>
    /// HTTPメソッドを取得および設定します
    /// </summary>
    public string Method { get; private set; }
    public string Protocol { get; private set; }
    /// <summary>
    /// リクエストされたUriを取得および設定します
    /// </summary>
    public Uri Uri     { get; private set; }
    /// <summary>
    /// リクエストヘッダの値のコレクション取得します
    /// </summary>
    public Dictionary<string, string> Headers { get; private set; }
    public Dictionary<string, string> Parameters { get; private set; }
    public Dictionary<string, string> Cookies { get; private set; }

    public bool KeepAlive {
      get {
        switch (Protocol) {
        case "HTTP/1.1":
          {
            string value;
            if (Headers.TryGetValue("CONNECTION", out value) && value=="close") {
              return false;
            }
            else {
              return true;
            }
          }
        case "HTTP/1.0":
          {
            string value;
            if (Headers.TryGetValue("CONNECTION", out value) && value=="keep-alive") {
              return true;
            }
            else {
              return false;
            }
          }
        default:
          return false;
        }
      }
    }

    /// <summary>
    /// HTTPリクエスト文字列からHTTPRequestオブジェクトを構築します
    /// </summary>
    /// <param name="requests">行毎に区切られたHTTPリクエストの文字列表現</param>
    public HTTPRequest(IEnumerable<string> requests)
    {
      Headers    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      Parameters = new Dictionary<string, string>();
      Cookies    = new Dictionary<string, string>();
      Protocol   = "HTTP/1.0";
      string host = "localhost";
      string path = "/";
      foreach (var req in requests) {
        Match match = null;
        if ((match = Regex.Match(req, @"^(\w+) +(\S+) +(HTTP/1.\d)$", RegexOptions.IgnoreCase)).Success) {
          this.Method = match.Groups[1].Value.ToUpper();
          path = match.Groups[2].Value;
          Protocol = match.Groups[3].Value;
        }
        else if ((match = Regex.Match(req, @"^Host:(.+)$", RegexOptions.IgnoreCase)).Success) {
          host = match.Groups[1].Value.Trim();
          Headers["HOST"] = host;
        }
        else if ((match = Regex.Match(req, @"^Cookie:(\s*)(.+)(\s*)$", RegexOptions.IgnoreCase)).Success) {
          foreach (var pair in match.Groups[2].Value.Split(';')) {
            var md = Regex.Match(pair, @"^([A-Za-z0-9!#$%^&*_\-+|~`'"".]+)=(.*)$");
            if (md.Success) {
              Cookies.Add(md.Groups[1].Value, md.Groups[2].Value);
            }
          }
        }
        else if ((match = Regex.Match(req, @"^(\S*):(.+)$", RegexOptions.IgnoreCase)).Success) {
          Headers[match.Groups[1].Value.ToUpper()] = match.Groups[2].Value.Trim();
        }
      }
      Uri uri;
      if (Uri.TryCreate("http://" + host + path, UriKind.Absolute, out uri)) {
        this.Uri = uri;
        foreach (Match param in Regex.Matches(uri.Query, @"(&|\?)([^&=]+)=([^&=]+)")) {
          this.Parameters.Add(
            Uri.UnescapeDataString(param.Groups[2].Value).ToLowerInvariant(),
            Uri.UnescapeDataString(param.Groups[3].Value));
        }
      }
      else {
        this.Uri = null;
      }
    }
  }

}
