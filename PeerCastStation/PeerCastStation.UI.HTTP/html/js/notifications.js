var NotificationsViewModel = new function() {
  var self = this;
  var updating = false;

  self.alerts = ko.observableArray([]);

  self.update = function() {
    PeerCast.getAllNotificationMessages(function(results) {
      if (results) {
        self.alerts.push.apply(self.alerts, $.map(results, function (data) {
          var alert = "";
          switch (data.type) {
          case "info":    alert = "alert-info"; break;
          case "warning": alert = "alert-danger"; break;
          case "error":   alert = "alert-error"; break;
          }
          var closed = false;
          return {
            title:   data.title,
            message: data.message,
            type:    data.type,
            alert:   alert
          };
        }));
      }
    });
  };

  self.bind = function(target) {
    self.update();
    updating = true;
    ko.applyBindings(self, target);
    updating = false;
  };
};

