var NotificationsViewModel = new function() {
  var self = this;

  self.alerts = ko.observableArray([]);

  self.update = function() {
    PeerCast.getAllNotificationMessages(function(results) {
      if (!results) {
        return;
      }

      results.reverse();
      var new_alerts = $.map(results, function (data) {
        var closed = false;
        return {
          title:   data.title,
          message: data.message,
          type:    data.type,
        };
      })
      self.alerts(new_alerts);
    });
  };

  self.bind = function(target) {
    self.update();
    ko.applyBindings(self, target);
  };

  $(function() {
    setInterval(self.update, 1000);
  });
};

