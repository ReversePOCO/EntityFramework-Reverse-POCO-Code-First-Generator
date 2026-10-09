-- ---------------------------------------------------------------------------------------------------------
-- EfrpgTestSynonyms for Oracle.
--
-- The way synonyms are used in the field: one schema owns the tables and code, and the application connects as
-- a second schema that owns almost nothing and reaches everything through private synonyms. Oracle reads one
-- schema per run, so without synonym support the generator sees an empty application schema. EfrpgTest.sql keeps
-- everything in one schema on purpose, so its two synonyms only cover the same-schema case; this covers the rest.
--
--   EFRPGSYNOWNER  owns the tables, a view, a procedure, a function and a package, and grants them to EFRPGSYN.
--   EFRPGSYN       owns one table of its own, REVIEW, and private synonyms for everything else.
--
-- Every synonym has a name different from its target, so a test can tell which of the two came through.
--
-- It creates both users itself and qualifies every name, so it runs as SYSDBA in one go, and drops both users
-- first, so it can be re-run:
--
--   docker exec -i efrpg-oracle sqlplus -s sys/efrpgTest123@localhost:1521/pdb1 as sysdba < EfrpgTestSynonyms.sql
--
-- docker-compose.yml also loads it on a fresh volume, after northwind.sql.
--
-- Read by Efrpg.Tests.Unit/Integration/OracleSynonymIntegrationTests in the Efrpg repository, and by
-- SingleDatabaseTestOracle.Synonyms here. The expected names in both come from this file: change one, change all.
-- ---------------------------------------------------------------------------------------------------------

SET DEFINE OFF
SET SQLBLANKLINES ON

ALTER SESSION SET CONTAINER=pdb1;

BEGIN
    FOR s IN (SELECT SYNONYM_NAME FROM DBA_SYNONYMS WHERE OWNER = 'PUBLIC' AND SYNONYM_NAME = 'EFRPGSYN_PUBLIC_CUSTOMER') LOOP
        EXECUTE IMMEDIATE 'DROP PUBLIC SYNONYM ' || s.SYNONYM_NAME;
    END LOOP;

    FOR u IN (SELECT USERNAME FROM DBA_USERS WHERE USERNAME IN ('EFRPGSYN', 'EFRPGSYNOWNER')) LOOP
        EXECUTE IMMEDIATE 'DROP USER ' || u.USERNAME || ' CASCADE';
    END LOOP;
END;
/

CREATE USER efrpgsynowner IDENTIFIED BY abc123 DEFAULT TABLESPACE USERS QUOTA UNLIMITED ON USERS;
CREATE USER efrpgsyn      IDENTIFIED BY abc123 DEFAULT TABLESPACE USERS QUOTA UNLIMITED ON USERS;
GRANT CREATE SESSION TO efrpgsynowner;
GRANT CREATE SESSION TO efrpgsyn;


-- ---------------------------------------------------------------------------------------------------------
-- The owner. ORDERS to CUSTOMER is a plain foreign key, ORDER_LINE to ORDERS cascades, and LINE_NOTE to
-- ORDER_LINE is composite - all three reached from the application schema through synonyms on both sides.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE efrpgsynowner.CUSTOMER
(
    CUSTOMER_ID NUMBER(10)    NOT NULL,
    NAME        VARCHAR2(100) NOT NULL,
    CONSTRAINT PK_CUSTOMER PRIMARY KEY (CUSTOMER_ID)
);

CREATE TABLE efrpgsynowner.ORDERS
(
    ORDER_ID    NUMBER(10)          NOT NULL,
    CUSTOMER_ID NUMBER(10)          NOT NULL,
    PLACED_ON   DATE DEFAULT SYSDATE NOT NULL,
    CONSTRAINT PK_ORDERS PRIMARY KEY (ORDER_ID),
    CONSTRAINT FK_ORDERS_CUSTOMER FOREIGN KEY (CUSTOMER_ID) REFERENCES efrpgsynowner.CUSTOMER (CUSTOMER_ID)
);

CREATE TABLE efrpgsynowner.ORDER_LINE
(
    ORDER_ID NUMBER(10) NOT NULL,
    LINE_NO  NUMBER(5)  NOT NULL,
    QUANTITY NUMBER(5)  NOT NULL,
    CONSTRAINT PK_ORDER_LINE PRIMARY KEY (ORDER_ID, LINE_NO),
    CONSTRAINT FK_ORDER_LINE_ORDERS FOREIGN KEY (ORDER_ID) REFERENCES efrpgsynowner.ORDERS (ORDER_ID) ON DELETE CASCADE
);

