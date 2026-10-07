// Display settings for Shadowfall's WebGL build.
var ShadowfallDisplay = {
  // Render resolution as a multiple of the page's CSS pixels (99 = the screen's own ratio, e.g. 2 on Retina).
  // Remembered in localStorage so the page starts at it next time (index.html reads "sf_render_scale"), and applied
  // now by changing the device pixel ratio Unity sizes its canvas with.
  SF_SetRenderScale: function (scale) {
    try { localStorage.setItem("sf_render_scale", String(scale)); } catch (e) {}
    var screenRatio = window.devicePixelRatio || 1;
    var ratio = Math.min(screenRatio, scale);
    try { Module.devicePixelRatio = ratio; } catch (e) {}
    if (typeof window.sfSetPixelRatio === "function") window.sfSetPixelRatio(ratio);
  }
};
mergeInto(LibraryManager.library, ShadowfallDisplay);
