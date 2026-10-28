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

  // Live preview on the ticket edit page: the same 3 per mille rule as the weighing app.
  document.querySelectorAll("form[data-ticket-preview]").forEach(function (form) {
    var net = form.querySelector("[name=NetText]");
    var price = form.querySelector("[name=PriceText]");
    var billableOut = form.querySelector("[data-out=billable]");
    var amountOut = form.querySelector("[data-out=amount]");
    var firstKg = parseFloat(form.getAttribute("data-split-first") || "0");
    var secondPrice = parseFloat(form.getAttribute("data-split-price") || "0");
    var fixedPrice = parseFloat(form.getAttribute("data-price") || "0");
    function digits(el) { return el ? parseInt((el.value || "").replace(/[^0-9]/g, ""), 10) || 0 : 0; }
    function billable(kg) { return Math.round(kg - kg / 1000 * 3); }
    function group(n) { return String(n).replace(/\B(?=(\d{3})+(?!\d))/g, "."); }
    function update() {
      var kg = digits(net);
      var unit = price ? digits(price) : fixedPrice;
      var weight, amount;
      if (firstKg > 0 && secondPrice > 0 && firstKg < kg) {
        var a = billable(firstKg), b = billable(kg - firstKg);
        weight = a + b;
        amount = Math.round(a * unit) + Math.round(b * secondPrice);
      } else {
        weight = billable(kg);
        amount = Math.round(weight * unit);
      }
      billableOut.textContent = kg > 0 ? group(weight) : "—";
      amountOut.textContent = kg > 0 && unit > 0 ? group(amount) : "chưa có giá";
    }
    form.addEventListener("input", update);
    update();
  });

  // data-print="1" / "2" prints one sheet of the report; plain data-print prints everything on the page.
  document.querySelectorAll("[data-print]").forEach(function (el) {
    el.addEventListener("click", function () {
      var which = el.getAttribute("data-print") || "all";
      document.body.setAttribute("data-print", which);
      window.print();
      document.body.removeAttribute("data-print");
    });
  });

  // Report preview: one sheet at a time on screen (printing is not affected).
  document.querySelectorAll("[data-view]").forEach(function (btn) {
    btn.addEventListener("click", function () {
      var which = btn.getAttribute("data-view");
      if (which === "all") { document.body.removeAttribute("data-view"); } else { document.body.setAttribute("data-view", which); }
      document.querySelectorAll("[data-view]").forEach(function (b) { b.classList.toggle("on", b === btn); });
    });
  });

  // Report picture: a sheet is drawn at a fixed width so the picture looks the same on a phone and on the desktop.
  var bar = document.querySelector(".preview-bar");
  if (bar) {
    var status = bar.querySelector("[data-image-status]");
    var say = function (text) { if (status) { status.textContent = text; } };
    var baseName = (bar.getAttribute("data-image-name") || "cong-no")
      .normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D")
      .replace(/[^A-Za-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
    var canCopy = !!(navigator.clipboard && window.ClipboardItem && window.isSecureContext);
    var touch = window.matchMedia && window.matchMedia("(pointer: coarse)").matches;

    bar.querySelectorAll('[data-mode="copy"]').forEach(function (b) { b.hidden = !canCopy; });
    if (touch) {
      bar.querySelectorAll('[data-mode="save"]').forEach(function (b) { b.textContent = "Gửi ảnh"; });
    }

    var render = function (which) {
      var sheet = document.querySelector(".sheet-" + which);
      return window.html2canvas(sheet, {
        scale: 2,
        backgroundColor: "#ffffff",
        windowWidth: 1500,
        onclone: function (doc, clone) {
          clone.style.display = "block";
          clone.style.width = "1400px";
          clone.style.maxWidth = "none";
          clone.style.margin = "0";
          clone.style.border = "none";
          clone.style.borderRadius = "0";
          clone.style.fontSize = "17px";
        }
      }).then(function (canvas) {
        return new Promise(function (resolve, reject) {
          canvas.toBlob(function (blob) { blob ? resolve(blob) : reject(new Error("no blob")); }, "image/png");
        });
      });
    };

    bar.querySelectorAll("[data-image]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        var which = btn.getAttribute("data-image");
        var mode = btn.getAttribute("data-mode");
        if (!window.html2canvas) { say("Chưa tải được phần tạo ảnh. Tải lại trang rồi thử lại."); return; }

        var fileName = baseName + "-to" + which + ".png";
        say("Đang tạo ảnh…");
        btn.disabled = true;

        // The clipboard wants its picture handed over as a promise so the click still counts as the user's action.
        var work = mode === "copy" && canCopy
          ? navigator.clipboard.write([new ClipboardItem({ "image/png": render(which) })]).then(function () { say("Đã sao chép ảnh tờ " + which + ". Dán vào Zalo hay tin nhắn."); })
          : render(which).then(function (blob) {
              var file = new File([blob], fileName, { type: "image/png" });
              if (touch && navigator.canShare && navigator.canShare({ files: [file] })) {
                return navigator.share({ files: [file], title: "Công nợ" }).then(function () { say("Đã mở chia sẻ ảnh tờ " + which + "."); });
              }
              var url = URL.createObjectURL(blob);
              var a = document.createElement("a");
              a.href = url;
              a.download = fileName;
              document.body.appendChild(a);
              a.click();
              a.remove();
              setTimeout(function () { URL.revokeObjectURL(url); }, 4000);
              say("Đã lưu ảnh tờ " + which + " (" + fileName + ").");
            });

        work.catch(function (err) {
          say(err && err.name === "AbortError" ? "" : "Không tạo được ảnh. Thử lại hoặc chụp màn hình.");
        }).then(function () { btn.disabled = false; });
      });
    });
  }

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
      }
    } catch (e) { /* storage not available: nothing to remember */ }
    // The bank is needed for a transfer only; cash and offsets can leave it empty.
    var bank = payForm.querySelector("input[name=BankName]");
    var bankHint = payForm.querySelector("[data-bank-required]");
    function syncBank() {
      var transfer = (payForm.querySelector("input[name=Method]:checked") || {}).value === "Transfer";
      if (bank) { bank.required = transfer; }
      if (bankHint) { bankHint.hidden = !transfer; }
    }
    payForm.querySelectorAll("input[name=Method]").forEach(function (radio) { radio.addEventListener("change", syncBank); });
    syncBank();
    payForm.addEventListener("submit", function () {
      try {
        var data = {};
        fields.forEach(function (name) { data[name] = payForm.querySelector("input[name=" + name + "]").value; });
        window.localStorage.setItem(memoryKey, JSON.stringify(data));
      } catch (e) { /* ignore */ }
    });
    update();
  }
})();