CREATE TABLE efrpgsynowner.LINE_NOTE
(
    NOTE_ID  NUMBER(10) NOT NULL,
    ORDER_ID NUMBER(10) NOT NULL,
    LINE_NO  NUMBER(5)  NOT NULL,
    NOTE     VARCHAR2(200),
    CONSTRAINT PK_LINE_NOTE PRIMARY KEY (NOTE_ID),
    CONSTRAINT FK_LINE_NOTE_ORDER_LINE FOREIGN KEY (ORDER_ID, LINE_NO)
        REFERENCES efrpgsynowner.ORDER_LINE (ORDER_ID, LINE_NO)
);

-- Never granted. The application schema has a synonym for it but cannot see it, so it must not be generated.
CREATE TABLE efrpgsynowner.SECRET
(
    SECRET_ID NUMBER(10) NOT NULL,
    CONSTRAINT PK_SECRET PRIMARY KEY (SECRET_ID)
);

CREATE OR REPLACE VIEW efrpgsynowner.CUSTOMER_SUMMARY AS
    SELECT C.CUSTOMER_ID, C.NAME, COUNT(O.ORDER_ID) AS ORDER_COUNT
    FROM efrpgsynowner.CUSTOMER C
    LEFT OUTER JOIN efrpgsynowner.ORDERS O
        ON O.CUSTOMER_ID = C.CUSTOMER_ID
    GROUP BY C.CUSTOMER_ID, C.NAME;

CREATE OR REPLACE PROCEDURE efrpgsynowner.PLACE_ORDER(P_CUSTOMER_ID IN NUMBER, P_ORDER_ID OUT NUMBER) AS
BEGIN
    SELECT NVL(MAX(ORDER_ID), 0) + 1 INTO P_ORDER_ID FROM efrpgsynowner.ORDERS;
    INSERT INTO efrpgsynowner.ORDERS (ORDER_ID, CUSTOMER_ID) VALUES (P_ORDER_ID, P_CUSTOMER_ID);
END PLACE_ORDER;
/

CREATE OR REPLACE FUNCTION efrpgsynowner.ORDER_COUNT(P_CUSTOMER_ID IN NUMBER) RETURN NUMBER AS
    V_COUNT NUMBER;
BEGIN
    SELECT COUNT(*) INTO V_COUNT FROM efrpgsynowner.ORDERS WHERE CUSTOMER_ID = P_CUSTOMER_ID;
    RETURN V_COUNT;
END ORDER_COUNT;
/

CREATE OR REPLACE PACKAGE efrpgsynowner.ORDER_API AS
    PROCEDURE CANCEL_ORDER(P_ORDER_ID IN NUMBER);
END ORDER_API;
/

CREATE OR REPLACE PACKAGE BODY efrpgsynowner.ORDER_API AS
    PROCEDURE CANCEL_ORDER(P_ORDER_ID IN NUMBER) IS
    BEGIN
        DELETE FROM efrpgsynowner.ORDERS WHERE ORDER_ID = P_ORDER_ID;
    END CANCEL_ORDER;
END ORDER_API;
/

-- REFERENCES as well as SELECT on CUSTOMER, because EFRPGSYN.REVIEW has a foreign key to it.
GRANT SELECT, INSERT, UPDATE, DELETE ON efrpgsynowner.CUSTOMER   TO efrpgsyn;
GRANT REFERENCES                     ON efrpgsynowner.CUSTOMER   TO efrpgsyn;
GRANT SELECT, INSERT, UPDATE, DELETE ON efrpgsynowner.ORDERS     TO efrpgsyn;
GRANT SELECT, INSERT, UPDATE, DELETE ON efrpgsynowner.ORDER_LINE TO efrpgsyn;
GRANT SELECT, INSERT, UPDATE, DELETE ON efrpgsynowner.LINE_NOTE  TO efrpgsyn;
GRANT SELECT                         ON efrpgsynowner.CUSTOMER_SUMMARY TO efrpgsyn;
GRANT EXECUTE ON efrpgsynowner.PLACE_ORDER TO efrpgsyn;
GRANT EXECUTE ON efrpgsynowner.ORDER_COUNT TO efrpgsyn;
GRANT EXECUTE ON efrpgsynowner.ORDER_API   TO efrpgsyn;


