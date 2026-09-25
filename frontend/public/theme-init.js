// Resolve the theme before first paint, so a dark choice never flashes light. <html
// data-theme> always holds the result, light or dark: "follow the system" is resolved
// here, so the CSS has one dark palette, not a second copy under prefers-color-scheme.
// A separate file, not an inline <script>, so the Content-Security-Policy can stay
// script-src 'self' with no hashes or 'unsafe-inline'.
var saved = null
try {
  saved = localStorage.getItem('adaplatform.theme')
} catch {
  // Storage blocked (private mode, site data off): follow the system.
}
var dark = saved === 'dark' || (saved !== 'light' && matchMedia('(prefers-color-scheme: dark)').matches)
document.documentElement.dataset.theme = dark ? 'dark' : 'light'
