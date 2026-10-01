-- ---------------------------------------------------------------------------------------------------------
-- EfrpgTest for Oracle.
--
-- Like the SQL Server EfrpgTest, this database is deliberately horrible. It is not a sample application: it
-- is a collection of every construct, type and naming atrocity the generator has to survive. If EFRPG can
-- reverse engineer this, it can reverse engineer whatever is out in the field.
--
-- It covers the same checklist as the SQL Server script - types, keys, defaults, generated columns, views,
-- triggers, comments, routines, enum lookups, awful identifiers - and then the things Oracle has that SQL
-- Server does not: NUMBER precision bands, the LONG type, INTERVAL and TIMESTAMP WITH LOCAL TIME ZONE,
-- BINARY_FLOAT/BINARY_DOUBLE, ROWID, XMLTYPE, virtual columns, both spellings of an identity column, the
-- pre-12c sequence-plus-trigger idiom, synonyms, index-organised tables, materialised views, disabled
-- constraints, and REF CURSOR output parameters.
--
--   sqlplus efrpgtest/abc123@//localhost:1521/pdb1 @EfrpgTest.sql
--
-- Everything lives in one schema on purpose. The reader scopes every query to
-- SYS_CONTEXT('USERENV','CURRENT_SCHEMA'), so a cross-schema foreign key would not be visible to it and
-- would only add noise. Cross-schema navigation is covered by the SQL Server and PostgreSQL scripts.
-- ---------------------------------------------------------------------------------------------------------

SET DEFINE OFF
SET SQLBLANKLINES ON


-- ---------------------------------------------------------------------------------------------------------
-- One column per type, so the language mapping is exercised end to end. The NUMBER bands matter most:
-- Oracle has no integer types, only NUMBER(p,s), and the generator has to band the precision to bool, byte,
-- short, int and long. A bare NUMBER with no precision at all is the awkward case - it is arbitrary
-- precision and can only be decimal.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE ALL_COLUMN_TYPES
(
    ID                      NUMBER(19)      NOT NULL,
    NUMBER_1                NUMBER(1),
    NUMBER_3                NUMBER(3),
    NUMBER_5                NUMBER(5),
    NUMBER_10               NUMBER(10),
    NUMBER_19               NUMBER(19),
    NUMBER_18_4             NUMBER(18, 4),
    NUMBER_NO_PRECISION     NUMBER,
    FLOAT_COL               FLOAT,
    FLOAT_126               FLOAT(126),
    BINARY_FLOAT_COL        BINARY_FLOAT,
    BINARY_DOUBLE_COL       BINARY_DOUBLE,
    CHAR_COL                CHAR(10),
    NCHAR_COL               NCHAR(10),
    VARCHAR2_COL            VARCHAR2(255),
    NVARCHAR2_COL           NVARCHAR2(255),
    CLOB_COL                CLOB,
    NCLOB_COL               NCLOB,
    BLOB_COL                BLOB,
    RAW_COL                 RAW(2000),
    DATE_COL                DATE,
    TIMESTAMP_COL           TIMESTAMP,
    TIMESTAMP_6             TIMESTAMP(6),
    TIMESTAMP_TZ            TIMESTAMP WITH TIME ZONE,
    TIMESTAMP_LTZ           TIMESTAMP WITH LOCAL TIME ZONE,
    INTERVAL_YM             INTERVAL YEAR TO MONTH,
    INTERVAL_DS             INTERVAL DAY TO SECOND,
    ROWID_COL               ROWID,
    UROWID_COL              UROWID,
    XML_COL                 XMLTYPE,
    CONSTRAINT PK_ALL_COLUMN_TYPES PRIMARY KEY (ID)
);

COMMENT ON TABLE  ALL_COLUMN_TYPES                     IS 'One column per Oracle type, to exercise the language mapping';
COMMENT ON COLUMN ALL_COLUMN_TYPES.NUMBER_1            IS 'NUMBER(1) is how Oracle spells a bool';
COMMENT ON COLUMN ALL_COLUMN_TYPES.NUMBER_NO_PRECISION IS 'Arbitrary precision, so decimal is the only safe mapping';

