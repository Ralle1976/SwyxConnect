// Type-safe wrapper for window APIs
// This avoids direct window access throughout the codebase

export const windowApi = {
  swyxApi: window.swyxApi,
  windowControls: window.windowControls,
} as const;
