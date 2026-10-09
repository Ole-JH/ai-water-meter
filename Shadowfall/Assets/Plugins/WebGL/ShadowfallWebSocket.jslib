// Browser WebSocket bridge for Shadowfall (used only in WebGL builds).
// Incoming messages are queued in JS and polled from C# once per frame.
var ShadowfallWebSocket = {
  $sfws: { socket: null, queue: [] },

  SFWS_Connect: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    if (!url) {
      // Default: the same server that served this page.
      var proto = window.location.protocol === "https:" ? "wss://" : "ws://";
      url = proto + window.location.host + "/ws";
    }
    if (sfws.socket) { try { sfws.socket.close(); } catch (e) {} }
    sfws.queue = [];
    try {
      sfws.socket = new WebSocket(url);
    } catch (e) {
      sfws.queue.push("__error:" + e.message);
      return;
    }
    var sock = sfws.socket;
    sock.onopen = function () { if (sock === sfws.socket) sfws.queue.push("__open"); };
    sock.onmessage = function (e) { if (sock === sfws.socket && typeof e.data === "string") sfws.queue.push(e.data); };
    sock.onerror = function () { if (sock === sfws.socket) sfws.queue.push("__error:could not reach the game server"); };
    sock.onclose = function (e) { if (sock === sfws.socket) sfws.queue.push("__close:" + (e.reason || ("code " + e.code))); };
  },

  SFWS_Send: function (msgPtr) {
    if (sfws.socket && sfws.socket.readyState === 1) sfws.socket.send(UTF8ToString(msgPtr));
  },

  SFWS_Close: function () {
    if (sfws.socket) { var s = sfws.socket; sfws.socket = null; try { s.close(); } catch (e) {} }
    sfws.queue = [];
  },

  // Reloads the page for a new game build, bypassing cached files (?v=<build>). Returns 0 when the page was
  // already reloaded for that build (the browser still serves an old copy), so the game can explain instead.
  SFWS_Reload: function (buildPtr) {
    var build = UTF8ToString(buildPtr);
    try {
      var url = new URL(window.location.href);
      if (url.searchParams.get("v") === build) return 0;
      url.searchParams.set("v", build);
      url.searchParams.delete("reset");
      window.location.replace(url.toString());
      return 1;
    } catch (e) { return 0; }
  },

  SFWS_Poll: function () {
    if (sfws.queue.length === 0) return 0;
    var str = sfws.queue.shift();
    var size = lengthBytesUTF8(str) + 1;
    var buffer = _malloc(size);
    stringToUTF8(str, buffer, size);
    return buffer;
  }
};

autoAddDeps(ShadowfallWebSocket, "$sfws");
mergeInto(LibraryManager.library, ShadowfallWebSocket);