INSERT INTO ALL_COLUMN_TYPES (ID) VALUES (1234);

-- LONG is deprecated and Oracle allows only one per table, so it gets its own. It is also the type
-- ALL_TAB_COLS.DATA_DEFAULT itself uses, which is why the reader sets InitialLONGFetchSize = -1.
CREATE TABLE LONG_COLUMN_TABLE
(
    ID       NUMBER(10) NOT NULL,
    NOTES    LONG,
    CONSTRAINT PK_LONG_COLUMN_TABLE PRIMARY KEY (ID)
);

-- A BFILE points at a file on the server's filesystem. It has no SQL Server equivalent at all.
CREATE TABLE BFILE_TABLE
(
    ID        NUMBER(10) NOT NULL,
    DOCUMENT  BFILE,
    CONSTRAINT PK_BFILE_TABLE PRIMARY KEY (ID)
);


-- ---------------------------------------------------------------------------------------------------------
-- A table where every column is nullable and there is no key at all. EF cannot use it; the generator is
-- expected to emit it inside a comment saying so.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE ALL_COLUMNS_NULL
(
    A NUMBER(10),
    B VARCHAR2(50),
    C DATE
);


-- ---------------------------------------------------------------------------------------------------------
-- The three ways an Oracle column gets its value generated. GENERATED ALWAYS and GENERATED BY DEFAULT
-- arrived in 12c; before that everyone used a sequence and a BEFORE INSERT trigger, which is still the
-- commonest thing in the field and is the reason the reader joins ALL_DEPENDENCIES to ALL_TRIGGERS.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE IDENTITY_ALWAYS
(
    ID           NUMBER GENERATED ALWAYS AS IDENTITY,
    DESCRIPTION  VARCHAR2(50),
    CONSTRAINT PK_IDENTITY_ALWAYS PRIMARY KEY (ID)
);

CREATE TABLE IDENTITY_BY_DEFAULT
(
    ID           NUMBER GENERATED BY DEFAULT AS IDENTITY,
    DESCRIPTION  VARCHAR2(50),
    CONSTRAINT PK_IDENTITY_BY_DEFAULT PRIMARY KEY (ID)
);

CREATE SEQUENCE SEQ_LEGACY_IDENTITY START WITH 100 INCREMENT BY 5 MAXVALUE 100000 CYCLE;
CREATE SEQUENCE SEQ_STANDALONE      START WITH 1   INCREMENT BY 1 NOCYCLE;

CREATE TABLE LEGACY_IDENTITY
(
    ID           NUMBER(10) NOT NULL,
    DESCRIPTION  VARCHAR2(50),
    CONSTRAINT PK_LEGACY_IDENTITY PRIMARY KEY (ID)
);

CREATE OR REPLACE TRIGGER TR_LEGACY_IDENTITY_BI
    BEFORE INSERT ON LEGACY_IDENTITY
    FOR EACH ROW
BEGIN
    IF :NEW.ID IS NULL THEN
        SELECT SEQ_LEGACY_IDENTITY.NEXTVAL INTO :NEW.ID FROM DUAL;
    END IF;
END;
/


