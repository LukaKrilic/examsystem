const { app, BrowserWindow, screen, desktopCapturer, shell, ipcMain, net, session } = require('electron');
const os = require('os');
const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');
const { machineIdSync } = require('node-machine-id');

// HTTPS, not the http:// of the build-guide sketch: ExamSystem.Web is pinned to https://localhost:8080
// (UseHttpsRedirection + UseHsts, and the SAML EntityId is https://localhost:8080/Saml2). Plain http
// on 8080 is not served at all.
const APP_URL = process.env.EXAM_APP_URL || 'https://localhost:8080';
const API_KEY = process.env.EXAM_API_KEY || 'dev-api-key';
const SCREENSHOT_DIR = process.env.SCREENSHOT_DIR || 'C:\\ExamScreenshots';
const API_HEADERS = { 'Content-Type': 'application/json', 'X-Exam-Api-Key': API_KEY };
const CULTURE_COOKIE = '.AspNetCore.Culture';   // same cookie the web app's /set-language writes

let win;
let allowQuit = false;
let sessionInfo = null;   // filled after Potvrdi (or on resume via the bridge)
let statusPollTimer = null;

// Every backend endpoint this app uses is POST + JSON + X-Exam-Api-Key, so one helper covers all five.
// net.fetch (Chromium's stack) rather than Node's fetch, so it goes through the session below and
// therefore honours the dev-certificate decision made there.
function api(pathname, body) {
  return net.fetch(APP_URL + pathname, {
    method: 'POST', headers: API_HEADERS, body: JSON.stringify(body)
  });
}

// The ASP.NET Core dev certificate is self-signed. Accept it for localhost ONLY - both the kiosk
// window and api() above go through this session, so this one hook covers page loads and API calls.
// -3 = "use Chromium's own verdict", so anything that is not localhost is still verified normally.
function trustDevCertificate() {
  session.defaultSession.setCertificateVerifyProc((request, callback) => {
    callback(request.hostname === 'localhost' ? 0 : -3);
  });
}

function getLocalIp() {
  for (const ifaces of Object.values(os.networkInterfaces()))
    for (const i of ifaces)
      if (i.family === 'IPv4' && !i.internal) return i.address;
  return '127.0.0.1';
}

function getGroup(ip) {                    // last octet: odd -> 1, even -> 2
  const last = parseInt(ip.split('.').pop(), 10);
  return (last % 2 === 1) ? 1 : 2;
}

async function registerDevice() {
  await api('/api/devices/register', {
    clientType: 'electron',
    deviceId: machineIdSync(),
    hostname: os.hostname(),
    localIp: getLocalIp()
  });
}

function createKioskWindow() {
  win = new BrowserWindow({
    kiosk: true, fullscreen: true, autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true, nodeIntegration: false
    }
  });
  win.loadURL(APP_URL);
  win.on('close', e => { if (!allowQuit) e.preventDefault(); }); // can't be closed
}

function quitApp() {
  allowQuit = true;
  win.setClosable(true);
  app.quit();
}

// forcing Chrome specifically: spec says "ako je moguce" - spawn Chrome, fall back to default browser
function openExam() {
  const chrome = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
  try {
    if (fs.existsSync(chrome)) { spawn(chrome, ['https://exam.net/'], { detached: true }); return; }
  } catch (e) { console.error(e); }
  shell.openExternal('https://exam.net/');
}

// after Potvrdi (or resume): leave kiosk, shrink to top-right corner, stay BEHIND other windows
function toCornerMode(data) {
  sessionInfo = data;
  win.setKiosk(false);
  win.setFullScreen(false);
  const { width } = screen.getPrimaryDisplay().workAreaSize;
  const W = 360, H = 260;
  win.setBounds({ x: width - W - 10, y: 10, width: W, height: H });
  win.setAlwaysOnTop(false);
  win.setClosable(false);
  win.loadFile(path.join(__dirname, 'corner.html'));
  win.blur();                             // let other apps come in front
  openExam();
  startStatusPoll();
}

