-- Optional: the web app creates this table automatically on first run (EnsureCreated).
-- Run this manually in the Azure Portal Query Editor if you prefer to create it yourself.

IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        Username         NVARCHAR(100) NOT NULL,
        PasswordHash     NVARCHAR(500) NOT NULL,   -- ASP.NET Core PasswordHasher (PBKDF2) hash, never plain text
        FullName         NVARCHAR(150) NULL,
        Role             NVARCHAR(50)  NOT NULL DEFAULT 'Operator',
        Status           NVARCHAR(50)  NOT NULL DEFAULT 'Idle',  -- updated by QueueTriggerFunction
        LastIrrigationAt DATETIME2     NULL,
        LastCommandId    NVARCHAR(100) NULL,
        CreatedAt        DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX IX_Users_Username ON dbo.Users (Username);
END
GO

-- Users are added by the app (see Seed section in appsettings.json) so passwords are hashed correctly.
-- To reset someone back to idle after irrigation:
-- UPDATE dbo.Users SET Status = 'Idle' WHERE Username = 'admin';