-- ---------------------------------------------------------------------------------------------------------
-- Defaults of every shape, plus a virtual column - Oracle's computed column, which is always calculated on
-- read and never stored. Note DEFAULT ON NULL, which Oracle has and SQL Server does not.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE DEFAULTS_AND_GENERATED
(
    ID             NUMBER GENERATED BY DEFAULT AS IDENTITY,
    QUANTITY       NUMBER(10)      DEFAULT 1           NOT NULL,
    UNIT_PRICE     NUMBER(18, 4)   DEFAULT 9.99        NOT NULL,
    DESCRIPTION    VARCHAR2(50)    DEFAULT 'Hello world' NOT NULL,
    IS_ACTIVE      NUMBER(1)       DEFAULT 1           NOT NULL,
    EXTERNAL_REF   VARCHAR2(36)    DEFAULT SYS_GUID()  NOT NULL,
    CREATED_AT     TIMESTAMP       DEFAULT SYSTIMESTAMP NOT NULL,
    CREATED_DATE   DATE            DEFAULT SYSDATE     NOT NULL,
    ON_NULL_COL    VARCHAR2(20)    DEFAULT ON NULL 'fallback' NOT NULL,
    THE_WORD_NULL  VARCHAR2(20)    DEFAULT 'NULL',
    LINE_TOTAL     NUMBER(20, 4)   GENERATED ALWAYS AS (QUANTITY * UNIT_PRICE) VIRTUAL,
    CONSTRAINT PK_DEFAULTS_AND_GENERATED PRIMARY KEY (ID)
);

COMMENT ON TABLE DEFAULTS_AND_GENERATED IS 'Column defaults and a virtual column';

-- ---------------------------------------------------------------------------------------------------------
-- Defaults that are expressions rather than literals. DATA_DEFAULT is the text as written, so USER (no
-- brackets), SYS_CONTEXT(...) and SYSDATE + 30 all arrive unquoted while the literals keep their quotes.
-- Expected: the expression columns get HasDefaultValueSql and no constructor default; the literals keep theirs.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE EXPRESSION_DEFAULT
(
    ID             NUMBER GENERATED BY DEFAULT AS IDENTITY,
    CHANGED_BY     VARCHAR2(128)   DEFAULT USER        NOT NULL,
    OS_USER        VARCHAR2(128)   DEFAULT SYS_CONTEXT('USERENV', 'OS_USER'),
    CREATED_TEXT   VARCHAR2(30)    DEFAULT TO_CHAR(SYSDATE, 'YYYY-MM-DD'),
    DUE_AT         DATE            DEFAULT SYSDATE + 30 NOT NULL,
    LITERAL_TEXT   VARCHAR2(20)    DEFAULT 'fallback'  NOT NULL,
    NUMBER_TEXT    VARCHAR2(10)    DEFAULT 0           NOT NULL,
    CONSTRAINT PK_EXPRESSION_DEFAULT PRIMARY KEY (ID)
);

COMMENT ON TABLE EXPRESSION_DEFAULT IS 'Expression defaults, and literals that must not be mistaken for them';

