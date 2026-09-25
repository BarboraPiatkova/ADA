// Apply the saved theme before first paint, so a dark choice never flashes light.
// A separate file, not an inline <script>, so the Content-Security-Policy can stay
// script-src 'self' with no hashes or 'unsafe-inline'.
try {
  var theme = localStorage.getItem('adaplatform.theme')
  if (theme === 'light' || theme === 'dark') document.documentElement.dataset.theme = theme
} catch {
  // Storage blocked (private mode, site data off): the system theme applies.
}
