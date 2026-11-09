CREATE TABLE "Users" (
    "UserId" VARCHAR(50) PRIMARY KEY,
    "Username" VARCHAR(50) UNIQUE NOT NULL,
    "PasswordHash" VARCHAR(255) NOT NULL,
    "FullName" VARCHAR(100) NOT NULL,
    "Phone" VARCHAR(15),
    "Email" VARCHAR(100),
    "Balance" NUMERIC(18, 2) NOT NULL DEFAULT 0.00,
    "RowVersion" INT NOT NULL DEFAULT 1,
    CONSTRAINT "CHK_PositiveBalance" CHECK ("Balance" >= 0)
);

CREATE TABLE "Students" (
    "Mssv" VARCHAR(20) PRIMARY KEY,
    "FullName" VARCHAR(100) NOT NULL
);

CREATE TABLE "TuitionFees" (
    "FeeId" VARCHAR(50) PRIMARY KEY,
    "Mssv" VARCHAR(20) NOT NULL REFERENCES "Students"("Mssv"),
    "Amount" NUMERIC(18, 2) NOT NULL,
    "IsPaid" BOOLEAN NOT NULL DEFAULT FALSE,
    "RowVersion" INT NOT NULL DEFAULT 1
);

CREATE TABLE "Transactions" (
    "TransactionId" VARCHAR(50) PRIMARY KEY,
    "UserId" VARCHAR(50) NOT NULL,
    "FeeId" VARCHAR(50) NOT NULL,
    "Amount" NUMERIC(18, 2) NOT NULL,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "Status" VARCHAR(20) NOT NULL,
    CONSTRAINT "CHK_Status" CHECK ("Status" IN ('PENDING', 'SUCCESS', 'FAILED', 'COMPENSATED', 'CANCELED'))
);

-- User 1: Dùng cho Happy Path và test thanh toán trùng
INSERT INTO "Users" ("UserId", "Username", "PasswordHash", "FullName", "Phone", "Email", "Balance", "RowVersion")
VALUES ('U1001', 'student123', 'password_string', 'John Doe', '0901234567', 'your_email@gmail.com', 50000000.00, 1);

-- User 2: Dùng cho test thanh toán trùng (cùng thanh toán 1 mã học phí với User 1)
INSERT INTO "Users" ("UserId", "Username", "PasswordHash", "FullName", "Phone", "Email", "Balance", "RowVersion")
VALUES ('U1002', 'student124', 'password_string', 'Jack Hammer', '0907654321', 'your_email@gmail.com', 50000000.00, 1);

-- User 3: Dùng cho test Concurrency số dư (Balance = 5tr)
INSERT INTO "Users" ("UserId", "Username", "PasswordHash", "FullName", "Phone", "Email", "Balance", "RowVersion")
VALUES ('U1003', 'student125', 'password_string', 'Jeff Mousse', '0908888888', 'your_email@gmail.com', 15000000.00, 1);


-- Sinh viên & Học phí
INSERT INTO "Students" ("Mssv", "FullName") VALUES ('123H4567', 'June Doe'), ('201H0022', 'Mike'), ('199H0011', 'Anh');

-- Học phí cho Happy Path (User 1 thanh toán)
INSERT INTO "TuitionFees" ("FeeId", "Mssv", "Amount", "IsPaid", "RowVersion") VALUES ('F_HAPPY', '123H4567', 5000000.00, FALSE, 1);
-- Học phí cho Test Trùng (User 1 và User 2 cùng thanh toán)
INSERT INTO "TuitionFees" ("FeeId", "Mssv", "Amount", "IsPaid", "RowVersion") VALUES ('F_DOUBLE', '201H0022', 5000000.00, FALSE, 1);
-- Học phí cho Test Số dư (User 3 thanh toán)
INSERT INTO "TuitionFees" ("FeeId", "Mssv", "Amount", "IsPaid", "RowVersion") VALUES ('F_SIMUL_1', '199H0011', 5000000.00, FALSE, 1);
INSERT INTO "TuitionFees" ("FeeId", "Mssv", "Amount", "IsPaid", "RowVersion") VALUES ('F_SIMUL_2', '199H0011', 5000000.00, FALSE, 1);

INSERT INTO "TuitionFees" ("FeeId", "Mssv", "Amount", "IsPaid", "RowVersion") VALUES ('F_OVER', '199H0011', 5000000.00, FALSE, 1);
