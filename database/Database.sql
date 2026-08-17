-- database/Database.sql — run in SSMS. Drop-and-recreate + seed = clean dev DB every run.
-- The app NEVER creates or migrates schema; this hand-maintained script is the only schema owner.
--
-- This database holds ONLY what the exam system itself owns. Student, course, exam, registration and
-- outcome-points data belongs to Infoeduka and lives in backend/MockInfoeduka.Api, reached through
-- IInfoedukaClient — never a table here. Columns naming those rows (StudentId, ExamId, OutcomeCode)
-- are therefore plain NVARCHAR external identifiers with no foreign key: the row they reference is
-- in another system.

-- Required for the UX_ExamSession_OneActive filtered index. SSMS/SqlClient default these ON;
-- setting them explicitly makes the script build the filtered index under any client (e.g. sqlcmd).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF DB_ID(N'ExamSystem') IS NULL CREATE DATABASE ExamSystem;
GO
USE ExamSystem;
GO
-- drop in FK dependency order (children first)
DROP TABLE IF EXISTS Screenshot, SessionOutcome, ExamSession, LockedExam,
                     ExamAccessCode, Instruction, Device;
GO

CREATE TABLE Device (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    DeviceId   NVARCHAR(100) NOT NULL UNIQUE,
    ClientType NVARCHAR(20)  NOT NULL,               -- 'electron'
    Hostname   NVARCHAR(100),
    LocalIp    NVARCHAR(45),
    LastSeen   DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE TABLE ExamAccessCode (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    ExamId     NVARCHAR(30) NOT NULL UNIQUE,         -- external Infoeduka examId, no FK
    Group1Code NVARCHAR(20) NOT NULL,
    Group2Code NVARCHAR(20) NOT NULL
);

CREATE TABLE Instruction (                           -- versioned, course-independent
    Id            BIGINT IDENTITY(1,1) PRIMARY KEY,
    InstructionId NVARCHAR(50) NOT NULL UNIQUE,      -- 'EXAM-GENERAL-V1'
    VersionNo     INT NOT NULL,
    HtmlHr        NVARCHAR(MAX) NOT NULL,
    HtmlEn        NVARCHAR(MAX) NOT NULL
);

CREATE TABLE ExamSession (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    SessionId  NVARCHAR(40) NOT NULL UNIQUE,         -- 'SESSION-001'
    StudentId  NVARCHAR(20) NOT NULL,                -- external Infoeduka studentId, no FK
    ExamId     NVARCHAR(30) NOT NULL,                -- external Infoeduka examId, no FK
    DeviceId   BIGINT NULL REFERENCES Device(Id),    -- NULL for online students
    GroupNo    INT NULL,                             -- 1 or 2, NULL for online
    Status     NVARCHAR(20) NOT NULL DEFAULT 'ACTIVE',  -- ACTIVE | FINISHED | AUTO_CLOSED
    WizardStep NVARCHAR(30) NOT NULL DEFAULT 'EXAMS',   -- EXAMS|OUTCOMES|INSTRUCTIONS|CONFIRM|IN_EXAM
    -- Snapshot taken at Potvrdi: the spec says confirmed data is locked ("ih više neće moći
    -- mijenjati"), and the during-exam screens must keep working even if Infoeduka is briefly
    -- unreachable mid-exam. NULL until the session is confirmed.
    CourseNameHr    NVARCHAR(200) NULL,
    CourseNameEn    NVARCHAR(200) NULL,
    ExamDateTime    DATETIMEOFFSET NULL,
    Classroom       NVARCHAR(30) NULL,
    StudentFullName NVARCHAR(200) NULL,
    StudentJmbag    NVARCHAR(10) NULL,
    StartedAt  DATETIMEOFFSET NULL,                  -- set at Potvrdi
    EndedAt    DATETIMEOFFSET NULL,
    CreatedAt  DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
-- max ONE ACTIVE session per student, enforced by the DB itself:
CREATE UNIQUE INDEX UX_ExamSession_OneActive ON ExamSession(StudentId) WHERE Status = 'ACTIVE';
CREATE INDEX IX_ExamSession_Exam ON ExamSession(ExamId);
CREATE INDEX IX_ExamSession_Status ON ExamSession(Status);

CREATE TABLE SessionOutcome (                        -- outcomes chosen for the session
    Id            BIGINT IDENTITY(1,1) PRIMARY KEY,
    ExamSessionId BIGINT NOT NULL REFERENCES ExamSession(Id),
    OutcomeCode   NVARCHAR(10) NOT NULL,             -- external outcome code, e.g. 'I1' — no FK
    CONSTRAINT UQ_SessionOutcome UNIQUE (ExamSessionId, OutcomeCode)
);

CREATE TABLE LockedExam (                            -- same-term lockout, permanent
    Id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    StudentId NVARCHAR(20) NOT NULL,                 -- external, no FK
    ExamId    NVARCHAR(30) NOT NULL,                 -- external, no FK
    Reason    NVARCHAR(100) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT UQ_LockedExam UNIQUE (StudentId, ExamId)
);
CREATE INDEX IX_LockedExam_Exam ON LockedExam(ExamId);

CREATE TABLE Screenshot (
    Id            BIGINT IDENTITY(1,1) PRIMARY KEY,
    ExamSessionId BIGINT NOT NULL REFERENCES ExamSession(Id),
    TakenAt       DATETIMEOFFSET NOT NULL,
    ImagePath     NVARCHAR(300) NOT NULL,            -- stored on server disk, not in DB
    CreatedAt     DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX IX_Screenshot_Session ON Screenshot(ExamSessionId);
GO

-- ===== DEV SEED (exam-system-owned reference data only) =====
-- (fixed IDs via IDENTITY_INSERT so tests can rely on them; N'...' for Croatian diacritics)

-- Access codes are keyed by the EXTERNAL exam id now — the Exam row lives in MockInfoeduka.Api.
SET IDENTITY_INSERT ExamAccessCode ON;
INSERT INTO ExamAccessCode (Id, ExamId, Group1Code, Group2Code) VALUES
 (1, N'EXAM-1001', N'MAT-G1-4821', N'MAT-G2-9173'),
 (2, N'EXAM-1002', N'PRG-G1-3355', N'PRG-G2-7042');
SET IDENTITY_INSERT ExamAccessCode OFF;

-- Versioned, course-independent instructions. Raw HTML injected via @Html.Raw on the frontend.
SET IDENTITY_INSERT Instruction ON;
INSERT INTO Instruction (Id, InstructionId, VersionNo, HtmlHr, HtmlEn) VALUES
 (1, N'EXAM-GENERAL-V1', 1,
  N'<h2>Upute prije ispita</h2><p>Provjerite da je na stolu samo dopušteni pribor. Zabranjena je uporaba mobilnih uređaja.</p><ul><li>Isključite sve ostale aplikacije.</li><li>Pričekajte znak dežurnog za početak.</li></ul>',
  N'<h2>Instructions before the exam</h2><p>Make sure only permitted materials are on your desk. Use of mobile devices is prohibited.</p><ul><li>Close all other applications.</li><li>Wait for the invigilator''s signal to begin.</li></ul>'),
 (2, N'EXAM-GENERAL-V2', 2,
  N'<h2>Upute tijekom ispita</h2><p>Ispit je u tijeku. Ne napuštajte aplikaciju za ispit.</p><ul><li>Za pomoć podignite ruku.</li><li>Po završetku odaberite <strong>Završi ispit</strong>.</li></ul>',
  N'<h2>Instructions during the exam</h2><p>The exam is in progress. Do not leave the exam application.</p><ul><li>Raise your hand if you need help.</li><li>When finished, choose <strong>Finish exam</strong>.</li></ul>');
SET IDENTITY_INSERT Instruction OFF;
GO
