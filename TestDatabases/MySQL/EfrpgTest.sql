-- ---------------------------------------------------------------------------------------------------------
-- EfrpgTest for MySQL.
--
-- Like the SQL Server EfrpgTest, this database is deliberately horrible. It is not a sample application: it
-- is a collection of every construct, type and naming atrocity the generator has to survive. If EFRPG can
-- reverse engineer this, it can reverse engineer whatever is out in the field.
--
-- Loaded automatically by docker-compose.yml on the first start of the container.
-- ---------------------------------------------------------------------------------------------------------

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 1;

-- ---------------------------------------------------------------------------------------------------------
-- Every type MySQL has, so the language mapping is exercised end to end. Note tinyint(1), which is how
-- MySQL stores a bool and the one display width MySQL 8 still reports, and the unsigned variants, which are
-- separate entries in the mapping because DATA_TYPE alone cannot tell them apart.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE AllColumnTypes
(
    Id                  INT AUTO_INCREMENT PRIMARY KEY,

    TinyIntBool         TINYINT(1)          NULL COMMENT 'tinyint(1) is a bool everywhere except information_schema',
    TinyIntNumber       TINYINT             NULL,
    TinyIntUnsigned     TINYINT UNSIGNED    NULL,
    SmallIntCol         SMALLINT            NULL,
    SmallIntUnsigned    SMALLINT UNSIGNED   NULL,
    MediumIntCol        MEDIUMINT           NULL,
    MediumIntUnsigned   MEDIUMINT UNSIGNED  NULL,
    IntCol              INT                 NULL,
    IntUnsigned         INT UNSIGNED        NULL,
    BigIntCol           BIGINT              NULL,
    BigIntUnsigned      BIGINT UNSIGNED     NULL,

    DecimalCol          DECIMAL(18, 4)      NULL,
    DecimalUnsigned     DECIMAL(10, 2) UNSIGNED NULL,
    NumericCol          NUMERIC(9, 3)       NULL,
    FloatCol            FLOAT               NULL,
    DoubleCol           DOUBLE              NULL,
    BitOne              BIT(1)              NULL,
    BitEight            BIT(8)              NULL,

    CharCol             CHAR(10)            NULL,
    VarCharCol          VARCHAR(255)        NULL,
    TinyTextCol         TINYTEXT            NULL,
    TextCol             TEXT                NULL,
    MediumTextCol       MEDIUMTEXT          NULL,
    LongTextCol         LONGTEXT            NULL,

    BinaryCol           BINARY(16)          NULL,
    VarBinaryCol        VARBINARY(255)      NULL,
    TinyBlobCol         TINYBLOB            NULL,
    BlobCol             BLOB                NULL,
    MediumBlobCol       MEDIUMBLOB          NULL,
    LongBlobCol         LONGBLOB            NULL,

    DateCol             DATE                NULL,
    DateTimeCol         DATETIME            NULL,
    DateTimeFractional  DATETIME(3)         NULL,
    TimeCol             TIME                NULL,
    TimeFractional      TIME(6)             NULL,
    YearCol             YEAR                NULL,
    TimestampCol        TIMESTAMP           NULL,

    EnumCol             ENUM('Small', 'Medium', 'Large') NULL,
    SetCol              SET('Red', 'Green', 'Blue')      NULL,
    JsonCol             JSON                NULL,

    GeometryCol         GEOMETRY            NULL,
    PointCol            POINT               NULL
) COMMENT = 'One column per MySQL type, to exercise the language mapping';

-- ---------------------------------------------------------------------------------------------------------
-- Defaults of every shape, and the store generated columns. A generated column may not depend on a column
-- carrying ON DELETE CASCADE, which is why nothing here has a foreign key.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE DefaultsAndGenerated
(
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    Quantity        INT             NOT NULL DEFAULT 1,
    UnitPrice       DECIMAL(18, 4)  NOT NULL DEFAULT 9.99,
    Description     VARCHAR(50)     NOT NULL DEFAULT 'Hello world',
    IsActive        TINYINT(1)      NOT NULL DEFAULT 1,
    ExternalRef     CHAR(36)        NOT NULL DEFAULT (UUID()),
    CreatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ModifiedAt      TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    LineTotal       DECIMAL(20, 4)  AS (Quantity * UnitPrice) STORED,
    HasQuantity     TINYINT(1)      AS (Quantity > 0) VIRTUAL
) COMMENT = 'Column defaults, generated columns and ON UPDATE';

