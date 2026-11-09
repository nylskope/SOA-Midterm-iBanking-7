const API = {
  user: "http://localhost:5001/api/users",
  tuition: "http://localhost:5002/api/tuitions",
  otp: "http://localhost:5003/api/otps",
  payment: "http://localhost:5004/api/transactions",
};

let state = {
  user: JSON.parse(localStorage.getItem("user")) || null,
  userRowVersion: 1,
  pendingPayment: null,
  pendingFeeIds: [],
};

const otpModal = new bootstrap.Modal(document.getElementById("otpModal"));

window.onload = async () => {
  const urlParams = new URLSearchParams(window.location.search);
  const status = urlParams.get("status");

  if (status) {
    document.getElementById("login-view").classList.add("hidden");
    document.getElementById("dashboard-view").classList.add("hidden");
    document.getElementById("callback-view").classList.remove("hidden");
    processVnpayCallback(urlParams);
    return;
  }

  if (state.user) {
    document.getElementById("login-view").classList.add("hidden");
    document.getElementById("dashboard-view").classList.remove("hidden");

    document.getElementById("user-fullname").innerText = state.user.fullName;
    document.getElementById("payer-name").value = state.user.fullName;
    document.getElementById("payer-phone").value = state.user.phone;
    document.getElementById("payer-email").value = state.user.email;

    await loadDashboard();
  }
};

function processVnpayCallback(urlParams) {
  const status = urlParams.get("status");
  const txn = urlParams.get("txn");
  const alertDiv = document.getElementById("callback-alert");
  const title = document.getElementById("callback-title");
  const msg = document.getElementById("callback-message");
  const spinner = document.getElementById("callback-spinner");
  const countdownContainer = document.getElementById(
    "callback-countdown-container",
  );

  spinner.classList.add("hidden");
  countdownContainer.classList.remove("hidden");

  if (status === "success") {
    alertDiv.className = "alert alert-success p-5 shadow-sm text-center";
    title.innerText = "Giao dịch thành công!";
    msg.innerHTML = `Mã GD: <strong>${txn}</strong><br>Học phí đã được thanh toán thành công qua VNPAY.`;
  } else if (status === "canceled") {
    alertDiv.className = "alert alert-warning p-5 shadow-sm text-center";
    title.innerText = "Giao dịch đã bị hủy!";
    msg.innerHTML = `Mã GD: <strong>${txn}</strong><br>Bạn đã chủ động hủy thanh toán tại cổng VNPAY.`;
  } else {
    let errorReason = "Lỗi hệ thống không xác định.";
    if (status === "conflict")
      errorReason =
        "Giao dịch bị từ chối do xung đột dữ liệu hoặc số dư không đủ.";

    alertDiv.className = "alert alert-danger p-5 shadow-sm text-center";
    title.innerText = "Giao dịch thất bại!";
    msg.innerHTML = `Mã GD: <strong>${txn}</strong><br>${errorReason}`;
  }

  let seconds = 5;
  const interval = setInterval(() => {
    seconds--;
    document.getElementById("callback-countdown").innerText = seconds;
    if (seconds <= 0) {
      clearInterval(interval);
      returnToDashboard();
    }
  }, 1000);
}

function returnToDashboard() {
  window.location.href = "/";
}

