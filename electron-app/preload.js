const { contextBridge, ipcRenderer } = require('electron');

// Exposed to BOTH the web app (kiosk mode) and corner.html - it is the same webContents, so the
// same preload applies after loadFile. The first five are what the web app's confirm.js and
// session-instructions.js call; the last two are only used by the corner widget.
contextBridge.exposeInMainWorld('examBridge', {
  getDeviceInfo: () => ipcRenderer.invoke('device-info'),
  examConfirmed: (data) => ipcRenderer.send('exam-confirmed', data),
  openExternal:  (url)  => ipcRenderer.send('open-external', url),
  endExam:       ()     => ipcRenderer.send('end-exam'),
  onSessionEnded: (cb)  => ipcRenderer.on('session-ended', cb),

  getCornerData: ()     => ipcRenderer.invoke('corner-data'),
  setLanguage:   (lang) => ipcRenderer.invoke('set-language', lang)
});