-- ---------------------------------------------------------------------------------------------------------
-- The application schema's own table. Its parent is behind a synonym, so without synonyms its foreign key
-- names a table the generator never sees; with them the parent must come through as CLIENT. FEEDBACK is a
-- synonym for it in the same schema, which is also where ALL_TRIGGERS can see the trigger behind a synonym.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE efrpgsyn.REVIEW
(
    REVIEW_ID   NUMBER(10) NOT NULL,
    CUSTOMER_ID NUMBER(10) NOT NULL,
    REVIEW_TEXT VARCHAR2(400),
    CONSTRAINT PK_REVIEW PRIMARY KEY (REVIEW_ID),
    CONSTRAINT FK_REVIEW_CUSTOMER FOREIGN KEY (CUSTOMER_ID) REFERENCES efrpgsynowner.CUSTOMER (CUSTOMER_ID)
);

CREATE OR REPLACE TRIGGER efrpgsyn.TR_REVIEW_BI
    BEFORE INSERT ON efrpgsyn.REVIEW
    FOR EACH ROW
BEGIN
    :NEW.REVIEW_TEXT := NVL(:NEW.REVIEW_TEXT, 'No comment');
END;
/


-- ---------------------------------------------------------------------------------------------------------
-- Synonyms that must be generated, under the synonym's name.
-- CUSTOMER has two: the foreign keys to it must name exactly one of them, and always the same one (CLIENT).
-- ---------------------------------------------------------------------------------------------------------
CREATE SYNONYM efrpgsyn.CLIENT             FOR efrpgsynowner.CUSTOMER;
CREATE SYNONYM efrpgsyn.CUSTOMER_ALIAS     FOR efrpgsynowner.CUSTOMER;
CREATE SYNONYM efrpgsyn.CLIENT_ORDER       FOR efrpgsynowner.ORDERS;
CREATE SYNONYM efrpgsyn.ORDER_ITEM         FOR efrpgsynowner.ORDER_LINE;
CREATE SYNONYM efrpgsyn.ITEM_NOTE          FOR efrpgsynowner.LINE_NOTE;
CREATE SYNONYM efrpgsyn.CLIENT_SUMMARY     FOR efrpgsynowner.CUSTOMER_SUMMARY;
CREATE SYNONYM efrpgsyn.SUBMIT_ORDER       FOR efrpgsynowner.PLACE_ORDER;
CREATE SYNONYM efrpgsyn.CLIENT_ORDER_COUNT FOR efrpgsynowner.ORDER_COUNT;
CREATE SYNONYM efrpgsyn.ORDERS_API         FOR efrpgsynowner.ORDER_API;
CREATE SYNONYM efrpgsyn.FEEDBACK           FOR efrpgsyn.REVIEW;


-- ---------------------------------------------------------------------------------------------------------
-- Synonyms that must NOT be generated.
-- HIDDEN's target was never granted. REMOTE_CUSTOMER goes over a database link (which need not exist for the
-- synonym to be created); its TABLE_OWNER and TABLE_NAME match a local table, so a reader that forgets the
-- DB_LINK filter generates a phantom copy of CUSTOMER. CLIENT_AGAIN is a synonym for a synonym, which is out of
-- scope. The PUBLIC synonym is visible to every schema and must stay out of every model.
-- ---------------------------------------------------------------------------------------------------------
CREATE SYNONYM efrpgsyn.HIDDEN             FOR efrpgsynowner.SECRET;
CREATE SYNONYM efrpgsyn.REMOTE_CUSTOMER    FOR efrpgsynowner.CUSTOMER@EFRPG_NO_SUCH_LINK;
CREATE SYNONYM efrpgsyn.CLIENT_AGAIN       FOR efrpgsyn.CLIENT;
CREATE PUBLIC SYNONYM EFRPGSYN_PUBLIC_CUSTOMER FOR efrpgsynowner.CUSTOMER;


-- ---------------------------------------------------------------------------------------------------------
-- Seed rows, so anything that executes rather than merely reads has something to work with.
-- ---------------------------------------------------------------------------------------------------------
INSERT INTO efrpgsynowner.CUSTOMER (CUSTOMER_ID, NAME) VALUES (1, 'First customer');
INSERT INTO efrpgsynowner.ORDERS (ORDER_ID, CUSTOMER_ID) VALUES (1, 1);
INSERT INTO efrpgsynowner.ORDER_LINE (ORDER_ID, LINE_NO, QUANTITY) VALUES (1, 1, 3);
INSERT INTO efrpgsynowner.LINE_NOTE (NOTE_ID, ORDER_ID, LINE_NO, NOTE) VALUES (1, 1, 1, 'Gift wrap');
INSERT INTO efrpgsyn.REVIEW (REVIEW_ID, CUSTOMER_ID, REVIEW_TEXT) VALUES (1, 1, NULL);

COMMIT;
