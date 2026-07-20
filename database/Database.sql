-- database/Database.sql — run in SSMS. Drop-and-recreate + seed = clean dev DB every run.
-- The app NEVER creates or migrates schema; this hand-maintained script is the only schema owner.

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
                     StudentOutcomePoints, LearningOutcome, ExamRegistration,
                     ExamAccessCode, Exam, Course, Instruction, Device, Student;
GO

-- Students (mirrors what Infoeduka would give us)
CREATE TABLE Student (
    Id           BIGINT IDENTITY(1,1) PRIMARY KEY,
    StudentId    NVARCHAR(20)  NOT NULL UNIQUE,      -- '2023001234'
    Jmbag        NVARCHAR(10)  NOT NULL UNIQUE,
    FullName     NVARCHAR(200) NOT NULL,
    AaiPrincipal NVARCHAR(200) UNIQUE,               -- hrEduPersonUniqueID
    CreatedAt    DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE TABLE Course (
    Id           BIGINT IDENTITY(1,1) PRIMARY KEY,
    CourseId     NVARCHAR(20)  NOT NULL UNIQUE,      -- '2876'
    CourseNameHr NVARCHAR(200) NOT NULL,
    CourseNameEn NVARCHAR(200) NOT NULL,
    CreatedAt    DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE TABLE Exam (
    Id           BIGINT IDENTITY(1,1) PRIMARY KEY,
    ExamId       NVARCHAR(30) NOT NULL UNIQUE,       -- 'EXAM-1001'
    CourseId     BIGINT       NOT NULL REFERENCES Course(Id),
    ExamDateTime DATETIMEOFFSET NOT NULL,
    Classroom    NVARCHAR(30) NOT NULL,
    CreatedAt    DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX IX_Exam_Course ON Exam(CourseId);
CREATE INDEX IX_Exam_ExamDateTime ON Exam(ExamDateTime);

CREATE TABLE ExamAccessCode (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    ExamId     BIGINT NOT NULL UNIQUE REFERENCES Exam(Id),
    Group1Code NVARCHAR(20) NOT NULL,
    Group2Code NVARCHAR(20) NOT NULL
);

CREATE TABLE ExamRegistration (                      -- student signed up for exam
    Id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    StudentId BIGINT NOT NULL REFERENCES Student(Id),
    ExamId    BIGINT NOT NULL REFERENCES Exam(Id),
    CONSTRAINT UQ_ExamRegistration UNIQUE (StudentId, ExamId)
);
CREATE INDEX IX_ExamRegistration_Exam ON ExamRegistration(ExamId);

CREATE TABLE LearningOutcome (
    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
    CourseId    BIGINT NOT NULL REFERENCES Course(Id),
    OutcomeCode NVARCHAR(10) NOT NULL,               -- 'I1'..'I5'
    CONSTRAINT UQ_LearningOutcome UNIQUE (CourseId, OutcomeCode)
);

CREATE TABLE StudentOutcomePoints (                  -- current points per outcome
    Id                BIGINT IDENTITY(1,1) PRIMARY KEY,
    StudentId         BIGINT NOT NULL REFERENCES Student(Id),
    LearningOutcomeId BIGINT NOT NULL REFERENCES LearningOutcome(Id),
    TotalPointsEarned NUMERIC(6,2) NOT NULL DEFAULT 0,
    TotalPointsMax    NUMERIC(6,2) NOT NULL,
    ExamPointsEarned  NUMERIC(6,2) NOT NULL DEFAULT 0,
    ExamPointsMax     NUMERIC(6,2) NOT NULL,
    CONSTRAINT UQ_StudentOutcomePoints UNIQUE (StudentId, LearningOutcomeId)
);
CREATE INDEX IX_StudentOutcomePoints_Outcome ON StudentOutcomePoints(LearningOutcomeId);

CREATE TABLE Instruction (                           -- versioned, course-independent
    Id            BIGINT IDENTITY(1,1) PRIMARY KEY,
    InstructionId NVARCHAR(50) NOT NULL UNIQUE,      -- 'EXAM-GENERAL-V1'
    VersionNo     INT NOT NULL,
    HtmlHr        NVARCHAR(MAX) NOT NULL,
    HtmlEn        NVARCHAR(MAX) NOT NULL
);

CREATE TABLE Device (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    DeviceId   NVARCHAR(100) NOT NULL UNIQUE,
    ClientType NVARCHAR(20)  NOT NULL,               -- 'electron'
    Hostname   NVARCHAR(100),
    LocalIp    NVARCHAR(45),
    LastSeen   DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE TABLE ExamSession (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    SessionId  NVARCHAR(40) NOT NULL UNIQUE,         -- 'SESSION-001'
    StudentId  BIGINT NOT NULL REFERENCES Student(Id),
    ExamId     BIGINT NOT NULL REFERENCES Exam(Id),
    DeviceId   BIGINT NULL REFERENCES Device(Id),    -- NULL for online students
    GroupNo    INT NULL,                             -- 1 or 2, NULL for online
    Status     NVARCHAR(20) NOT NULL DEFAULT 'ACTIVE',  -- ACTIVE | FINISHED | AUTO_CLOSED
    WizardStep NVARCHAR(30) NOT NULL DEFAULT 'EXAMS',   -- EXAMS|OUTCOMES|INSTRUCTIONS|CONFIRM|IN_EXAM
    StartedAt  DATETIMEOFFSET NULL,                  -- set at Potvrdi
    EndedAt    DATETIMEOFFSET NULL,
    CreatedAt  DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
-- max ONE ACTIVE session per student, enforced by the DB itself:
CREATE UNIQUE INDEX UX_ExamSession_OneActive ON ExamSession(StudentId) WHERE Status = 'ACTIVE';
CREATE INDEX IX_ExamSession_Exam ON ExamSession(ExamId);
CREATE INDEX IX_ExamSession_Status ON ExamSession(Status);

CREATE TABLE SessionOutcome (                        -- outcomes chosen for the session
    Id                BIGINT IDENTITY(1,1) PRIMARY KEY,
    ExamSessionId     BIGINT NOT NULL REFERENCES ExamSession(Id),
    LearningOutcomeId BIGINT NOT NULL REFERENCES LearningOutcome(Id),
    CONSTRAINT UQ_SessionOutcome UNIQUE (ExamSessionId, LearningOutcomeId)
);
CREATE INDEX IX_SessionOutcome_Outcome ON SessionOutcome(LearningOutcomeId);

CREATE TABLE LockedExam (                            -- same-term lockout, permanent
    Id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    StudentId BIGINT NOT NULL REFERENCES Student(Id),
    ExamId    BIGINT NOT NULL REFERENCES Exam(Id),
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

-- ===== DEV SEED =====
-- (fixed IDs via IDENTITY_INSERT so tests can rely on them; N'...' for Croatian diacritics)

SET IDENTITY_INSERT Student ON;
INSERT INTO Student (Id, StudentId, Jmbag, FullName, AaiPrincipal) VALUES
 (1, N'2023001234', N'0036512345', N'Ivan Horvat',  N'ivan.horvat@algebra.hr'),
 (2, N'2023001235', N'0036512346', N'Ana Kovač',    N'ana.kovac@algebra.hr'),
 (3, N'2023001236', N'0036512347', N'Marko Marić',  N'marko.maric@algebra.hr');
SET IDENTITY_INSERT Student OFF;

SET IDENTITY_INSERT Course ON;
INSERT INTO Course (Id, CourseId, CourseNameHr, CourseNameEn) VALUES
 (1, N'2876', N'Matematika 1',   N'Mathematics 1'),
 (2, N'2888', N'Programiranje 1', N'Programming 1');
SET IDENTITY_INSERT Course OFF;

SET IDENTITY_INSERT LearningOutcome ON;
INSERT INTO LearningOutcome (Id, CourseId, OutcomeCode) VALUES
 (1, 1, N'I1'), (2, 1, N'I2'), (3, 1, N'I3'), (4, 1, N'I4'), (5, 1, N'I5'),   -- Matematika 1
 (6, 2, N'I1'), (7, 2, N'I2'), (8, 2, N'I3'), (9, 2, N'I4'), (10, 2, N'I5');  -- Programiranje 1
SET IDENTITY_INSERT LearningOutcome OFF;

-- Both exams share the SAME ExamDateTime, captured once so they are byte-for-byte equal.
-- DATEADD relative to SYSDATETIMEOFFSET() keeps them inside the ±30 min window forever (seed never "expires").
DECLARE @ExamTime DATETIMEOFFSET = DATEADD(MINUTE, 10, SYSDATETIMEOFFSET());
SET IDENTITY_INSERT Exam ON;
INSERT INTO Exam (Id, ExamId, CourseId, ExamDateTime, Classroom) VALUES
 (1, N'EXAM-1001', 1, @ExamTime, N'A101'),   -- Matematika 1
 (2, N'EXAM-1002', 2, @ExamTime, N'B203');   -- Programiranje 1  (same term as EXAM-1001 → lockout testbed)
SET IDENTITY_INSERT Exam OFF;

SET IDENTITY_INSERT ExamAccessCode ON;
INSERT INTO ExamAccessCode (Id, ExamId, Group1Code, Group2Code) VALUES
 (1, 1, N'MAT-G1-4821', N'MAT-G2-9173'),
 (2, 2, N'PRG-G1-3355', N'PRG-G2-7042');
SET IDENTITY_INSERT ExamAccessCode OFF;

-- Ivan is registered for BOTH same-term exams (the graded lockout scenario);
-- Ana and Marko for one each.
SET IDENTITY_INSERT ExamRegistration ON;
INSERT INTO ExamRegistration (Id, StudentId, ExamId) VALUES
 (1, 1, 1),   -- Ivan  → EXAM-1001
 (2, 1, 2),   -- Ivan  → EXAM-1002  (same ExamDateTime)
 (3, 2, 1),   -- Ana   → EXAM-1001
 (4, 3, 2);   -- Marko → EXAM-1002
SET IDENTITY_INSERT ExamRegistration OFF;

-- Points per outcome. Green card rule is TotalPointsEarned >= 0.5 * TotalPointsMax (decimal, >=).
-- Multiple outcomes sit EXACTLY at 50% (LO1/LO7 for Ivan, LO2 for Ana, LO8 for Marko) — the green boundary.
SET IDENTITY_INSERT StudentOutcomePoints ON;
INSERT INTO StudentOutcomePoints
 (Id, StudentId, LearningOutcomeId, TotalPointsEarned, TotalPointsMax, ExamPointsEarned, ExamPointsMax) VALUES
 -- Ivan, Matematika 1 (LO 1..5)
 (1,  1, 1,  50.00, 100.00, 10.00, 20.00),   -- exactly 50% → green boundary
 (2,  1, 2,  75.00, 100.00, 15.00, 20.00),   -- green
 (3,  1, 3,  40.00, 100.00,  8.00, 20.00),   -- not green
 (4,  1, 4,  60.50, 100.00, 12.00, 20.00),   -- green
 (5,  1, 5,  30.00, 100.00,  6.00, 20.00),   -- not green
 -- Ivan, Programiranje 1 (LO 6..10)
 (6,  1, 6,  55.00, 100.00, 11.00, 20.00),   -- green
 (7,  1, 7,  50.00, 100.00, 10.00, 20.00),   -- exactly 50% → green boundary
 (8,  1, 8,  20.00, 100.00,  4.00, 20.00),   -- not green
 (9,  1, 9,  90.00, 100.00, 18.00, 20.00),   -- green
 (10, 1, 10, 45.00, 100.00,  9.00, 20.00),   -- not green
 -- Ana, Matematika 1
 (11, 2, 1,  80.00, 100.00, 16.00, 20.00),
 (12, 2, 2,  50.00, 100.00, 10.00, 20.00),   -- exactly 50%
 (13, 2, 3,  65.00, 100.00, 13.00, 20.00),
 (14, 2, 4,  30.00, 100.00,  6.00, 20.00),
 (15, 2, 5, 100.00, 100.00, 20.00, 20.00),
 -- Marko, Programiranje 1
 (16, 3, 6,  25.00, 100.00,  5.00, 20.00),
 (17, 3, 7,  60.00, 100.00, 12.00, 20.00),
 (18, 3, 8,  50.00, 100.00, 10.00, 20.00),   -- exactly 50%
 (19, 3, 9,  70.00, 100.00, 14.00, 20.00),
 (20, 3, 10, 40.00, 100.00,  8.00, 20.00);
SET IDENTITY_INSERT StudentOutcomePoints OFF;

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
