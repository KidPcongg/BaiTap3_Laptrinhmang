/*
  Bài tập 3 – Tạo cơ sở dữ liệu cho server.
  Chạy một lần bằng SSMS, Azure Data Studio hoặc sqlcmd:
      sqlcmd -S localhost -E -i database/QuanLyNguoiDung.sql
*/

IF DB_ID(N'QuanLyNguoiDung') IS NULL
    CREATE DATABASE QuanLyNguoiDung;
GO

USE QuanLyNguoiDung;
GO

IF OBJECT_ID(N'dbo.NguoiDung', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NguoiDung
    (
        UserId       INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_NguoiDung PRIMARY KEY,
        Username     NVARCHAR(50)       NOT NULL CONSTRAINT UQ_NguoiDung_Username UNIQUE,
        -- PBKDF2-HMAC-SHA256: 32 byte chuỗi băm, 16 byte salt ngẫu nhiên riêng cho từng tài khoản.
        PasswordHash VARBINARY(32)      NOT NULL,
        PasswordSalt VARBINARY(16)      NOT NULL,
        Email        NVARCHAR(100)      NOT NULL,
        FullName     NVARCHAR(100)      NOT NULL,
        Birthday     DATE               NULL
    );
END
GO