-- ---------------------------------------------------------------------------------------------------------
-- Defaults that are expressions rather than literals. MySQL is the awkward one: COLUMN_DEFAULT reports the
-- string literal 'fallback' as the bare text fallback, exactly as it reports the expression (UUID()) as uuid(),
-- so the text alone cannot tell them apart. Only EXTRA = DEFAULT_GENERATED can. LiteralLooksLikeFunction is the
-- trap for any guess based on the text: it is the literal string 'uuid()' and must stay one. Expected: the
-- expression columns get HasDefaultValueSql and no constructor default; the literals keep theirs.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE ExpressionDefault
(
    Id                       INT AUTO_INCREMENT PRIMARY KEY,
    ChangedBy                VARCHAR(288)    NOT NULL DEFAULT (CURRENT_USER()),
    CreatedText              VARCHAR(30)     NULL     DEFAULT (DATE_FORMAT(NOW(), '%Y-%m-%d')),
    DueAt                    DATETIME        NOT NULL DEFAULT (NOW() + INTERVAL 30 DAY),
    LiteralLooksLikeFunction VARCHAR(20)     NOT NULL DEFAULT 'uuid()',
    LiteralText              VARCHAR(20)     NOT NULL DEFAULT 'fallback',
    NumberText               VARCHAR(10)     NOT NULL DEFAULT 0
) COMMENT = 'Expression defaults, and literals that must not be mistaken for them';

-- ---------------------------------------------------------------------------------------------------------
-- Names chosen to break naive generators: spaces, reserved words in both SQL and C#, a leading digit,
-- non-ASCII, and a column with the same name as its table.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE `Spaced Table Name`
(
    `Id`                    INT AUTO_INCREMENT PRIMARY KEY,
    `Spaced Column Name`    VARCHAR(50)     NULL,
    `Column-With-Hyphens`   VARCHAR(50)     NULL,
    `1st Place`             VARCHAR(50)     NULL,
    `Ünïcödé Cölumn`        VARCHAR(50)     NULL,
    `Spaced Table Name`     VARCHAR(50)     NULL COMMENT 'Same name as the table it lives in',
    `order`                 INT             NULL COMMENT 'Reserved in SQL',
    `class`                 VARCHAR(50)     NULL COMMENT 'Reserved in C#',
    `event`                 VARCHAR(50)     NULL COMMENT 'Reserved in C#',
    `namespace`             VARCHAR(50)     NULL COMMENT 'Reserved in C#',
    `string`                VARCHAR(50)     NULL COMMENT 'A C# type name'
) COMMENT = 'Identifiers that are legal in MySQL and illegal in C#';

-- ---------------------------------------------------------------------------------------------------------
-- Keys: composite primary key, composite foreign key, self reference, unique constraint, plain index,
-- a table with no primary key at all, and a pure mapping table for the many to many collapse.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE ParentComposite
(
    KeyPartOne  INT             NOT NULL,
    KeyPartTwo  INT             NOT NULL,
    Description VARCHAR(100)    NULL,
    PRIMARY KEY (KeyPartOne, KeyPartTwo)
) COMMENT = 'Composite primary key';

CREATE TABLE ChildComposite
(
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    ParentKeyOne    INT             NOT NULL,
    ParentKeyTwo    INT             NOT NULL,
    UniqueCode      VARCHAR(20)     NOT NULL,
    Note            VARCHAR(100)    NULL,
    CONSTRAINT FK_ChildComposite_Parent FOREIGN KEY (ParentKeyOne, ParentKeyTwo)
        REFERENCES ParentComposite (KeyPartOne, KeyPartTwo) ON DELETE CASCADE,
    CONSTRAINT UQ_ChildComposite_UniqueCode UNIQUE (UniqueCode),
    INDEX IX_ChildComposite_Note (Note)
) COMMENT = 'Composite foreign key, unique constraint and a plain index';

