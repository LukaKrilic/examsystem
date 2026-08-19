// Corner widget. Runs in the same webContents as the kiosk web app, so window.examBridge is the
// same preload bridge. It holds no API key and no session data of its own - main.js resolves the
// exam key (via /api/exam/access-codes, picked by IP-parity group) and the current language.

// EN wording is the spec's own ("Instructions / Exam.net / Practical exam results / End Exam");
// the end-exam confirmation and the ended notice reuse the web app's SharedResource wording so the
// student reads the same sentence here as in the browser.
var STRINGS = {
  hr: {
    keyLabel: 'Ključ za ispit',
    copy: 'Kopiraj',
    copied: 'Kopirano',
    instructions: 'Upute',
    examNet: 'Exam.net',
    results: 'Rezultati praktičnog ispita',
    endExam: 'Završi ispit',
    confirmEnd: 'Jesi li siguran da želiš završiti ispit?',
    endedTitle: 'Ispit završen',
    endedMessage: 'Vaš ispit je završen. Aplikacija će se zatvoriti.'
  },
  en: {
    keyLabel: 'Student exam key',
    copy: 'Copy',
    copied: 'Copied',
    instructions: 'Instructions',
    examNet: 'Exam.net',
    results: 'Practical exam results',
    endExam: 'End Exam',
    confirmEnd: 'Are you sure you want to end the exam?',
    endedTitle: 'Exam finished',
    endedMessage: 'Your exam has been finished. The application will now close.'
  }
};

var lang = 'hr';
var appUrl = '';
var examKey = null;
var copyResetTimer = null;

function t() { return STRINGS[lang]; }

function el(id) { return document.getElementById(id); }

function applyLanguage(next) {
  lang = (next === 'en') ? 'en' : 'hr';
  var s = t();
  document.documentElement.lang = lang;
  clearTimeout(copyResetTimer);           // a pending "Kopirano" must not survive the switch
  el('keyLabel').textContent = s.keyLabel;
  el('btnCopy').textContent = s.copy;
  el('btnInstructions').textContent = s.instructions;
  el('btnExamNet').textContent = s.examNet;
  el('btnResults').textContent = s.results;
  el('btnEnd').textContent = s.endExam;
  el('endedTitle').textContent = s.endedTitle;
  el('endedMessage').textContent = s.endedMessage;
  el('langHr').setAttribute('aria-pressed', String(lang === 'hr'));
  el('langEn').setAttribute('aria-pressed', String(lang === 'en'));
}

function setLanguage(next) {
  applyLanguage(next);
  window.examBridge.setLanguage(lang);    // keep the web app's culture cookie in sync
}

function copyKey() {
  if (!examKey) return;
  var btn = el('btnCopy');
  navigator.clipboard.writeText(examKey).then(function () {
    clearTimeout(copyResetTimer);
    btn.textContent = t().copied;
    copyResetTimer = setTimeout(function () { btn.textContent = t().copy; }, 1500);
  }).catch(function () { /* clipboard refused - the key stays selectable in the box */ });
}

document.addEventListener('DOMContentLoaded', function () {
  var bridge = window.examBridge;

  el('langHr').addEventListener('click', function () { setLanguage('hr'); });
  el('langEn').addEventListener('click', function () { setLanguage('en'); });
  el('btnCopy').addEventListener('click', copyKey);

  el('btnInstructions').addEventListener('click', function () {
    bridge.openExternal(appUrl + '/session/instructions');
  });
  el('btnExamNet').addEventListener('click', function () {
    bridge.openExternal('https://exam.net/');
  });
  el('btnResults').addEventListener('click', function () {
    bridge.openExternal('https://results.vua.cloud/');
  });
  el('btnEnd').addEventListener('click', function () {
    if (confirm(t().confirmEnd)) bridge.endExam();
  });

  // main.js sends this when the 60 s poll sees the session is no longer ACTIVE (Zavrsi ispit from
  // another device, or the midnight auto-close). It quits ~5 s later.
  bridge.onSessionEnded(function () { document.body.classList.add('ended'); });

  bridge.getCornerData().then(function (data) {
    appUrl = data.appUrl;
    examKey = data.examKey;
    el('examKey').textContent = examKey || '—';
    applyLanguage(data.lang);
  }).catch(function () { applyLanguage('hr'); });
});
