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

  // Picking trucks for a period: all / none / a run of tickets / everything up to a day, with a live total.
  document.querySelectorAll("form[data-close-form]").forEach(function (form) {
    var boxes = Array.prototype.slice.call(form.querySelectorAll("input[data-pick]"));
    var summary = form.querySelector("[data-sel-summary]");
    var carry = parseInt(summary.getAttribute("data-carry"), 10) || 0;

    function refresh() {
      var count = 0, kg = 0, amount = 0;
      boxes.forEach(function (box) {
        if (box.checked) {
          count += 1;
          kg += parseFloat(box.getAttribute("data-kg")) || 0;
          amount += parseInt(box.getAttribute("data-amount"), 10) || 0;
        }
      });
      if (count === 0) { summary.textContent = "Chưa chọn xe nào."; return; }
      var payable = carry + amount;
      summary.textContent = "Đã chọn " + count + " xe · " + fmt(kg) + " kg · tiền hàng " + fmt(amount) +
        (carry !== 0 ? " · cộng nợ trước " + (carry < 0 ? "−" : "") + fmt(carry) : "") +
        " → phải trả " + (payable < 0 ? "−" : "") + fmt(payable);
    }

    function setAll(checked) { boxes.forEach(function (box) { box.checked = checked; }); refresh(); }

    form.querySelectorAll("[data-select]").forEach(function (button) {
      button.addEventListener("click", function () {
        var mode = button.getAttribute("data-select");
        if (mode === "all") { setAll(true); }
        else if (mode === "none") { setAll(false); }
        else if (mode === "range") {
          var a = parseInt(form.querySelector("[data-range-from]").value, 10);
          var b = parseInt(form.querySelector("[data-range-to]").value, 10);
          var lo = Math.min(a, b), hi = Math.max(a, b);
          boxes.forEach(function (box, i) { box.checked = i >= lo && i <= hi; });
          refresh();
        } else if (mode === "until") {
          var m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(form.querySelector("[data-select-until]").value.trim());
          if (!m) { summary.textContent = "Nhập ngày dạng 27/10/2026."; return; }
          var limit = m[3] + "-" + m[2] + "-" + m[1];
          boxes.forEach(function (box) { box.checked = box.getAttribute("data-day") <= limit; });
          refresh();
        }
      });
    });
    boxes.forEach(function (box) { box.addEventListener("change", refresh); });
    refresh();
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

  // Pay form: type an amount (or tap 50/100/200/500 triệu) and see at once what is left to owe.
  var payForm = document.querySelector("form[data-pay-form]");
  if (payForm) {
    var amount = payForm.querySelector("[data-pay-amount]");
    var period = payForm.querySelector("[data-pay-period]");
    var result = payForm.querySelector("[data-pay-result]");

    var remaining = function () {
      var option = period.options[period.selectedIndex];
      return parseInt(option.getAttribute("data-remaining"), 10) || 0;
    };
    var label = function () {
      return period.options[period.selectedIndex].getAttribute("data-label") || "Đang nợ";
    };

    var update = function () {
      var paid = parseInt(amount.value.replace(/\D/g, ""), 10) || 0;
      var left = remaining() - paid;
      result.className = "outcome";
      if (!paid) {
        result.className += " open";
        result.textContent = remaining() > 0
          ? label() + " " + fmt(remaining()) + ". Nhập số tiền hoặc bấm số tiền nhanh."
          : "Nhập số tiền cần ghi nhận.";
      } else if (left > 0) {
        result.className += " owe";
        result.textContent = "Trả " + fmt(paid) + " → còn nợ " + fmt(left) + " (trả sau).";
      } else if (left === 0) {
        result.className += " done";
        result.textContent = "Trả " + fmt(paid) + " → đã trả đủ.";
      } else {
        result.className += " ahead";
        result.textContent = "Trả " + fmt(paid) + " → nhiều hơn số đang nợ " + fmt(left) + ". Phần dư tính là trả trước, trừ vào các xe sau.";
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
    payForm.querySelectorAll("[data-pay-set]").forEach(function (button) {
      button.addEventListener("click", function () {
        amount.value = button.getAttribute("data-pay-set");
        formatMoney(amount);
        update();
        amount.focus();
      });
    });

    // Remember how this person paid last time (method, bank, who gave, who received) so the next entry starts filled in.
    var memoryKey = "canxe-debt-last-payment";
    var fields = ["BankName", "PaidBy", "ReceivedBy"];
    try {
      var saved = JSON.parse(window.localStorage.getItem(memoryKey) || "null");
      if (saved && !payForm.querySelector("input[name=BankName]").value && !payForm.querySelector("input[name=ReceivedBy]").value) {
        fields.forEach(function (name) {
          var input = payForm.querySelector("input[name=" + name + "]");
          if (input && saved[name]) { input.value = saved[name]; }
        });
        var radio = payForm.querySelector("input[name=Method][value=" + saved.Method + "]");
        if (radio) { radio.checked = true; }
      }
    } catch (e) { /* storage not available: nothing to remember */ }
    payForm.addEventListener("submit", function () {
      try {
        var data = { Method: (payForm.querySelector("input[name=Method]:checked") || {}).value || "Cash" };
        fields.forEach(function (name) { data[name] = payForm.querySelector("input[name=" + name + "]").value; });
        window.localStorage.setItem(memoryKey, JSON.stringify(data));
      } catch (e) { /* ignore */ }
    });
    update();
  }
})();