CREATE TABLE Employee
(
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    FullName    VARCHAR(100)    NOT NULL,
    ReportsTo   INT             NULL,
    CONSTRAINT FK_Employee_ReportsTo FOREIGN KEY (ReportsTo) REFERENCES Employee (Id)
) COMMENT = 'Self referencing foreign key';

CREATE TABLE Student
(
    Id      INT AUTO_INCREMENT PRIMARY KEY,
    Name    VARCHAR(100) NOT NULL
);

CREATE TABLE Course
(
    Id      INT AUTO_INCREMENT PRIMARY KEY,
    Title   VARCHAR(100) NOT NULL
);

CREATE TABLE StudentCourse
(
    StudentId   INT NOT NULL,
    CourseId    INT NOT NULL,
    PRIMARY KEY (StudentId, CourseId),
    CONSTRAINT FK_StudentCourse_Student FOREIGN KEY (StudentId) REFERENCES Student (Id) ON DELETE CASCADE,
    CONSTRAINT FK_StudentCourse_Course  FOREIGN KEY (CourseId)  REFERENCES Course (Id)  ON DELETE CASCADE
) COMMENT = 'Nothing but two foreign keys, so it collapses into a many to many';

CREATE TABLE NoPrimaryKey
(
    Something   VARCHAR(50) NULL,
    SomethingElse INT       NULL
) COMMENT = 'No primary key at all';

-- ---------------------------------------------------------------------------------------------------------
-- Lookup table for Settings.Enumerations, with a group column so the grouped form is covered too.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE OrderStatus
(
    Id          INT             NOT NULL PRIMARY KEY,
    Name        VARCHAR(50)     NOT NULL,
    StatusGroup VARCHAR(50)     NULL,
    Description VARCHAR(100)    NULL
) COMMENT = 'Reverse engineered into a C# enum';

INSERT INTO OrderStatus (Id, Name, StatusGroup, Description) VALUES
    (1, 'New',       'Open',   'Just placed'),
    (2, 'Picking',   'Open',   'Being picked'),
    (3, 'Shipped',   'Closed', 'On its way'),
    (4, 'Cancelled', 'Closed', 'Not happening'),
    (5, 'On Hold',   'Open',   'A value whose name needs cleaning up');

INSERT INTO ParentComposite (KeyPartOne, KeyPartTwo, Description) VALUES (1, 1, 'First'), (1, 2, 'Second');
INSERT INTO ChildComposite (ParentKeyOne, ParentKeyTwo, UniqueCode, Note) VALUES (1, 1, 'A-1', 'first child');
INSERT INTO Employee (FullName, ReportsTo) VALUES ('Alice', NULL);
INSERT INTO Employee (FullName, ReportsTo) VALUES ('Bob', 1);

-- ---------------------------------------------------------------------------------------------------------
-- A view, and a trigger, both of which the reader reports separately from tables.
-- ---------------------------------------------------------------------------------------------------------
CREATE VIEW ActiveChildren AS
SELECT c.Id, c.UniqueCode, c.Note, p.Description AS ParentDescription
FROM   ChildComposite c
       INNER JOIN ParentComposite p
           ON p.KeyPartOne = c.ParentKeyOne AND p.KeyPartTwo = c.ParentKeyTwo;

DELIMITER $$

CREATE TRIGGER TR_ChildComposite_BeforeInsert
    BEFORE INSERT ON ChildComposite
    FOR EACH ROW
BEGIN
    SET NEW.UniqueCode = UPPER(NEW.UniqueCode);
END $$

-- ---------------------------------------------------------------------------------------------------------
-- Routines: a procedure with every parameter mode, one that returns a result set, one with no parameters,
-- and functions whose return type has to arrive as the parameter at ordinal 0.
-- ---------------------------------------------------------------------------------------------------------
CREATE PROCEDURE GetChildrenByParent(IN parentKeyOne INT, IN parentKeyTwo INT)
BEGIN
    SELECT Id, UniqueCode, Note
    FROM   ChildComposite
    WHERE  ParentKeyOne = parentKeyOne AND ParentKeyTwo = parentKeyTwo;
