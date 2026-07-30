// During-exam instructions page:
//  - on load, if running inside Electron (window.examBridge), hand the current session back to
//    the corner widget — covers resume after an app restart/relogin mid-exam.
//  - "Zavrsi ispit" always confirms first with the exact spec dialog text, then either lets
//    Electron's main process end the session and quit, or (online) ends it here and redirects
//    to the completion page.
document.addEventListener('DOMContentLoaded', function () {
  var dataEl = document.getElementById('examSessionData');
  var sessionId = dataEl ? dataEl.dataset.sessionId : null;

  if (window.examBridge && sessionId) {
    fetch('/api/sessions/active', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sessionId: sessionId })
    })
      .then(function (r) { return r.json(); })
      .then(function (data) { window.examBridge.examConfirmed(data); })
      .catch(function () { /* offline — never crash */ });
  }

  var endBtn = document.getElementById('btnEndExam');
  if (!endBtn) return;

  endBtn.addEventListener('click', async function () {
    if (!confirm(endBtn.dataset.confirmText)) return;

    if (window.examBridge) {
      window.examBridge.endExam();   // main process posts /api/sessions/end and quits
      return;
    }

    endBtn.disabled = true;
    try {
      await fetch('/api/sessions/end', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sessionId: sessionId })
      });
    } catch (e) { /* best effort — still move on to the completion page */ }
    location.href = '/session/completed';
  });
});
