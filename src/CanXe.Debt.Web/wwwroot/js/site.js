(function () {
  function formatMoney(input) {
    var digits = input.value.replace(/\D/g, "");
    input.value = digits.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
  }

  document.querySelectorAll("input[data-money]").forEach(function (input) {
    input.addEventListener("input", function () { formatMoney(input); });
    formatMoney(input);
  });

  document.querySelectorAll("[data-autosubmit]").forEach(function (el) {
    el.addEventListener("change", function () { el.form && el.form.submit(); });
  });

  document.querySelectorAll("[data-print]").forEach(function (el) {
    el.addEventListener("click", function () { window.print(); });
  });

  document.querySelectorAll("[data-fill]").forEach(function (el) {
    el.addEventListener("click", function () {
      var target = document.getElementById(el.getAttribute("data-fill"));
      if (!target) { return; }
      target.value = el.getAttribute("data-value");
      formatMoney(target);
      target.focus();
    });
  });

  document.querySelectorAll("form[data-confirm]").forEach(function (form) {
    form.addEventListener("submit", function (e) {
      if (!window.confirm(form.getAttribute("data-confirm"))) { e.preventDefault(); }
    });
  });
})();