// ---- session status poll: react to midnight auto-close / remote end ----
function startStatusPoll() {
  clearInterval(statusPollTimer);
  statusPollTimer = setInterval(async () => {
    if (!sessionInfo) return;
    try {
      const r = await api('/api/sessions/active', { sessionId: sessionInfo.sessionId });
      if (!r.ok) return;                   // transient error -> try again next tick
      const s = await r.json();
      if (s.status && s.status !== 'ACTIVE') {
        clearInterval(statusPollTimer);    // stop polling while the notice is on screen
        win.webContents.send('session-ended');  // corner.js shows localized notice
        setTimeout(quitApp, 5000);
      }
    } catch (e) { /* offline - never crash */ }
  }, 60000);
}

// ---- screenshots every 40-60 s from APP LAUNCH (per spec) ----
async function takeScreenshot() {
  const { width, height } = screen.getPrimaryDisplay().size;
  const sources = await desktopCapturer.getSources({
    types: ['screen'], thumbnailSize: { width, height }
  });
  const png = sources[0].thumbnail.toPNG();
  fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
  fs.writeFileSync(path.join(SCREENSHOT_DIR, 'shot-' + Date.now() + '.png'), png);
  if (sessionInfo) {                       // also send to backend when session known
    api('/api/screenshots', {
      sessionId: sessionInfo.sessionId,
      timestamp: new Date().toISOString(),
      student: sessionInfo.student,
      exam: sessionInfo.exam,
      image: png.toString('base64')
    }).catch(() => {});                    // never crash the app on network errors
  }
}

function scheduleScreenshot() {
  const delay = 40000 + Math.random() * 20000;   // 40-60 s
  setTimeout(async () => {
    try { await takeScreenshot(); } catch (e) { console.error(e); }
    scheduleScreenshot();
  }, delay);
}

// ---- language: the corner widget shares the web app's culture cookie, so the HR/EN choice the
// student made in the wizard carries into the widget, and a change here carries back ----
async function readLanguage() {
  const [cookie] = await session.defaultSession.cookies.get({ url: APP_URL, name: CULTURE_COOKIE });
  const match = cookie && /c=([^|]+)/.exec(decodeURIComponent(cookie.value));
  return (match && match[1] === 'en') ? 'en' : 'hr';
}

function writeLanguage(lang) {
  return session.defaultSession.cookies.set({
    url: APP_URL, name: CULTURE_COOKIE, value: 'c=' + lang + '|uic=' + lang,
    expirationDate: Math.floor(Date.now() / 1000) + 365 * 24 * 3600
  });
}

async function fetchExamKey(group) {
  if (!sessionInfo) return null;
  const r = await api('/api/exam/access-codes', { examId: sessionInfo.exam.examId });
  if (!r.ok) return null;
  const body = await r.json();
  return group === 1 ? body.accessCodes.group1 : body.accessCodes.group2;
}

// ---- bridge handlers ----
ipcMain.handle('device-info', () => ({
  deviceId: machineIdSync(), hostname: os.hostname(),
  localIp: getLocalIp(), group: getGroup(getLocalIp())
}));
ipcMain.on('exam-confirmed', (_e, data) => toCornerMode(data));   // first confirm AND resume
ipcMain.on('open-external', (_e, url) => shell.openExternal(url));
ipcMain.on('end-exam', async () => {
  try {
    await api('/api/sessions/end', { sessionId: sessionInfo && sessionInfo.sessionId });
  } catch (e) { /* best effort - quit either way; the midnight closer is the backstop */ }
  quitApp();
});

// corner.html is a local file: it can neither read the culture cookie nor hold the API key, so the
// main process resolves both on its behalf.
ipcMain.handle('corner-data', async () => {
  const group = getGroup(getLocalIp());
  let examKey = null;
  try { examKey = await fetchExamKey(group); } catch (e) { console.error(e); }
  return { appUrl: APP_URL, lang: await readLanguage(), group, examKey };
});
ipcMain.handle('set-language', (_e, lang) => writeLanguage(lang));

app.whenReady().then(async () => {
  trustDevCertificate();
  await registerDevice().catch(console.error);
  createKioskWindow();
  scheduleScreenshot();
  // Resume is handled by the web app: after SAML login it redirects an IN_EXAM session to
  // /session/instructions, whose JS calls examBridge.examConfirmed(data) -> toCornerMode with the
  // previous session data. No deviceId guessing here.
});
