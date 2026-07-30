// Potvrdi: locks the button, calls /api/sessions/confirm, then branches on whether we're
// running inside the Electron kiosk (window.examBridge) or an ordinary online browser.
document.addEventListener('DOMContentLoaded', function () {
  var btn = document.getElementById('btnConfirm');
  var errorEl = document.getElementById('confirmError');
  if (!btn) return;

  btn.addEventListener('click', async function () {
    btn.disabled = true;
    if (errorEl) errorEl.hidden = true;

    try {
      var deviceId = null;
      var groupNo = null;
      if (window.examBridge) {
        var info = await window.examBridge.getDeviceInfo();
        deviceId = info.deviceId;
        groupNo = info.group;
      }

      var res = await fetch('/api/sessions/confirm', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ deviceId: deviceId, groupNo: groupNo })
      });
      if (!res.ok) throw new Error('confirm failed: ' + res.status);
      var data = await res.json();

      if (window.examBridge) {
        window.examBridge.examConfirmed(data);   // Electron: shrink to corner, open exam.net
      } else {
        location.href = '/session/instructions'; // online: during-exam instructions
      }
    } catch (e) {
      btn.disabled = false;
      if (errorEl) errorEl.hidden = false;
    }
  });
});