END $$

CREATE PROCEDURE CountChildren(IN parentKeyOne INT, OUT childCount INT, INOUT runningTotal INT)
BEGIN
    SELECT COUNT(*) INTO childCount FROM ChildComposite WHERE ParentKeyOne = parentKeyOne;
    SET runningTotal = IFNULL(runningTotal, 0) + childCount;
END $$

CREATE PROCEDURE NoParameters()
BEGIN
    SELECT 1 AS Answer;
END $$

CREATE FUNCTION LineTotal(quantity INT, unitPrice DECIMAL(18, 4))
    RETURNS DECIMAL(20, 4)
    DETERMINISTIC
BEGIN
    RETURN quantity * unitPrice;
END $$

CREATE FUNCTION IsHighValue(total DECIMAL(20, 4))
    RETURNS TINYINT(1)
    DETERMINISTIC
BEGIN
    RETURN total > 1000;
END $$

DELIMITER ;

-- ---------------------------------------------------------------------------------------------------------
-- More key shapes: a foreign key onto a unique constraint rather than the primary key, two foreign keys onto
-- the same parent (which the reverse navigation properties have to disambiguate), a one to one where the
-- child's primary key is also its foreign key, and a primary key whose columns are declared in a different
-- order from the column order.
--
-- InnoDB has no un-enforced foreign key - no NOCHECK, no NOT VALID, no DISABLE - so that case cannot be
-- covered here. It is covered by the SQL Server, PostgreSQL and Oracle scripts.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE PrincipalKeyChild
(
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    ParentUniqueCode VARCHAR(20) NOT NULL,
    CONSTRAINT FK_PrincipalKeyChild FOREIGN KEY (ParentUniqueCode) REFERENCES ChildComposite (UniqueCode)
) COMMENT = 'Foreign key onto a unique constraint, not the primary key';

CREATE TABLE Airport
(
    Id   INT         NOT NULL PRIMARY KEY,
    Name VARCHAR(50) NOT NULL
);

CREATE TABLE Flight
(
    Id                 INT AUTO_INCREMENT PRIMARY KEY,
    DepartureAirportId INT NOT NULL,
    ArrivalAirportId   INT NOT NULL,
    CONSTRAINT FK_Flight_Departure FOREIGN KEY (DepartureAirportId) REFERENCES Airport (Id),
    CONSTRAINT FK_Flight_Arrival   FOREIGN KEY (ArrivalAirportId)   REFERENCES Airport (Id)
) COMMENT = 'Two foreign keys onto the same parent';

CREATE TABLE Person
(
    Id       INT AUTO_INCREMENT PRIMARY KEY,
    FullName VARCHAR(100) NOT NULL
);

CREATE TABLE PersonPhoto
(
    PersonId INT  NOT NULL PRIMARY KEY,
    Photo    BLOB NOT NULL,
    CONSTRAINT FK_PersonPhoto FOREIGN KEY (PersonId) REFERENCES Person (Id)
) COMMENT = 'One to one: the primary key is also the foreign key';

CREATE TABLE PkOrdinalTest
(
    Filler    VARCHAR(10) NULL,
    SecondKey INT         NOT NULL,
    FirstKey  INT         NOT NULL,
    PRIMARY KEY (FirstKey, SecondKey)
) COMMENT = 'The key order is not the column order';

CREATE TABLE AllColumnsNull
(
    A INT          NULL,
    B VARCHAR(50)  NULL,
    C DATETIME     NULL
) COMMENT = 'Every column nullable and no key, so EF cannot use it';


-- ---------------------------------------------------------------------------------------------------------
-- Cross database foreign key. MySQL has no schema separate from the database, so a "different schema" is a
-- different database - which is exactly why the reader sets IncludeSchema = false for MySQL.
-- ---------------------------------------------------------------------------------------------------------
CREATE DATABASE IF NOT EXISTS EfrpgTestOther;

