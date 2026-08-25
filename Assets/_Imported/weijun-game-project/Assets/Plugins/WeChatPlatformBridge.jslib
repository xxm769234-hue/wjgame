mergeInto(LibraryManager.library, {
  $WJRealtimeState: {
    socket: null,
    receiverName: '',
    generation: 0
  },

  $WJFriendGiftState: {
    manager: null,
    openlink: '',
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

  WJ_WeChatRequestPrivacyAuthorization: function (receiverNamePtr) {
    var receiverName = UTF8ToString(receiverNamePtr);
    var send = function (method, value) {
      if (typeof SendMessage === 'function') {
        SendMessage(receiverName, method, value || '');
      } else if (typeof GameGlobal !== 'undefined' && GameGlobal.Module && GameGlobal.Module.SendMessage) {
        GameGlobal.Module.SendMessage(receiverName, method, value || '');
      }
    };

    if (typeof wx === 'undefined' || typeof wx.requirePrivacyAuthorize !== 'function') {
      send('OnWeChatPrivacyAuthorizationFailed', '当前微信基础库不支持隐私授权接口');
      return;
    }

    wx.requirePrivacyAuthorize({
      success: function () {
        send('OnWeChatPrivacyAuthorized', '');
      },
      fail: function (error) {
        send(
          'OnWeChatPrivacyAuthorizationFailed',
          error && error.errMsg ? error.errMsg : '微信隐私授权未完成');
      }
    });
  },

  WJ_WeChatOpenPrivacyContract: function (receiverNamePtr) {
    var receiverName = UTF8ToString(receiverNamePtr);
    var send = function (method, value) {
      if (typeof SendMessage === 'function') {
        SendMessage(receiverName, method, value || '');
      } else if (typeof GameGlobal !== 'undefined' && GameGlobal.Module && GameGlobal.Module.SendMessage) {
        GameGlobal.Module.SendMessage(receiverName, method, value || '');
      }
    };

    if (typeof wx === 'undefined' || typeof wx.openPrivacyContract !== 'function') {
      send('OnWeChatPrivacyContractFailed', '当前微信基础库不支持打开隐私协议');
      return;
    }

    wx.openPrivacyContract({
      success: function () {
        send('OnWeChatPrivacyContractOpened', '');
      },
      fail: function (error) {
        send(
          'OnWeChatPrivacyContractFailed',
          error && error.errMsg ? error.errMsg : '打开微信隐私协议失败');
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

  WJ_WeChatShowRewardedVideo: function (receiverNamePtr, watchIdPtr, adUnitIdPtr) {
    var receiverName = UTF8ToString(receiverNamePtr);
    var watchId = UTF8ToString(watchIdPtr);
    var adUnitId = UTF8ToString(adUnitIdPtr);
    var send = function (method, value) {
      if (typeof SendMessage === 'function') {
        SendMessage(receiverName, method, value || '');
      } else if (typeof GameGlobal !== 'undefined' && GameGlobal.Module && GameGlobal.Module.SendMessage) {
        GameGlobal.Module.SendMessage(receiverName, method, value || '');
      }
    };

    if (typeof wx === 'undefined' || typeof wx.createRewardedVideoAd !== 'function') {
      send('OnWeChatRewardedVideoFailed', '当前环境不支持 wx.createRewardedVideoAd');
      return;
    }
    if (!adUnitId) {
      send('OnWeChatRewardedVideoFailed', '服务器未返回广告位 ID');
      return;
    }

    var ad = wx.createRewardedVideoAd({ adUnitId: adUnitId });
    var settled = false;
    var cleanup = function () {
      if (ad.offClose) ad.offClose(onClose);
      if (ad.offError) ad.offError(onError);
    };
    var onClose = function (result) {
      if (settled) return;
      settled = true;
      cleanup();
      if (result && result.isEnded === true) {
        send('OnWeChatRewardedVideoCompleted', watchId);
      } else {
        send('OnWeChatRewardedVideoClosed', watchId);
      }
    };
    var onError = function (error) {
      if (settled) return;
      settled = true;
      cleanup();
      send('OnWeChatRewardedVideoFailed', error && error.errMsg ? error.errMsg : '激励视频加载失败');
    };

    ad.onClose(onClose);
    ad.onError(onError);
    ad.show().catch(function () {
      return ad.load().then(function () { return ad.show(); });
    }).catch(onError);
  },

  WJ_WeChatRequestSubscribeMessages: function (receiverNamePtr, requestJsonPtr) {
    var receiverName = UTF8ToString(receiverNamePtr);
    var send = function (method, value) {
      if (typeof SendMessage === 'function') {
        SendMessage(receiverName, method, value || '');
      } else if (typeof GameGlobal !== 'undefined' && GameGlobal.Module && GameGlobal.Module.SendMessage) {
        GameGlobal.Module.SendMessage(receiverName, method, value || '');
      }
    };

    if (typeof wx === 'undefined' || typeof wx.requestSubscribeMessage !== 'function') {
      send('OnWeChatSubscribeFailed', '当前环境不支持 wx.requestSubscribeMessage');
      return;
    }

    var request;
    try {
      request = JSON.parse(UTF8ToString(requestJsonPtr));
    } catch (error) {
      send('OnWeChatSubscribeFailed', '订阅模板参数格式错误');
      return;
    }
    var templateIds = request && request.templateIds ? request.templateIds : [];
    if (!templateIds.length) {
      send('OnWeChatSubscribeFailed', '没有可申请的订阅模板');
      return;
    }

    wx.requestSubscribeMessage({
      tmplIds: templateIds,
      success: function (result) {
        var items = [];
        for (var i = 0; i < templateIds.length; i++) {
          var templateId = templateIds[i];
          items.push({
            templateId: templateId,
            status: result && result[templateId] ? result[templateId] : 'reject'
          });
        }
        send('OnWeChatSubscribeResult', JSON.stringify({ results: items }));
      },
      fail: function (error) {
        send('OnWeChatSubscribeFailed', error && error.errMsg ? error.errMsg : '微信订阅授权失败');
      }
    });
  },

  WJ_WeChatChooseAvatar: function (receiverNamePtr) {
    var receiverName = UTF8ToString(receiverNamePtr);
    var send = function (method, value) {
      if (typeof SendMessage === 'function') {
        SendMessage(receiverName, method, value || '');
      } else if (typeof GameGlobal !== 'undefined' && GameGlobal.Module && GameGlobal.Module.SendMessage) {
        GameGlobal.Module.SendMessage(receiverName, method, value || '');
      }
    };

    if (typeof wx !== 'undefined' && typeof wx.chooseMedia === 'function' && typeof wx.getFileSystemManager === 'function') {
      wx.chooseMedia({
        count: 1,
        mediaType: ['image'],
        sourceType: ['album', 'camera'],
        sizeType: ['compressed'],
        success: function (result) {
          var file = result && result.tempFiles && result.tempFiles.length ? result.tempFiles[0] : null;
          var filePath = file && file.tempFilePath ? file.tempFilePath : '';
          if (!filePath) {
            send('OnAvatarSelectionFailed', '微信未返回头像临时文件');
            return;
          }

          wx.getFileSystemManager().readFile({
            filePath: filePath,
            encoding: 'base64',
            success: function (readResult) {
              var lowerPath = filePath.toLowerCase();
              var isPng = lowerPath.lastIndexOf('.png') === lowerPath.length - 4;
              send('OnAvatarSelected', JSON.stringify({
                fileName: isPng ? 'avatar.png' : 'avatar.jpg',
                mimeType: isPng ? 'image/png' : 'image/jpeg',
                base64Data: readResult && readResult.data ? readResult.data : ''
              }));
            },
            fail: function (error) {
              send('OnAvatarSelectionFailed', error && error.errMsg ? error.errMsg : '读取头像文件失败');
            }
          });
        },
        fail: function (error) {
          send('OnAvatarSelectionFailed', error && error.errMsg ? error.errMsg : '头像选择已取消');
        }
      });
      return;
    }

    if (typeof document === 'undefined' || typeof FileReader === 'undefined') {
      send('OnAvatarSelectionFailed', '当前运行环境不支持系统图片选择');
      return;
    }

    var input = document.createElement('input');
    input.type = 'file';
    input.accept = '.jpg,.jpeg,.png,image/jpeg,image/png';
    input.style.position = 'fixed';
    input.style.left = '-10000px';
    input.style.top = '-10000px';
    document.body.appendChild(input);

    var finished = false;
    var selectionStarted = false;
    var focusTimer = 0;
    var handleWindowFocus = function () {
      clearTimeout(focusTimer);
      focusTimer = setTimeout(function () {
        if (!finished && !selectionStarted && (!input.files || !input.files.length)) {
          finish('OnAvatarSelectionFailed', '头像选择已取消');
        }
      }, 300);
    };
    var cleanup = function () {
      clearTimeout(focusTimer);
      input.onchange = null;
      input.oncancel = null;
      window.removeEventListener('focus', handleWindowFocus);
      if (input.parentNode) {
        input.parentNode.removeChild(input);
      }
    };
    var finish = function (method, value) {
      if (finished) {
        return;
      }
      finished = true;
      cleanup();
      send(method, value);
    };

    input.onchange = function () {
      var file = input.files && input.files.length ? input.files[0] : null;
      if (!file) {
        finish('OnAvatarSelectionFailed', '头像选择已取消');
        return;
      }
      selectionStarted = true;
      if (file.size > 16 * 1024 * 1024) {
        finish('OnAvatarSelectionFailed', '头像源文件超过 16 MB，请重新选择较小图片');
        return;
      }

      var reader = new FileReader();
      reader.onload = function () {
        var dataUrl = String(reader.result || '');
        var separator = dataUrl.indexOf(',');
        var base64Data = separator >= 0 ? dataUrl.substring(separator + 1) : '';
        finish('OnAvatarSelected', JSON.stringify({
          fileName: file.name || 'avatar.jpg',
          mimeType: file.type || 'image/jpeg',
          base64Data: base64Data
        }));
      };
      reader.onerror = function () {
        finish('OnAvatarSelectionFailed', '读取头像文件失败');
      };
      reader.readAsDataURL(file);
    };
    input.oncancel = function () {
      finish('OnAvatarSelectionFailed', '头像选择已取消');
    };
    window.addEventListener('focus', handleWindowFocus);
    input.click();
  },

  WJ_WeChatSyncFriendLevel: function (level) {
    if (typeof wx === 'undefined' || typeof wx.setUserCloudStorage !== 'function') {
      return;
    }

    wx.setUserCloudStorage({
      KVDataList: [
        { key: 'player_level', value: String(Math.max(0, level || 0)) }
      ],
      fail: function (error) {
        if (typeof console !== 'undefined' && console.warn) {
          console.warn('同步微信好友等级失败', error);
        }
      }
    });
  },

  WJ_WeChatOpenFriendGiftPage__deps: ['$WJFriendGiftState'],
  WJ_WeChatOpenFriendGiftPage: function (receiverNamePtr, openlinkPtr, minimumVersionPtr) {
    var receiverName = UTF8ToString(receiverNamePtr);
    var openlink = UTF8ToString(openlinkPtr);
    var minimumVersion = UTF8ToString(minimumVersionPtr);
    var send = function (method, value) {
      if (typeof SendMessage === 'function') {
        SendMessage(receiverName, method, value || '');
      } else if (typeof GameGlobal !== 'undefined' && GameGlobal.Module && GameGlobal.Module.SendMessage) {
        GameGlobal.Module.SendMessage(receiverName, method, value || '');
      }
    };
    var versionAtLeast = function (current, minimum) {
      if (!minimum) return true;
      var currentParts = String(current || '').split('.');
      var minimumParts = String(minimum || '').split('.');
      var length = Math.max(currentParts.length, minimumParts.length);
      for (var i = 0; i < length; i++) {
        var currentPart = parseInt(currentParts[i] || '0', 10) || 0;
        var minimumPart = parseInt(minimumParts[i] || '0', 10) || 0;
        if (currentPart > minimumPart) return true;
        if (currentPart < minimumPart) return false;
      }
      return true;
    };

    if (typeof wx === 'undefined' || typeof wx.createPageManager !== 'function') {
      send('OnWeChatFriendGiftPageFailed', '当前微信基础库不支持好友赠礼页面');
      return;
    }
    if (!openlink) {
      send('OnWeChatFriendGiftPageFailed', '服务器未返回好友赠礼 OPENLINK');
      return;
    }

    var sdkVersion = '';
    try {
      sdkVersion = wx.getSystemInfoSync && wx.getSystemInfoSync().SDKVersion
        ? wx.getSystemInfoSync().SDKVersion
        : '';
    } catch (ignored) {}
    if (!versionAtLeast(sdkVersion, minimumVersion)) {
      send('OnWeChatFriendGiftPageFailed', '微信基础库版本过低，需要 ' + minimumVersion + ' 或更高版本');
      return;
    }

    if (WJFriendGiftState.manager && WJFriendGiftState.openlink !== openlink) {
      try { WJFriendGiftState.manager.destroy(); } catch (ignored) {}
      WJFriendGiftState.manager = null;
    }
    if (!WJFriendGiftState.manager) {
      WJFriendGiftState.manager = wx.createPageManager();
      WJFriendGiftState.openlink = openlink;
    }

    var generation = ++WJFriendGiftState.generation;
    var manager = WJFriendGiftState.manager;
    var options = { openlink: openlink };
    Promise.resolve(manager.load(options))
      .then(function () { return manager.show(options); })
      .then(function () {
        if (generation === WJFriendGiftState.generation) {
          send('OnWeChatFriendGiftPageOpened', 'opened');
        }
      })
      .catch(function (error) {
        if (generation === WJFriendGiftState.generation) {
          send(
            'OnWeChatFriendGiftPageFailed',
            error && error.errMsg ? error.errMsg : '微信好友赠礼页面打开失败');
        }
      });
  },

  WJ_WeChatDestroyFriendGiftPage__deps: ['$WJFriendGiftState'],
  WJ_WeChatDestroyFriendGiftPage: function () {
    WJFriendGiftState.generation++;
    if (WJFriendGiftState.manager) {
      try { WJFriendGiftState.manager.destroy(); } catch (ignored) {}
    }
    WJFriendGiftState.manager = null;
    WJFriendGiftState.openlink = '';
  },

  WJ_RealtimeConnect__deps: ['$WJRealtimeState', '$WJRealtimeNotify'],
  WJ_RealtimeConnect: function (receiverNamePtr, urlPtr, authorizationPtr) {
    var receiverName = UTF8ToString(receiverNamePtr);
    var url = UTF8ToString(urlPtr);
    var authorization = UTF8ToString(authorizationPtr);
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
      var task = wx.connectSocket({
        url: url,
        header: { Authorization: authorization }
      });
      WJRealtimeState.socket = task;
      task.onOpen(function () { notifyCurrent('OnRealtimeOpened', 'connected'); });
      task.onMessage(function (event) { notifyCurrent('OnRealtimeMessage', event && event.data ? event.data : ''); });
      task.onClose(function (event) { notifyCurrent('OnRealtimeClosed', event && event.reason ? event.reason : 'closed'); });
      task.onError(function (error) { notifyCurrent('OnRealtimeError', error && error.errMsg ? error.errMsg : 'WebSocket error'); });
      return;
    }

    notifyCurrent(
      'OnRealtimeError',
      'Browser WebSocket cannot send the required Authorization handshake header.'
    );
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
