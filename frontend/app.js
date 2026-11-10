const API = {
  user: "http://localhost:5001/api/users",
  tuition: "http://localhost:5002/api/tuitions",
  otp: "http://localhost:5003/api/otps",
  payment: "http://localhost:5004/api/transactions",
};

let state = {
  user: JSON.parse(localStorage.getItem("user")) || null,
  userRowVersion: 1,
  stagedPayment: null, // Temporary hold before terms are accepted
  pendingPayment: null,
  history: []
};

const termsModal = new bootstrap.Modal(document.getElementById("termsModal"));
const otpModal = new bootstrap.Modal(document.getElementById("otpModal"));

window.onload = async () => {
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
  document.getElementById("user-balance").innerText = balanceData.balance.toLocaleString() + " VND";

  const historyRes = await fetch(`${API.payment}/history/${state.user.userId}`);
  const historyBody = document.getElementById("history-table-body");
  historyBody.innerHTML = "";

  if (historyRes.ok) {
    state.history = await historyRes.json();
    state.history.forEach((txn) => {
      let badgeClass = "bg-secondary";

      if (txn.status === "SUCCESS") badgeClass = "bg-success";
      else if (txn.status === "FAILED") badgeClass = "bg-danger";
      else if (txn.status === "CANCELED") badgeClass = "bg-secondary";
      else if (txn.status === "COMPENSATED") badgeClass = "bg-info text-dark";
      else if (txn.status === "PENDING") badgeClass = "bg-warning text-dark";

      let actionBtn = txn.status === "PENDING"
        ? `<button class="btn btn-sm btn-outline-danger ms-2 py-0 px-1" style="font-size: 0.75rem;" onclick="cancelTransactionFromHistory('${txn.transactionId}')">Hủy</button>`
        : "";

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
      const isFeePending = state.history.some(t => t.feeId === fee.feeId && t.status === "PENDING");
      const hasEnoughBalance = state.user.balance >= fee.amount;

      let btnClass = "";
      let termsHtml = "";
      let btnText = "Thanh Toán";

      if (isFeePending) {
        btnClass = "btn-warning disabled";
        termsHtml = `<span class="text-warning small fw-bold">Đang có GD chờ xử lý</span>`;
        btnText = "Đang xử lý";
      } else if (!hasEnoughBalance) {
        btnClass = "btn-secondary disabled";
        termsHtml = `<span class="text-danger small">Số dư không đủ</span>`;
      } else {
        btnClass = "btn-primary";
      }

      tbody.innerHTML += `
                <tr>
                    <td>${fee.mssv}</td>
                    <td>${fee.fullName}</td>
                    <td>${fee.feeId}</td>
                    <td>${fee.amount.toLocaleString()}</td>
                    <td>
                        ${termsHtml}
                        <button class="btn btn-sm ${btnClass} mt-1"
                            onclick="stagePayment('${fee.feeId}', ${fee.amount}, ${fee.rowVersion})" ${isFeePending || !hasEnoughBalance ? 'disabled' : ''}>
                            ${btnText}
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

// 1. Stage Payment and Request Terms Acceptance
function stagePayment(feeId, amount, tuitionRowVersion) {
  state.stagedPayment = {
      feeId: feeId,
      amount: amount,
      tuitionRowVersion: tuitionRowVersion
  };
  termsModal.show();
}

// 2. Terms Accepted - Initialize PENDING Transaction & Request OTP
async function acceptTermsAndInitPayment() {
  termsModal.hide();

  if (!state.stagedPayment) return;

  const transactionId = `TXN_${Date.now()}`;

  // Register as PENDING globally to lock the fee for this user
  const initRes = await fetch(`${API.payment}/init`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
          transactionId: transactionId,
          userId: state.user.userId,
          feeId: state.stagedPayment.feeId,
          amount: state.stagedPayment.amount
      })
  });

  if (!initRes.ok) {
      const data = await initRes.json();
      alert(data.message || "Không thể khởi tạo giao dịch.");
      await loadDashboard();
      lookupTuition();
      return;
  }

  // Transaction successfully locked, move from staged to pending
  state.pendingPayment = {
    transactionId: transactionId,
    feeId: state.stagedPayment.feeId,
    amount: state.stagedPayment.amount,
    tuitionRowVersion: state.stagedPayment.tuitionRowVersion,
  };
  state.stagedPayment = null;

  // Request the OTP Email
  await fetch(API.otp, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      transactionId: state.pendingPayment.transactionId,
      email: state.user.email,
    }),
  });

  // Update UI and Show OTP Modal
  await loadDashboard();
  lookupTuition();

  document.getElementById("payment-error").classList.add("hidden");
  document.getElementById("otp-email-display").innerText = state.user.email;
  document.getElementById("otp-code").value = "";

  otpModal.show();
}

// 3. Submit OTP - Executes the Two-Phase Commit Saga
async function submitPayment() {
  const otpCode = document.getElementById("otp-code").value;
  if (!otpCode) return;

  const btn = document.getElementById("btn-submit-payment");
  btn.disabled = true;
  btn.innerText = "Đang xử lý...";

  const errorDiv = document.getElementById("payment-error");
  errorDiv.classList.add("hidden");

  // Fetch the absolute latest User RowVersion to prevent premature OCC rejection
  const freshBalanceRes = await fetch(`${API.user}/${state.user.userId}/balance`);
  if (freshBalanceRes.ok) {
      const freshData = await freshBalanceRes.json();
      state.userRowVersion = freshData.rowVersion;
  }

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

  btn.disabled = false;
  btn.innerText = "Thanh Toán";

  if (res.ok) {
    otpModal.hide();
    state.pendingPayment = null;

    const alertDiv = document.getElementById("payment-result-alert");
    alertDiv.className = "alert alert-success text-center mb-4";
    alertDiv.innerHTML = `<strong>Thành công!</strong> Đã thanh toán học phí.`;
    alertDiv.classList.remove("hidden");

    setTimeout(() => { alertDiv.classList.add("hidden"); }, 5000);

  } else {
    const errorData = await res.json();
    errorDiv.innerText = errorData.message || "Giao dịch thất bại.";
    errorDiv.classList.remove("hidden");
  }

  await loadDashboard();
  lookupTuition();
}

// Global Cancellation Methods
async function cancelPendingPayment() {
  if (state.pendingPayment) {
      await fetch(`${API.payment}/cancel/${state.pendingPayment.transactionId}`, { method: "POST" });
      state.pendingPayment = null;
      await loadDashboard();
      lookupTuition();
  }
}

async function cancelTransactionFromHistory(txId) {
  if(confirm("Bạn có chắc chắn muốn hủy giao dịch này không?")) {
      await fetch(`${API.payment}/cancel/${txId}`, { method: "POST" });
      await loadDashboard();
      lookupTuition();
  }
}

function logout() {
  localStorage.removeItem("user");
  location.reload();
}
