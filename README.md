# iBanking Tuition Module - SOA Midterm Project

A microservices-based web application developed for the Service-Oriented Architecture (SOA) Midterm Project at Ton Duc Thang University. 

The system simulates a bank's tuition payment portal, allowing users to securely query and pay university tuition fees using their internal balance. It features a containerized backend, distributed transaction management (Saga Pattern & Two-Phase Commit), Optimistic Concurrency Control (OCC), and real-time integration with external APIs (Brevo API).

## Team Members
- **Trần Quang Duy** – 524H0012
- **Lý Tín Hòa** – 524H0015
- **Hà Dư Tuấn** – 524H0134

## Prerequisites
Before running this project, ensure you have the following installed and configured:
1. **Docker & Docker Compose:** Essential for cross-platform and quick deployment.
2. **.NET 10.0 SDK:** Required to run or build the project.
3. **Brevo API Account:** For OTP and digital receipt email dispatch.
    - Create a free account and generate an API key at [Brevo](https://www.brevo.com/).

*Note: For ease of use, Brevo API Key has been temporarily pre-configured in the `.env` file*

---

## Installation & Setup

**Step 1: Clone the repository**
```bash
git clone https://github.com/nylskope/SOA-Midterm-iBanking-7.git
cd SOA-Midterm-iBanking-7
```

**Step 2: Configure Environment Variables**
In the root directory of the project (where `docker-compose.yml` is located), create a `.env` file. Do not use quotes around the values. Fill in your external API credentials:
```env
JWT_SECRET=your_jwt_secret
BREVO_API_KEY=your_brevo_api_key_here
BREVO_FROM_EMAIL=your_verified_brevo_sender_email@example.com
```
*Note: The frontend runs on `http://localhost:3000`.*

**Step 3: Run the System**
We have provided initialization scripts that automatically tear down old volumes, rebuild the .NET 10 images, and spin up the PostgreSQL and Redis containers.
- **On Windows:** Double-click `run.bat` or execute it in Command Prompt.
- **On Linux:**
```bash
sudo chmod +x run.sh
./run.sh
```
- **On macOS:** Double-click `run.command` or execute it in Terminal.

*Note: For macOS, the script has already been chmod +x run.command, but if you can't double-click the script then you should open your Terminal and run the command:*
```bash
sudo chmod +x run.command
```

Once running, access the application at `http://localhost:3000`.

---

## Testing & Demo Scenarios
The `db-init/init.sql` SQL script automatically seeds the PostgreSQL database with the exact data needed to test the scenarios.

### Email Configuration

Before testing, proceed into the `db-init/init.sql` SQL script, scroll down to the INSERTS, and change the three placeholders (your_email@gmail.com) to your actual emails to receive OTP codes and receipts.

### NCB Dummy Card

- Card ID: 9704198526191432198
- Name: NGUYEN VAN A
- Date: 07/15

### Test Accounts
| User Role | Username | Password | Notes |
| :--- | :--- | :--- | :--- |
| **User 1** | `student123` | `password_string` | Balance: 50,000,000 VND |
| **User 2** | `student124` | `password_string` | Balance: 15,000,000 VND |
| **User 3** | `student125` | `password_string` | Balance: 5,000,000 VND  |

### Scenario 1: Happy Path (End-to-End Workflow)
1. Log in as **User 1** (`student123`).
2. Query for MSSV: `123H4567`.
3. Select fee `F_HAPPY` (5,000,000 VND), tick the box to accept terms, and click "Thanh Toán".
4. Check your email inbox (that was configured in the database) for the 6-digit OTP, and enter the OTP as prompted.
5. Enter the card information as prompted.
7. Enter another OTP (will be sent automatically).
7. **Observation:** You are redirected back to the UI. The balance is successfully deducted, the fee is marked as paid, and the transaction history shows `SUCCESS`. A receipt email is asynchronously dispatched.

### Scenario 2: Concurrency Handling - One User

#### Concurrency 1: Simultaneous Transactions
*Requirement: Ensure multiple fees can be paid simultaneously on the same account.*
1. Open two separate browser tabs (one normal tab and one Incognito tab).
2. Log in as **User 2** (`student124`) on both tabs who has exactly **15,000,000 VND**.
3. Query for MSSV `199H0011` on both tabs. There are two fees: `F_SIMUL_1` and `F_SIMUL_2` (both 5,000,000 VND). Ignoring `F_OVER_1` and `F_OVER_2` for now.
4. Initiate payment for `F_SIMUL_1` and `F_SIMUL_2` on Tab A and Tab B respectively, pausing both at the OTP screen.
5. Complete the payment in Tab A by entering the OTP.
6. Then do the same for Tab B.
7. **Observation:** Both tabs will succeed.

#### Concurrency 2: Overdraft Protection
*Requirement: Prevent account balance from dropping below zero during simultaneous transactions.*
1. Open two separate browser tabs (one normal tab and one Incognito tab).
2. Log in as **User 2** (`student124`) on both tabs who should now have exactly **5,000,000 VND** once you have done **Concurrency 1**.
3. Query for MSSV `199H0011` on both tabs. There should now be only two fees: `F_OVER_1` and `F_OVER_2`.
4. Initiate payment for `F_OVER_1` and `F_OVER_2` on Tab A and Tab B respectively, pausing both at the OTP screen.
5. Complete the payment in Tab A by entering the OTP (Balance becomes 0).
6. Then do the same for Tab B.
7. **Observation:** Tab B will fail. Even though the payment succeeded, the User Service's physical database constraint (`CHK_PositiveBalance`) blocked the internal deduction. The Saga orchestrator marks the transaction as `FAILED`.

### Scenario 3: Concurrency Handling - Two Users

#### Concurrency 3 - Double Payment Protection
*Requirement: Ensure a fee can only be paid once if two users try at the same time.*
1. Open two separate browser tabs (one normal tab and one Incognito tab).
2. Tab A: Log in as **User 1** (`student123`).
3. Tab B: Log in as **User 3** (`student125`).
4. In both tabs, search for MSSV `201H0022` and initiate payment for fee `F_DOUBLE`.
5. Enter the respective OTPs and proceed to the card information prompt screen in both tabs.
6. **Tab A:** Complete the payment first. There should be a success screen.
7. **Tab B:** Complete the payment after Tab A.
8. **Observation:** Tab B will display a transaction failure screen. The Orchestrator detected a stale `RowVersion` via Optimistic Concurrency Control (OCC) and aborted the database commit. Check User 3's transaction history to see the `COMPENSATED` status, proving the auto-refund Saga executed successfully.

---

## System Architecture & Ports
The system relies on Docker's internal networking for inter-service communication, exposing the following ports to the host:
- **Frontend SPA (Nginx):** `:3000`
- **User Service:** `:5001` (Authentication & Balance Management)
- **Tuition Service:** `:5002` (Tuition Fees & Student Data)
- **OTP Service:** `:5003` (Redis interaction & Brevo Mail dispatch)
- **Payment Orchestrator:** `:5004` (Saga Conductor & IPN Webhook Handler)
- **PostgreSQL:** `:5432` (Relational persistence)
- **Redis:** `:6379` (Ephemeral OTP caching)

## Heads-up
- **Logical References:** To adhere strictly to microservices best practices, there are no physical Foreign Keys linking the `Transactions` table to the `Users` or `TuitionFees` tables. They use logical ID references to maintain domain autonomy.
- **Manual Kill Switch:** A manual Cancel button is provided in the UI for transactions stuck in the `PENDING` state. This simulates a dead-letter queue cleanup process for transactions abandoned midway.
- **Separate Tabs Testing**: To properly evaluate the backend's Optimistic Concurrency Control (OCC) and Saga rollback mechanisms for overlapping transactions, *you must use two distinct browsers (e.g., Chrome and Edge) or one normal and one Incognito window.*
