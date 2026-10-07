// Sends the game's exceptions and logged errors to the server, through the page's window.sfReport (index.html).
var ShadowfallReport = {
  SF_ReportError: function (kindPtr, msgPtr, stackPtr) {
    if (typeof window.sfReport === "function") window.sfReport(UTF8ToString(kindPtr), UTF8ToString(msgPtr), UTF8ToString(stackPtr));
  }
};
mergeInto(LibraryManager.library, ShadowfallReport);