CREATE TABLE EfrpgTestOther.Category
(
    Id   INT         NOT NULL PRIMARY KEY,
    Name VARCHAR(50) NOT NULL
);

CREATE TABLE EfrpgTestOther.CategoryDescription
(
    CategoryId  INT          NOT NULL PRIMARY KEY,
    Description VARCHAR(200) NOT NULL,
    CONSTRAINT FK_CategoryDescription FOREIGN KEY (CategoryId) REFERENCES EfrpgTestOther.Category (Id)
);


-- ---------------------------------------------------------------------------------------------------------
-- More naming atrocities. Column names in MySQL are always case insensitive, so two columns differing only
-- by case is impossible; two tables differing only by case is not, on a case sensitive filesystem. ALLCAPS
-- names ending in IES are the pluralisation trap.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE CURRENCIES
(
    Id   INT     NOT NULL PRIMARY KEY,
    Code CHAR(3) NOT NULL
) COMMENT = 'ALLCAPS ending in IES, which naive pluralisation mangles';

CREATE TABLE CATEGORIES
(
    Id   INT         NOT NULL PRIMARY KEY,
    Name VARCHAR(50) NOT NULL
);


-- ---------------------------------------------------------------------------------------------------------
-- Indexes and constraints MySQL spells its own way: a prefix index over part of a column, a FULLTEXT index,
-- a CHECK constraint (enforced since 8.0.16), and ZEROFILL, which implies UNSIGNED and changes how the
-- column is reported.
--
-- Note there is no unique-constraint-versus-unique-index distinction to test here: in MySQL a UNIQUE
-- constraint *is* a unique index, and information_schema reports only the index.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE IndexTest
(
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    UniqueCol   INT             NOT NULL,
    Body        TEXT            NOT NULL,
    Title       VARCHAR(255)    NOT NULL,
    ZeroFilled  INT(8) ZEROFILL NULL,
    A           INT             NOT NULL,
    B           INT             NOT NULL,
    Quantity    INT             NOT NULL DEFAULT 1,
    CONSTRAINT UQ_IndexTest_UniqueCol UNIQUE (UniqueCol),
    CONSTRAINT CK_IndexTest_Quantity  CHECK (Quantity > 0),
    UNIQUE   KEY UX_IndexTest_A_B (A, B),
             KEY IX_IndexTest_TitlePrefix (Title(20)),
    FULLTEXT KEY FT_IndexTest_Body (Body)
) COMMENT = 'Prefix index, fulltext index, check constraint and a zerofill column';


-- ---------------------------------------------------------------------------------------------------------
-- An enum lookup table with no group column, so both the grouped and ungrouped enum paths are covered.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE Status
(
    Id   INT         NOT NULL PRIMARY KEY,
    Name VARCHAR(10) NOT NULL
) COMMENT = 'Enum lookup with no group column';

INSERT INTO Status (Id, Name) VALUES (1, 'Todo'), (2, 'InProgress'), (3, 'Done');


-- ---------------------------------------------------------------------------------------------------------
-- Seed rows for the tables added above.
-- ---------------------------------------------------------------------------------------------------------
INSERT INTO Airport (Id, Name) VALUES (1, 'Manchester'), (2, 'Heathrow');
INSERT INTO Flight (DepartureAirportId, ArrivalAirportId) VALUES (1, 2);
INSERT INTO EfrpgTestOther.Category (Id, Name) VALUES (1, 'Beverages');
INSERT INTO EfrpgTestOther.CategoryDescription (CategoryId, Description) VALUES (1, 'Soft drinks and coffees');


-- ---------------------------------------------------------------------------------------------------------
-- A procedure whose parameters are reserved words in both SQL and C#, which the name cleanup has to survive.
-- ---------------------------------------------------------------------------------------------------------
DELIMITER $$

CREATE PROCEDURE ReservedWordParams(IN SelectValue VARCHAR(50), IN ClassValue INT, IN NamespaceValue VARCHAR(50))
BEGIN
    SELECT SelectValue AS `select`, ClassValue AS `class`, NamespaceValue AS `namespace`;
END $$

DELIMITER ;
