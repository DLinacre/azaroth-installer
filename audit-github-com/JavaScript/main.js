// Minimal progressive enhancement for the Azaroth Core landing page.
// No external dependencies. Respects reduced motion and keyboard users.
(function () {
  "use strict";

  // 1. Mark external links to open in a new tab with rel=noopener.
  document.querySelectorAll('a[href^="http"]').forEach(function (a) {
    if (a.hostname && a.hostname !== location.hostname) {
      a.setAttribute("target", "_blank");
      a.setAttribute("rel", "noopener noreferrer");
    }
  });

  // 2. Smooth in-view reveal (disabled when reduced motion is preferred).
  var reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (!reduce && "IntersectionObserver" in window) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (e.isIntersecting) {
          e.target.style.opacity = "1";
          e.target.style.transform = "none";
          io.unobserve(e.target);
        }
      });
    }, { threshold: 0.12 });

    document.querySelectorAll(".card, .steps li, details").forEach(function (el) {
      el.style.opacity = "0";
      el.style.transform = "translateY(12px)";
      el.style.transition = "opacity .4s ease, transform .4s ease";
      io.observe(el);
    });
  }

  // 3. Fetch the latest release version for the download button label (graceful).
  var dl = document.querySelector('a[href*="releases/latest"]');
  if (dl) {
    fetch("https://api.github.com/repos/DLinacre/azaroth-installer/releases/latest")
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (rel) {
        if (rel && rel.tag_name) {
          var label = dl.textContent.replace(/\s*·.*/, "");
          dl.textContent = label + " · " + rel.tag_name;
        }
      })
      .catch(function () { /* offline / rate-limited — leave default label */ });
  }
})();
