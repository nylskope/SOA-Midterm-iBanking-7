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

INSERT INTO "Users" ("UserId", "Username", "PasswordHash", "FullName", "Phone", "Email", "Balance", "RowVersion")
VALUES ('U1001', 'student123', 'password_string', 'John Doe', '0901234567', 'davidduy06@gmail.com', 9000000.00, 1);

INSERT INTO "Students" ("Mssv", "FullName") VALUES ('123H4567', 'John Doe');

INSERT INTO "TuitionFees" ("FeeId", "Mssv", "Amount", "IsPaid", "RowVersion")
VALUES ('F2026_1', '123H4567', 5000000.00, FALSE, 1),
       ('F2026_2', '123H4567', 5000000.00, FALSE, 1);


INSERT INTO "Users" ("UserId", "Username", "PasswordHash", "FullName", "Phone", "Email", "Balance", "RowVersion")
VALUES ('U1002', 'student124', 'password_string', 'Dzuy', '0907654321', 'ganduytran2102@gmail.com', 140000000.00, 1);

INSERT INTO "Students" ("Mssv", "FullName") VALUES ('524H0012', 'Dzuy');

INSERT INTO "TuitionFees" ("FeeId", "Mssv", "Amount", "IsPaid", "RowVersion")
VALUES ('F2026_3', '524H0012', 5000000.00, FALSE, 1),
       ('F2026_4', '524H0012', 5000000.00, FALSE, 1);
