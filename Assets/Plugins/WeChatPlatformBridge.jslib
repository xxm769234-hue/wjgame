mergeInto(LibraryManager.library, {
  $WJRealtimeState: {
    socket: null,
    receiverName: '',
    generation: 0
  },

  $WJRealtimeNotify: function (method, value) {
    var receiverName = WJRealtimeState.receiverName;
    if (!receiverName) {
      return;
    }
    if (typeof SendMessage === 'function') {
      SendMessage(receiverName, method, value || '');
    } else if (typeof GameGlobal !== 'undefined' && GameGlobal.Module && GameGlobal.Module.SendMessage) {
      GameGlobal.Module.SendMessage(receiverName, method, value || '');
    }
  },

  WJ_WeChatLogin: function (receiverNamePtr) {
    var receiverName = UTF8ToString(receiverNamePtr);
    var send = function (method, value) {
      if (typeof SendMessage === 'function') {
        SendMessage(receiverName, method, value || '');
      } else if (typeof GameGlobal !== 'undefined' && GameGlobal.Module && GameGlobal.Module.SendMessage) {
        GameGlobal.Module.SendMessage(receiverName, method, value || '');
      }
    };

    if (typeof wx === 'undefined' || typeof wx.login !== 'function') {
      send('OnWeChatLoginFailed', '当前环境不支持 wx.login');
      return;
    }

    wx.login({
      success: function (result) {
        if (result && result.code) {
          send('OnWeChatLoginCodeReceived', result.code);
        } else {
          send('OnWeChatLoginFailed', 'wx.login 未返回 code');
        }
      },
      fail: function (error) {
        send('OnWeChatLoginFailed', error && error.errMsg ? error.errMsg : 'wx.login 调用失败');
      }
    });
  },

  WJ_WeChatRequestPayment: function (receiverNamePtr, paymentParametersPtr) {
    var receiverName = UTF8ToString(receiverNamePtr);
    var send = function (method, value) {
      if (typeof SendMessage === 'function') {
        SendMessage(receiverName, method, value || '');
      } else if (typeof GameGlobal !== 'undefined' && GameGlobal.Module && GameGlobal.Module.SendMessage) {
        GameGlobal.Module.SendMessage(receiverName, method, value || '');
      }
    };

    if (typeof wx === 'undefined' || typeof wx.requestPayment !== 'function') {
      send('OnWeChatPaymentFailed', '当前环境不支持 wx.requestPayment');
      return;
    }

    var parameters;
    try {
      parameters = JSON.parse(UTF8ToString(paymentParametersPtr));
    } catch (error) {
      send('OnWeChatPaymentFailed', '支付参数格式错误');
      return;
    }

    wx.requestPayment({
      timeStamp: parameters.timeStamp || '',
      nonceStr: parameters.nonceStr || '',
      'package': parameters.package || '',
      signType: parameters.signType || 'RSA',
      paySign: parameters.paySign || '',
      success: function () {
        send('OnWeChatPaymentSucceeded', 'success');
      },
      fail: function (error) {
        send('OnWeChatPaymentFailed', error && error.errMsg ? error.errMsg : '微信支付调用失败');
      }
    });
  },

  WJ_RealtimeConnect__deps: ['$WJRealtimeState', '$WJRealtimeNotify'],
  WJ_RealtimeConnect: function (receiverNamePtr, urlPtr) {
    var receiverName = UTF8ToString(receiverNamePtr);
    var url = UTF8ToString(urlPtr);
    WJRealtimeState.receiverName = receiverName;
    var generation = ++WJRealtimeState.generation;
    var notifyCurrent = function (method, value) {
      if (WJRealtimeState.generation === generation) {
        WJRealtimeNotify(method, value);
      }
    };

    if (WJRealtimeState.socket) {
      try {
        WJRealtimeState.socket.close();
      } catch (ignored) {}
      WJRealtimeState.socket = null;
    }

    if (typeof wx !== 'undefined' && typeof wx.connectSocket === 'function') {
      var task = wx.connectSocket({ url: url });
      WJRealtimeState.socket = task;
      task.onOpen(function () { notifyCurrent('OnRealtimeOpened', 'connected'); });
      task.onMessage(function (event) { notifyCurrent('OnRealtimeMessage', event && event.data ? event.data : ''); });
      task.onClose(function (event) { notifyCurrent('OnRealtimeClosed', event && event.reason ? event.reason : 'closed'); });
      task.onError(function (error) { notifyCurrent('OnRealtimeError', error && error.errMsg ? error.errMsg : 'WebSocket error'); });
      return;
    }

    if (typeof WebSocket === 'undefined') {
      notifyCurrent('OnRealtimeError', '当前环境不支持 WebSocket');
      return;
    }

    var socket = new WebSocket(url);
    WJRealtimeState.socket = socket;
    socket.onopen = function () { notifyCurrent('OnRealtimeOpened', 'connected'); };
    socket.onmessage = function (event) { notifyCurrent('OnRealtimeMessage', event && event.data ? event.data : ''); };
    socket.onclose = function (event) { notifyCurrent('OnRealtimeClosed', event && event.reason ? event.reason : 'closed'); };
    socket.onerror = function () { notifyCurrent('OnRealtimeError', 'WebSocket error'); };
  },

  WJ_RealtimeSend__deps: ['$WJRealtimeState'],
  WJ_RealtimeSend: function (messagePtr) {
    var socket = WJRealtimeState.socket;
    if (!socket) {
      return;
    }

    var message = UTF8ToString(messagePtr);
    if (typeof socket.send === 'function') {
      if (typeof wx !== 'undefined' && socket.onOpen && socket.onMessage) {
        socket.send({ data: message });
      } else if (socket.readyState === 1) {
        socket.send(message);
      }
    }
  },

  WJ_RealtimeClose__deps: ['$WJRealtimeState'],
  WJ_RealtimeClose: function () {
    if (!WJRealtimeState.socket) {
      return;
    }
    WJRealtimeState.generation++;
    var socket = WJRealtimeState.socket;
    WJRealtimeState.socket = null;
    try {
      if (typeof wx !== 'undefined' && typeof socket.onOpen === 'function') {
        socket.close({ reason: 'client close' });
      } else {
        socket.close(1000, 'client close');
      }
    } catch (error) {
      try { socket.close(); } catch (ignored) {}
    }
  }
});
