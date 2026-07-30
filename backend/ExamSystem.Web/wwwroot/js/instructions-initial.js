// Instructions gate: Dalje stays disabled until the acceptance checkbox is checked.
document.addEventListener('DOMContentLoaded', function () {
  var cb = document.getElementById('accept');
  var next = document.getElementById('btnNext');
  if (!cb || !next) return;

  next.disabled = !cb.checked;
  cb.addEventListener('change', function () {
    next.disabled = !cb.checked;
  });
});
