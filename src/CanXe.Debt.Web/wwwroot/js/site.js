(function () {
  function formatMoney(input) {
    var digits = input.value.replace(/\D/g, "");
    input.value = digits.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
  }

  function fmt(n) {
    return Math.round(Math.abs(n)).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ".");
  }

  document.querySelectorAll("input[data-money]").forEach(function (input) {
    input.addEventListener("input", function () { formatMoney(input); });
    formatMoney(input);
  });

  // Date boxes: digits only, slashes inserted as you type (27102026 becomes 27/10/2026).
  document.querySelectorAll("input[data-date]").forEach(function (input) {
    input.addEventListener("input", function () {
      var d = input.value.replace(/\D/g, "").slice(0, 8);
      var out = d.slice(0, 2);
      if (d.length > 2) { out += "/" + d.slice(2, 4); }
      if (d.length > 4) { out += "/" + d.slice(4); }
      input.value = out;
    });
  });

  document.addEventListener("click", function (e) {
    document.querySelectorAll("details.menu[open]").forEach(function (menu) {
      if (!menu.contains(e.target)) { menu.removeAttribute("open"); }
    });
  });

  document.querySelectorAll("[data-autosubmit]").forEach(function (el) {
    el.addEventListener("change", function () { el.form && el.form.submit(); });
  });

  document.querySelectorAll("[data-print]").forEach(function (el) {
    el.addEventListener("click", function () { window.print(); });
  });

  document.querySelectorAll("form[data-confirm]").forEach(function (form) {
    form.addEventListener("submit", function (e) {
      if (!window.confirm(form.getAttribute("data-confirm"))) { e.preventDefault(); }
    });
  });

  // Pay form: shows what is left after the typed amount, for the period picked in the list.
  var payForm = document.querySelector("form[data-pay-form]");
  if (payForm) {
    var amount = payForm.querySelector("[data-pay-amount]");
    var period = payForm.querySelector("[data-pay-period]");
    var result = payForm.querySelector("[data-pay-result]");

    var remaining = function () {
      var option = period.options[period.selectedIndex];
      return parseInt(option.getAttribute("data-remaining"), 10) || 0;
    };

    var update = function () {
      var paid = parseInt(amount.value.replace(/\D/g, ""), 10) || 0;
      var left = remaining() - paid;
      result.className = "outcome";
      if (!paid) {
        result.className += " open";
        result.textContent = remaining() > 0
          ? "Kỳ này còn nợ " + fmt(remaining()) + ". Nhập số tiền trả để xem còn lại bao nhiêu."
          : "Nhập số tiền trả.";
      } else if (left > 0) {
        result.className += " owe";
        result.textContent = "Trả " + fmt(paid) + " → còn nợ " + fmt(left) + ", tự chuyển sang kỳ sau.";
      } else if (left === 0) {
        result.className += " done";
        result.textContent = "Trả " + fmt(paid) + " → tất toán kỳ này.";
      } else {
        result.className += " ahead";
        result.textContent = "Trả " + fmt(paid) + " → đã ứng trước " + fmt(left) + ", trừ vào kỳ sau.";
      }
    };

    amount.addEventListener("input", update);
    period.addEventListener("change", update);
    payForm.querySelectorAll("[data-pay-all]").forEach(function (button) {
      button.addEventListener("click", function () {
        if (remaining() > 0) { amount.value = String(remaining()); formatMoney(amount); }
        update();
        amount.focus();
      });
    });
    payForm.querySelectorAll("[data-pay-round]").forEach(function (button) {
      button.addEventListener("click", function () {
        var step = parseInt(button.getAttribute("data-pay-round"), 10);
        var rounded = Math.floor(remaining() / step) * step;
        if (rounded > 0) { amount.value = String(rounded); formatMoney(amount); }
        update();
        amount.focus();
      });
    });
    update();
  }
})();
