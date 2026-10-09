// The clipboard (Clipboard.cs). The game takes the keyboard, so the browser never pastes into it by itself: the page
// (index.html) lets Ctrl/Cmd+V through to the browser and keeps what was pasted in window.sfClipboard; the game picks
// it up here. Copying goes to the browser's clipboard (allowed right after a key press).
var ShadowfallClipboard = {
  SF_TakePaste: function () {
    var c = window.sfClipboard;
    if (!c || !c.pasted) return null;
    var t = c.pasted;
    c.pasted = "";
    var n = lengthBytesUTF8(t) + 1, p = _malloc(n);
    stringToUTF8(t, p, n);
    return p;
  },
  SF_Copy: function (ptr) {
    var t = UTF8ToString(ptr);
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(t); return; }
    } catch (e) {}
    // older browsers: a hidden text area and the copy command
    try {
      var a = document.createElement("textarea");
      a.value = t; a.style.position = "fixed"; a.style.opacity = "0";
      document.body.appendChild(a); a.select(); document.execCommand("copy"); document.body.removeChild(a);
      var canvas = document.getElementById("unity-canvas"); if (canvas) canvas.focus();
    } catch (e) {}
  }
};
mergeInto(LibraryManager.library, ShadowfallClipboard);