-- ---------------------------------------------------------------------------------------------------------
-- String defaults that need escaping in C# (#281, #893). A backslash makes the default a verbatim literal,
-- where a double quote is escaped by doubling it rather than with \", and the generator left those quotes
-- single, so a default with both did not compile. Expected: QuoteOnly = "say \"hi\"", BackslashOnly =
-- @"C:\Temp", BackslashAndQuote = @"C:\Temp\say ""hi""" and QuoteThenBackslash = @"""quoted""\".
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE STRING_DEFAULT_ESCAPING
(
    ID                   NUMBER GENERATED BY DEFAULT AS IDENTITY,
    QUOTE_ONLY           VARCHAR2(30)    DEFAULT 'say "hi"'          NOT NULL,
    BACKSLASH_ONLY       VARCHAR2(30)    DEFAULT 'C:\Temp'           NOT NULL,
    BACKSLASH_AND_QUOTE  VARCHAR2(30)    DEFAULT 'C:\Temp\say "hi"'  NOT NULL,
    QUOTE_THEN_BACKSLASH VARCHAR2(30)    DEFAULT '"quoted"\'         NOT NULL,
    CONSTRAINT PK_STRING_DEFAULT_ESCAPING PRIMARY KEY (ID)
);

COMMENT ON TABLE STRING_DEFAULT_ESCAPING IS 'String defaults containing double quotes and backslashes';


-- ---------------------------------------------------------------------------------------------------------
-- Keys. Composite primary key, composite foreign key (whose columns pair by POSITION, not by order of
-- appearance), a self reference, two foreign keys onto the same parent, a foreign key to a unique
-- constraint rather than the primary key, a one to one where the child's key is also its foreign key, and a
-- DISABLEd foreign key, which is Oracle's NOCHECK and must come back as IsNotEnforced.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE PARENT_COMPOSITE
(
    KEY_ONE   NUMBER(10)   NOT NULL,
    KEY_TWO   NUMBER(10)   NOT NULL,
    ALT_KEY   NUMBER(10)   NOT NULL,
    NAME      VARCHAR2(50) NOT NULL,
    CONSTRAINT PK_PARENT_COMPOSITE PRIMARY KEY (KEY_ONE, KEY_TWO),
    CONSTRAINT UQ_PARENT_COMPOSITE_ALT UNIQUE (ALT_KEY)
);

CREATE TABLE CHILD_COMPOSITE
(
    CHILD_ID        NUMBER GENERATED BY DEFAULT AS IDENTITY,
    PARENT_KEY_ONE  NUMBER(10) NOT NULL,
    PARENT_KEY_TWO  NUMBER(10) NOT NULL,
    DESCRIPTION     VARCHAR2(50),
    CONSTRAINT PK_CHILD_COMPOSITE PRIMARY KEY (CHILD_ID),
    CONSTRAINT FK_CHILD_COMPOSITE_PARENT FOREIGN KEY (PARENT_KEY_ONE, PARENT_KEY_TWO)
        REFERENCES PARENT_COMPOSITE (KEY_ONE, KEY_TWO)
);

CREATE INDEX IX_CHILD_COMPOSITE_PARENT ON CHILD_COMPOSITE (PARENT_KEY_ONE, PARENT_KEY_TWO);

CREATE TABLE PRINCIPAL_KEY_CHILD
(
    ID              NUMBER GENERATED BY DEFAULT AS IDENTITY,
    PARENT_ALT_KEY  NUMBER(10) NOT NULL,
    CONSTRAINT PK_PRINCIPAL_KEY_CHILD PRIMARY KEY (ID),
    CONSTRAINT FK_PRINCIPAL_KEY_CHILD FOREIGN KEY (PARENT_ALT_KEY)
        REFERENCES PARENT_COMPOSITE (ALT_KEY)
);

CREATE TABLE EMPLOYEE
(
    EMPLOYEE_ID  NUMBER GENERATED BY DEFAULT AS IDENTITY,
    MANAGER_ID   NUMBER(19),
    FULL_NAME    VARCHAR2(100) NOT NULL,
    CONSTRAINT PK_EMPLOYEE PRIMARY KEY (EMPLOYEE_ID),
    CONSTRAINT FK_EMPLOYEE_MANAGER FOREIGN KEY (MANAGER_ID) REFERENCES EMPLOYEE (EMPLOYEE_ID)
);

CREATE TABLE AIRPORT
(
    AIRPORT_ID  NUMBER(10)   NOT NULL,
    NAME        VARCHAR2(50) NOT NULL,
    CONSTRAINT PK_AIRPORT PRIMARY KEY (AIRPORT_ID)
);

CREATE TABLE FLIGHT
(
    FLIGHT_ID             NUMBER GENERATED BY DEFAULT AS IDENTITY,
    DEPARTURE_AIRPORT_ID  NUMBER(10) NOT NULL,
    ARRIVAL_AIRPORT_ID    NUMBER(10) NOT NULL,
    CONSTRAINT PK_FLIGHT PRIMARY KEY (FLIGHT_ID),
    CONSTRAINT FK_FLIGHT_DEPARTURE FOREIGN KEY (DEPARTURE_AIRPORT_ID) REFERENCES AIRPORT (AIRPORT_ID),
    CONSTRAINT FK_FLIGHT_ARRIVAL   FOREIGN KEY (ARRIVAL_AIRPORT_ID)   REFERENCES AIRPORT (AIRPORT_ID)
);

CREATE TABLE PERSON
(
    PERSON_ID  NUMBER GENERATED BY DEFAULT AS IDENTITY,
    FULL_NAME  VARCHAR2(100) NOT NULL,
    CONSTRAINT PK_PERSON PRIMARY KEY (PERSON_ID)
);

CREATE TABLE PERSON_PHOTO
(
    PERSON_ID  NUMBER NOT NULL,
    PHOTO      BLOB   NOT NULL,
    CONSTRAINT PK_PERSON_PHOTO PRIMARY KEY (PERSON_ID),
    CONSTRAINT FK_PERSON_PHOTO FOREIGN KEY (PERSON_ID) REFERENCES PERSON (PERSON_ID)
);

CREATE TABLE UNENFORCED_PARENT
(
    ID NUMBER(10) NOT NULL,
    CONSTRAINT PK_UNENFORCED_PARENT PRIMARY KEY (ID)
);

CREATE TABLE UNENFORCED_CHILD
(
    ID         NUMBER GENERATED BY DEFAULT AS IDENTITY,
    PARENT_ID  NUMBER(10),
    CONSTRAINT PK_UNENFORCED_CHILD PRIMARY KEY (ID),
    CONSTRAINT FK_UNENFORCED_CHILD FOREIGN KEY (PARENT_ID)
        REFERENCES UNENFORCED_PARENT (ID) DISABLE
);

CREATE TABLE STUDENT
(
    STUDENT_ID  NUMBER GENERATED BY DEFAULT AS IDENTITY,
    FULL_NAME   VARCHAR2(100) NOT NULL,
    CONSTRAINT PK_STUDENT PRIMARY KEY (STUDENT_ID)
);

CREATE TABLE COURSE
(
    COURSE_ID  NUMBER GENERATED BY DEFAULT AS IDENTITY,
    TITLE      VARCHAR2(100) NOT NULL,
    CONSTRAINT PK_COURSE PRIMARY KEY (COURSE_ID)
);

CREATE TABLE STUDENT_COURSE
(
    STUDENT_ID  NUMBER NOT NULL,
    COURSE_ID   NUMBER NOT NULL,
    CONSTRAINT PK_STUDENT_COURSE PRIMARY KEY (STUDENT_ID, COURSE_ID),
    CONSTRAINT FK_STUDENT_COURSE_STUDENT FOREIGN KEY (STUDENT_ID) REFERENCES STUDENT (STUDENT_ID),
    CONSTRAINT FK_STUDENT_COURSE_COURSE  FOREIGN KEY (COURSE_ID)  REFERENCES COURSE (COURSE_ID)
);

-- A primary key whose columns are declared in a different order from the column order, so the reader has to
-- honour the constraint's own POSITION rather than the table's column order.
CREATE TABLE PK_ORDINAL_TEST
(
    FILLER      VARCHAR2(10),
    SECOND_KEY  NUMBER(10) NOT NULL,
    FIRST_KEY   NUMBER(10) NOT NULL,
    CONSTRAINT PK_PK_ORDINAL_TEST PRIMARY KEY (FIRST_KEY, SECOND_KEY)
);

CREATE TABLE NO_PRIMARY_KEY
(
    A NUMBER(10)   NOT NULL,
    B VARCHAR2(50) NOT NULL
);

-- Index-organised table: the rows live in the primary key index. Oracle only; there is no heap.
CREATE TABLE IOT_TABLE
(
    ID     NUMBER(10)   NOT NULL,
    VALUE1 VARCHAR2(50),
    CONSTRAINT PK_IOT_TABLE PRIMARY KEY (ID)
) ORGANIZATION INDEX;


-- ---------------------------------------------------------------------------------------------------------
-- Names chosen to break naive generators. Oracle folds unquoted identifiers to UPPER case, so anything that
-- needs to keep its shape has to be quoted - and quoting is what lets spaces, hyphens, leading digits,
-- reserved words and non-ASCII through.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE "Spaced Table Name"
(
    "Spaced Table Name"    NUMBER GENERATED BY DEFAULT AS IDENTITY,
    "Column With Spaces"   VARCHAR2(50),
    "Column-With-Hyphens"  VARCHAR2(50),
    "1_leading_digit"      NUMBER(10),
    "select"               VARCHAR2(50),
    "class"                VARCHAR2(50),
    "event"                VARCHAR2(50),
    "namespace"            VARCHAR2(50),
    "string"               VARCHAR2(50),
    "table.with.periods"   VARCHAR2(50),
    CONSTRAINT "PK Spaced Table Name" PRIMARY KEY ("Spaced Table Name")
);

COMMENT ON TABLE "Spaced Table Name" IS 'Every identifier here needs quoting';

-- Names differing only by case, which C# cannot express as two properties.
CREATE TABLE CASE_ONLY_DIFFERENCE
(
    ID      NUMBER(10) NOT NULL,
    "Value" VARCHAR2(10),
    "value" VARCHAR2(10),
    CONSTRAINT PK_CASE_ONLY_DIFFERENCE PRIMARY KEY (ID)
);

-- ALLCAPS names ending in IES, which broke pluralisation. In Oracle everything unquoted is ALLCAPS anyway,
-- which is precisely why this dialect is the one most likely to hit it.
CREATE TABLE CURRENCIES
(
    ID   NUMBER(10) NOT NULL,
    CODE CHAR(3)    NOT NULL,
    CONSTRAINT PK_CURRENCIES PRIMARY KEY (ID)
);

CREATE TABLE CATEGORIES
(
    ID   NUMBER(10)   NOT NULL,
    NAME VARCHAR2(50) NOT NULL,
    CONSTRAINT PK_CATEGORIES PRIMARY KEY (ID)
);


-- ---------------------------------------------------------------------------------------------------------
-- Enum lookup tables, both with and without a group column.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE STATUS
(
    ID   NUMBER(10)   NOT NULL,
    NAME VARCHAR2(10) NOT NULL,
    CONSTRAINT PK_STATUS PRIMARY KEY (ID)
);

INSERT INTO STATUS (ID, NAME) VALUES (1, 'Todo');
INSERT INTO STATUS (ID, NAME) VALUES (2, 'InProgress');
INSERT INTO STATUS (ID, NAME) VALUES (3, 'Done');

CREATE TABLE ORDER_STATUS
(
    ORDER_STATUS_ID NUMBER(10)    NOT NULL,
    NAME            VARCHAR2(30)  NOT NULL,
    STATUS_GROUP    VARCHAR2(20)  NOT NULL,
    DESCRIPTION     VARCHAR2(100),
    CONSTRAINT PK_ORDER_STATUS PRIMARY KEY (ORDER_STATUS_ID)
);

INSERT INTO ORDER_STATUS (ORDER_STATUS_ID, NAME, STATUS_GROUP, DESCRIPTION) VALUES (1, 'Pending',   'Open',   'Not yet processed');
INSERT INTO ORDER_STATUS (ORDER_STATUS_ID, NAME, STATUS_GROUP, DESCRIPTION) VALUES (2, 'Picking',   'Open',   'Being picked');
INSERT INTO ORDER_STATUS (ORDER_STATUS_ID, NAME, STATUS_GROUP, DESCRIPTION) VALUES (3, 'Shipped',   'Closed', 'On its way');
INSERT INTO ORDER_STATUS (ORDER_STATUS_ID, NAME, STATUS_GROUP, DESCRIPTION) VALUES (4, 'Cancelled', 'Closed', 'Cancelled by the customer');


-- ---------------------------------------------------------------------------------------------------------
-- Indexes. A unique constraint and a unique index are different objects in ALL_CONSTRAINTS and ALL_INDEXES,
-- and the reader has to tell them apart. A function-based index is Oracle's expression index.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE INDEX_TEST
(
    ID          NUMBER GENERATED BY DEFAULT AS IDENTITY,
    UNIQUE_COL  NUMBER(10)   NOT NULL,
    LOWER_ME    VARCHAR2(50) NOT NULL,
    A           NUMBER(10)   NOT NULL,
    B           NUMBER(10)   NOT NULL,
    CONSTRAINT PK_INDEX_TEST PRIMARY KEY (ID),
    CONSTRAINT UQ_INDEX_TEST_UNIQUE_COL UNIQUE (UNIQUE_COL)
);

CREATE UNIQUE INDEX UX_INDEX_TEST_A_B   ON INDEX_TEST (A, B);
CREATE        INDEX IX_INDEX_TEST_LOWER ON INDEX_TEST (LOWER(LOWER_ME));


-- ---------------------------------------------------------------------------------------------------------
-- Views, a materialised view and a synonym. Oracle is the only other dialect besides SQL Server that has
-- synonyms, so this is where that reader path gets exercised.
-- ---------------------------------------------------------------------------------------------------------
CREATE OR REPLACE VIEW ACTIVE_CHILDREN AS
    SELECT C.CHILD_ID, C.DESCRIPTION, P.NAME AS PARENT_NAME
    FROM CHILD_COMPOSITE C
    INNER JOIN PARENT_COMPOSITE P
        ON P.KEY_ONE = C.PARENT_KEY_ONE AND P.KEY_TWO = C.PARENT_KEY_TWO;

COMMENT ON TABLE ACTIVE_CHILDREN IS 'A view has no keys and no relationships';

CREATE OR REPLACE VIEW ALL_COLUMNS_NULL_VIEW AS
    SELECT A, B, C FROM ALL_COLUMNS_NULL;

CREATE MATERIALIZED VIEW ORDER_STATUS_SUMMARY AS
    SELECT STATUS_GROUP, COUNT(*) AS STATUS_COUNT
    FROM ORDER_STATUS
    GROUP BY STATUS_GROUP;

CREATE SYNONYM SYN_PARENT_COMPOSITE FOR PARENT_COMPOSITE;
CREATE SYNONYM SYN_ACTIVE_CHILDREN  FOR ACTIVE_CHILDREN;


-- ---------------------------------------------------------------------------------------------------------
-- A trigger on an ordinary table, reported separately from the table it hangs off.
-- ---------------------------------------------------------------------------------------------------------
CREATE OR REPLACE TRIGGER TR_CHILD_COMPOSITE_BI
    BEFORE INSERT ON CHILD_COMPOSITE
    FOR EACH ROW
BEGIN
    IF :NEW.DESCRIPTION IS NULL THEN
        :NEW.DESCRIPTION := 'stamped';
    END IF;
END;
/


-- ---------------------------------------------------------------------------------------------------------
-- Routines. Oracle procedures return result sets through a REF CURSOR OUT parameter rather than by simply
-- selecting, which is the single biggest difference from SQL Server and the reason a procedure's shape
-- cannot be inferred from its return type. Standalone routines are in ALL_ARGUMENTS with a NULL
-- PACKAGE_NAME; packaged ones carry the package name, which is how the reader tells them apart.
-- ---------------------------------------------------------------------------------------------------------

-- Scalar function.
CREATE OR REPLACE FUNCTION LINE_TOTAL(QUANTITY IN NUMBER, UNIT_PRICE IN NUMBER)
    RETURN NUMBER
IS
BEGIN
    RETURN QUANTITY * UNIT_PRICE;
END;
/

-- Scalar function with a default argument and a reserved word as a parameter name.
CREATE OR REPLACE FUNCTION IS_HIGH_VALUE("VALUE" IN NUMBER, THRESHOLD IN NUMBER DEFAULT 1000)
    RETURN NUMBER
IS
BEGIN
    RETURN CASE WHEN "VALUE" > THRESHOLD THEN 1 ELSE 0 END;
END;
/

-- A function with no parameters at all.
CREATE OR REPLACE FUNCTION NO_PARAMETERS
    RETURN NUMBER
IS
BEGIN
    RETURN 42;
END;
/

-- Procedure with every parameter mode.
CREATE OR REPLACE PROCEDURE COUNT_CHILDREN
(
    PARENT_KEY_ONE  IN     NUMBER,
    CHILD_COUNT        OUT NUMBER,
    RUNNING_TOTAL   IN OUT NUMBER
)
IS
BEGIN
    SELECT COUNT(*) INTO CHILD_COUNT
    FROM CHILD_COMPOSITE C
    WHERE C.PARENT_KEY_ONE = COUNT_CHILDREN.PARENT_KEY_ONE;
    RUNNING_TOTAL := RUNNING_TOTAL + CHILD_COUNT;
END;
/

-- Procedure returning a result set the only way Oracle can: a REF CURSOR OUT parameter.
CREATE OR REPLACE PROCEDURE GET_CHILDREN_BY_PARENT
(
    PARENT_KEY_ONE  IN  NUMBER,
    PARENT_KEY_TWO  IN  NUMBER,
    RESULTS         OUT SYS_REFCURSOR
)
IS
BEGIN
    OPEN RESULTS FOR
        SELECT CHILD_ID, DESCRIPTION
        FROM CHILD_COMPOSITE C
        WHERE C.PARENT_KEY_ONE = GET_CHILDREN_BY_PARENT.PARENT_KEY_ONE
          AND C.PARENT_KEY_TWO = GET_CHILDREN_BY_PARENT.PARENT_KEY_TWO;
END;
/

-- Procedure with no parameters.
CREATE OR REPLACE PROCEDURE NO_PARAMETERS_PROC
IS
BEGIN
    UPDATE PARENT_COMPOSITE SET NAME = NAME;
END;
/

-- A package, whose members carry a PACKAGE_NAME in ALL_ARGUMENTS.
CREATE OR REPLACE PACKAGE ORDER_PKG AS
    FUNCTION  DOUBLE_IT(N IN NUMBER) RETURN NUMBER;
    PROCEDURE TOUCH_PARENT(KEY_ONE IN NUMBER, KEY_TWO IN NUMBER);
END ORDER_PKG;
/

CREATE OR REPLACE PACKAGE BODY ORDER_PKG AS
    FUNCTION DOUBLE_IT(N IN NUMBER) RETURN NUMBER IS
    BEGIN
        RETURN N * 2;
    END DOUBLE_IT;

    PROCEDURE TOUCH_PARENT(KEY_ONE IN NUMBER, KEY_TWO IN NUMBER) IS
    BEGIN
        UPDATE PARENT_COMPOSITE P
        SET P.NAME = P.NAME
        WHERE P.KEY_ONE = TOUCH_PARENT.KEY_ONE
          AND P.KEY_TWO = TOUCH_PARENT.KEY_TWO;
    END TOUCH_PARENT;
END ORDER_PKG;
/


-- ---------------------------------------------------------------------------------------------------------
-- Seed rows, so anything that executes rather than merely reads has something to work with.
-- ---------------------------------------------------------------------------------------------------------
INSERT INTO PARENT_COMPOSITE (KEY_ONE, KEY_TWO, ALT_KEY, NAME) VALUES (1, 1, 10, 'First parent');
INSERT INTO PARENT_COMPOSITE (KEY_ONE, KEY_TWO, ALT_KEY, NAME) VALUES (1, 2, 20, 'Second parent');

INSERT INTO CHILD_COMPOSITE (PARENT_KEY_ONE, PARENT_KEY_TWO, DESCRIPTION) VALUES (1, 1, 'First child');
INSERT INTO CHILD_COMPOSITE (PARENT_KEY_ONE, PARENT_KEY_TWO, DESCRIPTION) VALUES (1, 1, NULL);
INSERT INTO CHILD_COMPOSITE (PARENT_KEY_ONE, PARENT_KEY_TWO, DESCRIPTION) VALUES (1, 2, 'Third child');

INSERT INTO AIRPORT (AIRPORT_ID, NAME) VALUES (1, 'Manchester');
INSERT INTO AIRPORT (AIRPORT_ID, NAME) VALUES (2, 'Heathrow');
INSERT INTO FLIGHT (DEPARTURE_AIRPORT_ID, ARRIVAL_AIRPORT_ID) VALUES (1, 2);

COMMIT;
