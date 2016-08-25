
var IndexViewModel = new function() {
  var self = this;
  self.apiVersion            = ko.observable(null);
  self.agentName             = ko.observable(null);
  self.isFirewalled          = ko.observable(null);
  self.uptime                = ko.observable(null);
  self.workingSet            = ko.observable(null);
  self.peakWorkingSet        = ko.observable(null);
  self.virtualMemorySize     = ko.observable(null);
  self.peakVirtualMemorySize = ko.observable(null);
  self.totalProcessorTime    = ko.observable(null);
  self.globalRelayEndPoint   = ko.observable(null);
  self.globalDirectEndPoint  = ko.observable(null);
  self.localRelayEndPoint    = ko.observable(null);
  self.localDirectEndPoint   = ko.observable(null);
  self.environmentVersion    = ko.observable(null);
  self.plugins               = ko.observableArray();
  self.pluginDLLs            = ko.observableArray();

  self.firewalledStatus = ko.computed(function() {
    var firewalled = self.isFirewalled();
    if (firewalled==true) {
      return 'NG';
    }
    else if (firewalled==false) {
      return 'OK';
    }
    else {
      return '不明';
    }
  });

  self.uptimeReadable  = ko.computed(function() {
    var seconds = self.uptime();
    var minutes = Math.floor(seconds /   60) % 60;
    var hours   = Math.floor(seconds / 3600);
    return hours + ":" + (minutes<10 ? ("0"+minutes) : minutes);
  });

  function twoDecimalPlaces(str) {
    var m = str.match(/[^.]*\..{0,2}/);
    if (m == null) {
      return str;
    } else {
      return m[0];
    }
  }

  function convertMemoryReadable(bytes) {
      if (bytes > 1024 * 1024 * 1024) {
          return twoDecimalPlaces(String(bytes / 1024 / 1024 / 1024)) + " GB";
      } else {
          return twoDecimalPlaces(String(bytes / 1024 / 1024)) + " MB";
      }
  }

  self.workingSetReadable = ko.computed(function() {
      return convertMemoryReadable(self.workingSet());
  });
  self.peakWorkingSetReadable = ko.computed(function() {
      return convertMemoryReadable(self.peakWorkingSet());
  });
  self.virtualMemorySizeReadable = ko.computed(function() {
      return convertMemoryReadable(self.virtualMemorySize());
  });
  self.peakVirtualMemorySizeReadable = ko.computed(function() {
      return convertMemoryReadable(self.peakVirtualMemorySize());
  });
  self.totalProcessorTimeReadable = ko.computed(function() {
      return convertMemoryReadable(self.totalProcessorTime());
  });

  self.globalRelayAddress = ko.computed(function() {
    var endpoint = self.globalRelayEndPoint();
    return endpoint ? (endpoint[0]+':'+endpoint[1]) : '無し';
  });

  self.globalDirectAddress = ko.computed(function() {
    var endpoint = self.globalDirectEndPoint();
    return endpoint ? (endpoint[0]+':'+endpoint[1]) : '無し';
  });

  self.localRelayAddress = ko.computed(function() {
    var endpoint = self.localRelayEndPoint();
    return endpoint ? (endpoint[0]+':'+endpoint[1]) : '無し';
  });

  self.localDirectAddress = ko.computed(function() {
    var endpoint = self.localDirectEndPoint();
    return endpoint ? (endpoint[0]+':'+endpoint[1]) : '無し';
  });

  self.update = function() {
    PeerCast.getStatus(function(result) {
      if (result) {
        self.isFirewalled(result.isFirewalled);
        self.uptime(result.uptime);
        self.workingSet(result.workingSet);
        self.peakWorkingSet(result.peakWorkingSet);
        self.virtualMemorySize(result.virtualMemorySize);
        self.peakVirtualMemorySize(result.peakVirtualMemorySize);
        self.totalProcessorTime(twoDecimalPlaces(result.totalProcessorTime));
        self.globalRelayEndPoint(result.globalRelayEndPoint);
        self.globalDirectEndPoint(result.globalDirectEndPoint);
        self.localRelayEndPoint(result.localRelayEndPoint);
        self.localDirectEndPoint(result.localDirectEndPoint);
        self.environmentVersion(result.environmentVersion);
      }
    });
  };

  $(document).ready(function() {
    PeerCast.getVersionInfo(function(result) {
      if (result) {
        self.apiVersion(result.apiVersion);
        self.agentName(result.agentName);
      }
    });
    PeerCast.getPlugins(function(result) {
      if (result) {
        var pluginDLLs = {};
        var plugins = [];
        $.each(result, function(i, plugin) {
          var dllname = plugin.assembly.path;
          pluginDLLs[dllname] = {
            name:    plugin.assembly.name,
            path:    plugin.assembly.path,
            dll:     dllname,
            version: plugin.assembly.version
          };
          plugins.push({
            name:     plugin.name,
            isUsable: plugin.isUsable,
            dll:      dllname,
            version:  plugin.assembly.version
          });
        });
        for (var dllname in pluginDLLs) {
          self.pluginDLLs.push(pluginDLLs[dllname]);
        }
        self.plugins.splice.apply(self.plugins, [0, self.plugins().length].concat(plugins));
      }
    });

    setInterval(self.update, 1000);
  });

  self.bind = function (target) {
    self.update();
    ko.applyBindings(self, target);
  };
};

