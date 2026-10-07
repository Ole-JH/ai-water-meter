// The pre-deploy browser check (GameCheck.cs, tools/browser-check): the game reports each step to window.sfCheck,
// and the browser marks a "shot:" step as acked once it has taken its screenshot.
var ShadowfallCheck = {
  SF_CheckReport: function (stagePtr, detailPtr) {
    var stage = UTF8ToString(stagePtr), detail = UTF8ToString(detailPtr);
    var c = window.sfCheck || (window.sfCheck = { log: [], errors: [], acked: "" });
    if (stage === "error") { c.errors.push(detail); return; }
    c.stage = stage;
    c.detail = detail;
    c.log.push({ t: Date.now(), stage: stage, detail: detail });
  },
  SF_CheckAcked: function (stagePtr) {
    var c = window.sfCheck;
    return c && c.acked === UTF8ToString(stagePtr) ? 1 : 0;
  }
};
mergeInto(LibraryManager.library, ShadowfallCheck);