async function login() {
  const username = document.getElementById("username").value;
  const password = document.getElementById("password").value;

  try {
    const res = await fetch(`${API.user}/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username, password }),
    });

    if (!res.ok) throw new Error("Login failed");

    const data = await res.json();
    localStorage.setItem("user", JSON.stringify(data.user));
    state.user = data.user;

    location.reload();
  } catch (err) {
    document.getElementById("login-error").classList.remove("hidden");
  }
}

async function loadDashboard() {
  const balanceRes = await fetch(`${API.user}/${state.user.userId}/balance`);
  const balanceData = await balanceRes.json();
  state.userRowVersion = balanceData.rowVersion;
  state.user.balance = balanceData.balance;
  document.getElementById("user-balance").innerText =
    balanceData.balance.toLocaleString() + " VND";

  const historyRes = await fetch(`${API.payment}/history/${state.user.userId}`);
  const historyBody = document.getElementById("history-table-body");
  historyBody.innerHTML = "";
  state.pendingFeeIds = [];

  if (historyRes.ok) {
    const history = await historyRes.json();
    history.forEach((txn) => {
      let badgeClass = "bg-secondary";
      let actionBtn = "";

      if (txn.status === "SUCCESS") badgeClass = "bg-success";
      else if (txn.status === "FAILED") badgeClass = "bg-danger";
      else if (txn.status === "CANCELED") badgeClass = "bg-secondary";
      else if (txn.status === "COMPENSATED") badgeClass = "bg-info text-dark";
      else if (txn.status === "PENDING") {
        badgeClass = "bg-warning text-dark";
        state.pendingFeeIds.push(txn.feeId);
        actionBtn = `<button class="btn btn-sm btn-outline-danger ms-2 py-0" style="font-size: 0.75rem;" onclick="killPendingTxn('${txn.transactionId}')">Hủy</button>`;
      }

      historyBody.innerHTML += `
                <tr>
                    <td class="small">${txn.transactionId}</td>
                    <td>${txn.feeId}</td>
                    <td>${txn.amount.toLocaleString()}</td>
                    <td><span class="badge ${badgeClass}">${txn.status}</span>${actionBtn}</td>
                </tr>
            `;
    });
  }
}

async function lookupTuition() {
  const mssv = document.getElementById("search-mssv").value;
  if (!mssv) return;

  const res = await fetch(`${API.tuition}/${mssv}`);
  const tbody = document.getElementById("tuition-table-body");
  tbody.innerHTML = "";

  if (res.ok) {
    const fees = await res.json();
    fees.forEach((fee) => {
      const hasEnoughBalance = state.user.balance >= fee.amount;
      let btnClass = hasEnoughBalance
        ? "btn-success"
        : "btn-secondary disabled";
      let termsHtml = hasEnoughBalance
        ? `<div class="form-check small"><input class="form-check-input term-check" type="checkbox" id="term_${fee.feeId}" onchange="togglePayBtn('${fee.feeId}')"><label class="form-check-label">Đồng ý điều khoản</label></div>`
        : `<span class="text-danger small">Số dư không đủ</span>`;

      if (state.pendingFeeIds.includes(fee.feeId)) {
        btnClass = "btn-secondary disabled";
        termsHtml = `<span class="text-warning text-dark small fw-bold">Có giao dịch đang chờ (Xem Lịch sử)</span>`;
      }
      tbody.innerHTML += `
                <tr>
                    <td>${fee.mssv}</td>
                    <td>${fee.fullName}</td>
                    <td>${fee.feeId}</td>
                    <td>${fee.amount.toLocaleString()}</td>
                    <td>
                        ${termsHtml}
                        <button id="paybtn_${fee.feeId}" class="btn btn-sm ${btnClass} mt-1"
                            onclick="initiatePayment('${fee.feeId}', ${fee.amount}, ${fee.rowVersion})" disabled>
                            Thanh Toán
                        </button>
                    </td>
                </tr>
            `;
    });
  } else {
    tbody.innerHTML = `<tr><td colspan="5" class="text-center text-muted">Không tìm thấy khoản học phí nào cần nộp.</td></tr>`;
  }
}

function handleSearchKeyPress(event) {
  if (event.key === "Enter") {
    event.preventDefault();
    lookupTuition();
  }
}

function handleLoginKeyPress(event) {
  if (event.key === "Enter") {
    event.preventDefault();
    login();
  }
}

function togglePayBtn(feeId) {
  const checkbox = document.getElementById(`term_${feeId}`);
  const btn = document.getElementById(`paybtn_${feeId}`);
  btn.disabled = !checkbox.checked;
}

function initiatePayment(feeId, amount, tuitionRowVersion) {
  state.pendingPayment = {
    transactionId: `TXN_${Date.now()}`,
    feeId: feeId,
    amount: amount,
    tuitionRowVersion: tuitionRowVersion,
  };

  document.getElementById("otp-step-1").classList.remove("hidden");
  document.getElementById("otp-step-2").classList.add("hidden");
  document.getElementById("payment-error").classList.add("hidden");
  document.getElementById("otp-email-display").innerText = state.user.email;
  document.getElementById("otp-code").value = "";

  otpModal.show();
}

async function requestOtp() {
  document.getElementById("btn-request-otp").disabled = true;

  const res = await fetch(API.otp, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      transactionId: state.pendingPayment.transactionId,
      email: state.user.email,
    }),
  });

  document.getElementById("btn-request-otp").disabled = false;

  if (res.ok) {
    document.getElementById("otp-step-1").classList.add("hidden");
    document.getElementById("otp-step-2").classList.remove("hidden");
  }
}

async function submitPayment() {
  const otpCode = document.getElementById("otp-code").value;
  if (!otpCode) return;

  document.getElementById("btn-submit-payment").disabled = true;
  const errorDiv = document.getElementById("payment-error");
  errorDiv.classList.add("hidden");

  const payload = {
    transactionId: state.pendingPayment.transactionId,
    payerId: state.user.userId,
    feeId: state.pendingPayment.feeId,
    amount: state.pendingPayment.amount,
    otpCode: otpCode,
    userRowVersion: state.userRowVersion,
    tuitionRowVersion: state.pendingPayment.tuitionRowVersion,
    email: state.user.email,
  };

  const res = await fetch(`${API.payment}/pay`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });

  document.getElementById("btn-submit-payment").disabled = false;

  if (res.ok) {
    const data = await res.json();
    window.location.href = data.paymentUrl;
  } else {
    const errorData = await res.json();
    errorDiv.innerText = errorData.message || "Xác thực OTP thất bại.";
    errorDiv.classList.remove("hidden");
    await loadDashboard();
    lookupTuition();
  }
}

function logout() {
  localStorage.removeItem("user");
  location.reload();
}

async function killPendingTxn(transactionId) {
  if (
    !confirm(
      "Xác nhận hủy giao dịch đang treo này? Trạng thái sẽ chuyển sang CANCELED.",
    )
  )
    return;

  await fetch(`${API.payment}/cancel-pending/${transactionId}`, {
    method: "POST",
  });

  await loadDashboard();
  lookupTuition();
}
