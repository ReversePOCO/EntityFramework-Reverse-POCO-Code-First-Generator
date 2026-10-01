-- ---------------------------------------------------------------------------------------------------------
-- EfrpgTest for PostgreSQL.
--
-- Like the SQL Server EfrpgTest, this database is deliberately horrible. It is not a sample application: it
-- is a collection of every construct, type and naming atrocity the generator has to survive. If EFRPG can
-- reverse engineer this, it can reverse engineer whatever is out in the field.
--
-- It covers the same checklist as the SQL Server script - types, keys, defaults, generated columns, views,
-- triggers, comments, routines, enum lookups, awful identifiers - and then the things PostgreSQL has that
-- SQL Server does not: serial versus identity, arrays, enum and domain and composite types, ranges, network
-- and interval types, jsonb, materialised views, partitioned tables, inheritance, partial and expression
-- indexes, and the four different ways a function can return a set.
--
--   psql -h 127.0.0.1 -p 5432 -U testuser -d EfrpgTest -f EfrpgTest.sql
--
-- Run against an empty database. There are no GO separators: that is a SQL Server batch terminator and
-- PostgreSQL cannot parse it.
-- ---------------------------------------------------------------------------------------------------------


-- ---------------------------------------------------------------------------------------------------------
-- Schemas. "another" exists so a foreign key can cross a schema boundary, which is issue 855: navigation
-- properties were not generated unless both ends lived in the same schema.
-- ---------------------------------------------------------------------------------------------------------
CREATE SCHEMA IF NOT EXISTS another;
CREATE SCHEMA IF NOT EXISTS "Mixed Case Schema";


-- ---------------------------------------------------------------------------------------------------------
-- Types PostgreSQL has and SQL Server does not: an enum type, a domain over a base type, and a composite
-- type. All three arrive at the reader as USER-DEFINED, which is exactly why they are here.
-- ---------------------------------------------------------------------------------------------------------
CREATE TYPE public.mood AS ENUM ('sad', 'ok', 'happy');
CREATE DOMAIN public.positive_int AS integer CHECK (VALUE > 0);
CREATE TYPE public.full_name AS (first_name text, last_name text);


-- ---------------------------------------------------------------------------------------------------------
-- One column per type, so the language mapping is exercised end to end. Everything is nullable except the
-- key, which also makes this the "almost every column is null" case.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE public.AllColumnTypes
(
    "bigint"                      bigint NOT NULL,
    "bit_1"                       bit(1),
    "bit_8"                       bit(8),
    "bit_varying"                 bit varying(16),
    "boolean"                     boolean,
    "box"                         box,
    "bytea"                       bytea,
    "char"                        char,
    "character"                   character,
    "character_varying"           character varying,
    "character_varying_50"        character varying(50),
    "cid"                         cid,
    "cidr"                        cidr,
    "circle"                      circle,
    "date"                        date,
    "double_precision"            double precision,
    "inet"                        inet,
    "integer"                     integer,
    "interval"                    interval,
    "json"                        json,
    "jsonb"                       jsonb,
    "line"                        line,
    "lseg"                        lseg,
    "macaddr"                     macaddr,
    "macaddr8"                    macaddr8,
    "money"                       money,
    "name"                        name,
    "numeric"                     numeric,
    "numeric_18_4"                numeric(18, 4),
    "oid"                         oid,
    "oidvector"                   oidvector,
    "path"                        path,
    "point"                       point,
    "polygon"                     polygon,
    "real"                        real,
    "smallint"                    smallint,
    "text"                        text,
    "time_with_time_zone"         time with time zone,
    "time_without_time_zone"      time without time zone,
    "timestamp_with_time_zone"    timestamp with time zone,
    "timestamp_without_time_zone" timestamp without time zone,
    "tsquery"                     tsquery,
    "tsvector"                    tsvector,
    "uuid"                        uuid,
    "xid"                         xid,
    "xml"                         xml,
    "mood"                        public.mood,
    "positive_int"                public.positive_int,
    "full_name"                   public.full_name,
    "int_array"                   integer[],
    "text_array"                  text[],
    "int_matrix"                  integer[][],
    "int4range"                   int4range,
    "tstzrange"                   tstzrange,
    CONSTRAINT pk_AllColumnTypes PRIMARY KEY ("bigint")
);

COMMENT ON TABLE  public.AllColumnTypes            IS 'One column per PostgreSQL type, to exercise the language mapping';
COMMENT ON COLUMN public.AllColumnTypes."jsonb"    IS 'A column comment, read through pg_description with objsubid > 0';
COMMENT ON COLUMN public.AllColumnTypes."int_array" IS 'Arrays have no SQL Server equivalent';

INSERT INTO public.AllColumnTypes ("bigint") VALUES (1234);


-- ---------------------------------------------------------------------------------------------------------
-- A table where every column is nullable and there is no key at all. EF cannot use it; the generator is
-- expected to emit it inside a comment saying so.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE public.all_columns_null
(
    a integer,
    b text,
    c timestamp without time zone
);


-- ---------------------------------------------------------------------------------------------------------
-- The four ways a PostgreSQL column can generate its own value, which is the single biggest difference
-- from SQL Server. serial is the trap: information_schema reports is_identity = NO and is_generated = NEVER
-- for it, and the only evidence is a column default of nextval(...). That default must make the column an
-- identity AND must not survive into the generated C# as a property initialiser.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE public.serial_test
(
    id           serial PRIMARY KEY,
    big_id       bigserial     NOT NULL,
    small_id     smallserial   NOT NULL,
    description  text
);

CREATE TABLE public.identity_always
(
    id          integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    description text
);

CREATE TABLE public.identity_by_default
(
    id          bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    description text
);

-- A standalone sequence, and a column defaulting to it. The sequence is reported separately from the table.
CREATE SEQUENCE public.count_by_five START WITH 100 INCREMENT BY 5 MINVALUE 100 MAXVALUE 100000 CYCLE;
CREATE SEQUENCE public.count_by_one_no_cycle;

CREATE TABLE public.sequence_test
(
    id          integer NOT NULL DEFAULT nextval('public.count_by_five'),
    description text,
    CONSTRAINT pk_sequence_test PRIMARY KEY (id)
);


-- ---------------------------------------------------------------------------------------------------------
-- Defaults of every shape, plus a stored generated column. Case 156: a varchar defaulting to the literal
-- string 'NULL' must not be confused with a default of NULL.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE public.defaults_and_generated
(
    id             serial PRIMARY KEY,
    quantity       integer                     NOT NULL DEFAULT 1,
    unit_price     numeric(18, 4)              NOT NULL DEFAULT 9.99,
    description     varchar(50)                NOT NULL DEFAULT 'Hello world',
    is_active      boolean                     NOT NULL DEFAULT true,
    external_ref   uuid                        NOT NULL DEFAULT gen_random_uuid(),
    created_at     timestamp with time zone    NOT NULL DEFAULT now(),
    created_date   date                        NOT NULL DEFAULT CURRENT_DATE,
    the_word_null  varchar(20)                          DEFAULT 'NULL',
    really_null    varchar(20)                          DEFAULT NULL,
    tags           text[]                      NOT NULL DEFAULT '{}',
    line_total     numeric(20, 4) GENERATED ALWAYS AS (quantity * unit_price) STORED
);

COMMENT ON TABLE public.defaults_and_generated IS 'Column defaults and a stored generated column';

-- ---------------------------------------------------------------------------------------------------------
-- Defaults that are expressions rather than literals: CURRENT_USER (no brackets), function calls, arithmetic,
-- and a non-empty array literal, which has no one-line C# equivalent. Expected: the expression columns get
-- HasDefaultValueSql and no constructor default; the literals keep theirs. defaults_and_generated.tags above
-- ('{}' on text[]) is the array control: it must become an empty array, not the string "{}".
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE public.expression_default
(
    id             serial PRIMARY KEY,
    changed_by     varchar(128)                NOT NULL DEFAULT CURRENT_USER,
    app_name       text                                 DEFAULT current_setting('application_name'),
    created_text   varchar(30)                          DEFAULT to_char(now(), 'YYYY-MM-DD'),
    due_at         timestamp with time zone    NOT NULL DEFAULT (now() + interval '30 days'),
    numbers        integer[]                   NOT NULL DEFAULT '{1,2}',
    literal_text   varchar(20)                 NOT NULL DEFAULT 'fallback',
    number_text    varchar(10)                 NOT NULL DEFAULT 0
);

COMMENT ON TABLE public.expression_default IS 'Expression defaults, and literals that must not be mistaken for them';

-- ---------------------------------------------------------------------------------------------------------
-- String defaults that need escaping in C# (#281, #893). A backslash makes the default a verbatim literal,
-- where a double quote is escaped by doubling it rather than with \", and the generator left those quotes
-- single, so a default with both did not compile. Expected: quote_only = "say \"hi\"", backslash_only =
-- @"C:\Temp", backslash_and_quote = @"C:\Temp\say ""hi""" and quote_then_backslash = @"""quoted""\".
-- standard_conforming_strings is on, so the backslashes below are literal.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE public.string_default_escaping
(
    id                   serial PRIMARY KEY,
    quote_only           varchar(30) NOT NULL DEFAULT 'say "hi"',
    backslash_only       varchar(30) NOT NULL DEFAULT 'C:\Temp',
    backslash_and_quote  varchar(30) NOT NULL DEFAULT 'C:\Temp\say "hi"',
    quote_then_backslash varchar(30) NOT NULL DEFAULT '"quoted"\'
);

COMMENT ON TABLE public.string_default_escaping IS 'String defaults containing double quotes and backslashes';


-- ---------------------------------------------------------------------------------------------------------
-- Keys. Composite primary key, composite foreign key (the pairing of which is what the pg_constraint
-- unnest(conkey, confkey) WITH ORDINALITY rewrite exists to get right), a self reference, two foreign keys
-- to the same parent so the reverse navigation properties have to be disambiguated, a foreign key to a
-- unique constraint rather than the primary key, a one to one where the child's key is also its foreign
-- key, and a NOT VALID foreign key, which is PostgreSQL's equivalent of SQL Server's NOCHECK.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE public.parent_composite
(
    key_one     integer NOT NULL,
    key_two     integer NOT NULL,
    alt_key     integer NOT NULL,
    name        varchar(50) NOT NULL,
    CONSTRAINT pk_parent_composite PRIMARY KEY (key_one, key_two),
    CONSTRAINT uq_parent_composite_alt_key UNIQUE (alt_key)
);

CREATE TABLE public.child_composite
(
    child_id        serial PRIMARY KEY,
    parent_key_one  integer NOT NULL,
    parent_key_two  integer NOT NULL,
    description     varchar(50),
    CONSTRAINT fk_child_composite_parent FOREIGN KEY (parent_key_one, parent_key_two)
        REFERENCES public.parent_composite (key_one, key_two)
);

CREATE INDEX ix_child_composite_parent ON public.child_composite (parent_key_one, parent_key_two);

-- Foreign key pointing at a unique constraint rather than the primary key (issue 364, HasPrincipalKey).
CREATE TABLE public.principal_key_child
(
    id              serial PRIMARY KEY,
    parent_alt_key  integer NOT NULL,
    CONSTRAINT fk_principal_key_child FOREIGN KEY (parent_alt_key)
        REFERENCES public.parent_composite (alt_key)
);

CREATE TABLE public.employee
(
    employee_id   serial PRIMARY KEY,
    manager_id    integer,
    full_name     varchar(100) NOT NULL,
    CONSTRAINT fk_employee_manager FOREIGN KEY (manager_id) REFERENCES public.employee (employee_id)
);

-- Two foreign keys onto the same parent: issue 309, where both reverse navigation properties collided.
CREATE TABLE public.flight
(
    flight_id             serial PRIMARY KEY,
    departure_airport_id  integer NOT NULL,
    arrival_airport_id    integer NOT NULL
);

CREATE TABLE public.airport
(
    airport_id  integer PRIMARY KEY,
    name        varchar(50) NOT NULL
);

ALTER TABLE public.flight
    ADD CONSTRAINT fk_flight_departure FOREIGN KEY (departure_airport_id) REFERENCES public.airport (airport_id),
    ADD CONSTRAINT fk_flight_arrival   FOREIGN KEY (arrival_airport_id)   REFERENCES public.airport (airport_id);

-- One to one: the child's primary key is also its foreign key (issue 321, 312).
CREATE TABLE public.person
(
    person_id   serial PRIMARY KEY,
    full_name   varchar(100) NOT NULL
);

CREATE TABLE public.person_photo
(
    person_id  integer PRIMARY KEY,
    photo      bytea NOT NULL,
    CONSTRAINT fk_person_photo FOREIGN KEY (person_id) REFERENCES public.person (person_id)
);

-- NOT VALID is PostgreSQL's un-enforced foreign key (issue 363).
CREATE TABLE public.unenforced_parent
(
    id integer PRIMARY KEY
);

CREATE TABLE public.unenforced_child
(
    id        serial PRIMARY KEY,
    parent_id integer
);

ALTER TABLE public.unenforced_child
    ADD CONSTRAINT fk_unenforced_child FOREIGN KEY (parent_id)
        REFERENCES public.unenforced_parent (id) NOT VALID;

-- Many to many through a pure mapping table, which the generator is expected to collapse.
CREATE TABLE public.student
(
    student_id  serial PRIMARY KEY,
    full_name   varchar(100) NOT NULL
);

CREATE TABLE public.course
(
    course_id  serial PRIMARY KEY,
    title      varchar(100) NOT NULL
);

CREATE TABLE public.student_course
(
    student_id  integer NOT NULL,
    course_id   integer NOT NULL,
    CONSTRAINT pk_student_course PRIMARY KEY (student_id, course_id),
    CONSTRAINT fk_student_course_student FOREIGN KEY (student_id) REFERENCES public.student (student_id),
    CONSTRAINT fk_student_course_course  FOREIGN KEY (course_id)  REFERENCES public.course (course_id)
);

-- A primary key whose columns are declared in a different order from the column order, so the reader has to
-- honour the constraint's own ordinal rather than the table's.
CREATE TABLE public.pk_ordinal_test
(
    filler     varchar(10),
    second_key integer NOT NULL,
    first_key  integer NOT NULL,
    CONSTRAINT pk_pk_ordinal_test PRIMARY KEY (first_key, second_key)
);

-- No primary key at all.
CREATE TABLE public.no_primary_key
(
    a integer NOT NULL,
    b varchar(50) NOT NULL
);


-- ---------------------------------------------------------------------------------------------------------
-- Cross schema foreign key (issue 855).
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE public.categories
(
    category_id   smallint NOT NULL,
    category_name character varying(15) NOT NULL,
    CONSTRAINT pk_categories PRIMARY KEY (category_id)
);

CREATE TABLE another.category_description
(
    category_id smallint NOT NULL,
    description character varying(80) NOT NULL,
    CONSTRAINT pk_category_description PRIMARY KEY (category_id),
    CONSTRAINT fk_category_description FOREIGN KEY (category_id) REFERENCES public.categories (category_id)
);

-- The same table name in two schemas, which must not collide in the generated model.
CREATE TABLE public.duplicated_name
(
    id     integer PRIMARY KEY,
    marker varchar(10) NOT NULL DEFAULT 'public'
);

CREATE TABLE another.duplicated_name
(
    id     integer PRIMARY KEY,
    marker varchar(10) NOT NULL DEFAULT 'another'
);

-- Duplicate foreign key names in different schemas (the Harish3485 case).
CREATE TABLE another.harish_parent
(
    id integer PRIMARY KEY
);

CREATE TABLE another.harish_child
(
    id        integer PRIMARY KEY,
    parent_id integer NOT NULL,
    CONSTRAINT fk_harish FOREIGN KEY (parent_id) REFERENCES another.harish_parent (id)
);

CREATE TABLE public.harish_parent
(
    id integer PRIMARY KEY
);

CREATE TABLE public.harish_child
(
    id        integer PRIMARY KEY,
    parent_id integer NOT NULL,
    CONSTRAINT fk_harish FOREIGN KEY (parent_id) REFERENCES public.harish_parent (id)
);


-- ---------------------------------------------------------------------------------------------------------
-- Names chosen to break naive generators. PostgreSQL folds unquoted identifiers to lower case, so anything
-- that needs to keep its shape has to be quoted - and quoting is what lets spaces, dots, reserved words,
-- leading digits and non-ASCII through.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE "Mixed Case Schema"."Spaced Table Name"
(
    "Spaced Table Name"     serial PRIMARY KEY,
    "Column With Spaces"    varchar(50),
    "Column-With-Hyphens"   varchar(50),
    "1_leading_digit"       integer,
    "select"                varchar(50),
    "class"                 varchar(50),
    "event"                 varchar(50),
    "namespace"             varchar(50),
    "string"                varchar(50),
    "table.with.periods"    varchar(50),
    "Бренды"                varchar(50),
    "œufs"                  varchar(50)
);

COMMENT ON TABLE "Mixed Case Schema"."Spaced Table Name" IS 'Every identifier here needs quoting';

-- Names differing only by case, which C# cannot express as two properties.
CREATE TABLE public.case_only_difference
(
    id      integer PRIMARY KEY,
    "Value" varchar(10),
    "value" varchar(10)
);

-- ALLCAPS names ending in IES, which broke pluralisation (the Bitfiddler case).
CREATE TABLE public."CURRENCIES"
(
    id   integer PRIMARY KEY,
    code char(3) NOT NULL
);

CREATE TABLE public."CATEGORIES"
(
    id   integer PRIMARY KEY,
    name varchar(50) NOT NULL
);


-- ---------------------------------------------------------------------------------------------------------
-- Enum lookup tables, both with and without a group column, since PostgreSQL's EnumSQL omits the GroupField
-- column entirely when no group is configured - which is how the "zero enum rows" bug got in.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE another.status
(
    id   integer PRIMARY KEY,
    name varchar(10) NOT NULL
);

INSERT INTO another.status (id, name) VALUES (1, 'Todo'), (2, 'InProgress'), (3, 'Done');

CREATE TABLE public.order_status
(
    order_status_id integer PRIMARY KEY,
    name            varchar(30) NOT NULL,
    status_group    varchar(20) NOT NULL,
    description     varchar(100)
);

INSERT INTO public.order_status (order_status_id, name, status_group, description) VALUES
    (1, 'Pending',   'Open',   'Not yet processed'),
    (2, 'Picking',   'Open',   'Being picked'),
    (3, 'Shipped',   'Closed', 'On its way'),
    (4, 'Cancelled', 'Closed', 'Cancelled by the customer');


-- ---------------------------------------------------------------------------------------------------------
-- Indexes. A unique constraint and a unique index are different objects in pg_constraint/pg_index and the
-- reader has to tell them apart. Partial and expression indexes are PostgreSQL-only.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE public.index_test
(
    id          serial PRIMARY KEY,
    unique_col  integer NOT NULL,
    lower_me    varchar(50) NOT NULL,
    is_deleted  boolean NOT NULL DEFAULT false,
    a           integer NOT NULL,
    b           integer NOT NULL,
    CONSTRAINT uq_index_test_unique_col UNIQUE (unique_col)
);

CREATE UNIQUE INDEX ux_index_test_a_b   ON public.index_test (a, b);
CREATE        INDEX ix_index_test_lower ON public.index_test (lower(lower_me));
CREATE        INDEX ix_index_test_live  ON public.index_test (a) WHERE is_deleted = false;


-- ---------------------------------------------------------------------------------------------------------
-- Partitioned table. relkind is 'p' rather than 'r', which the index and table reads have to allow for.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE public.measurement
(
    city_id    integer   NOT NULL,
    logdate    date      NOT NULL,
    peaktemp   integer,
    CONSTRAINT pk_measurement PRIMARY KEY (city_id, logdate)
) PARTITION BY RANGE (logdate);

CREATE TABLE public.measurement_2025 PARTITION OF public.measurement
    FOR VALUES FROM ('2025-01-01') TO ('2026-01-01');


-- ---------------------------------------------------------------------------------------------------------
-- Table inheritance, which PostgreSQL has and SQL Server does not. The child reports its own columns plus
-- the parent's.
-- ---------------------------------------------------------------------------------------------------------
CREATE TABLE public.vehicle
(
    vehicle_id  serial PRIMARY KEY,
    registration varchar(20) NOT NULL
);

CREATE TABLE public.truck
(
    payload_kg integer NOT NULL
) INHERITS (public.vehicle);


-- ---------------------------------------------------------------------------------------------------------
-- Views, including a materialised view, which has no SQL Server equivalent and which the reader sees as a
-- separate relkind.
-- ---------------------------------------------------------------------------------------------------------
CREATE VIEW public.active_children AS
    SELECT c.child_id, c.description, p.name AS parent_name
    FROM public.child_composite c
    INNER JOIN public.parent_composite p
        ON p.key_one = c.parent_key_one AND p.key_two = c.parent_key_two;

COMMENT ON VIEW public.active_children IS 'A view has no keys and no relationships';

CREATE VIEW public.all_columns_null_view AS
    SELECT a, b, c FROM public.all_columns_null;

CREATE MATERIALIZED VIEW public.order_status_summary AS
    SELECT status_group, count(*) AS status_count
    FROM public.order_status
    GROUP BY status_group;


-- ---------------------------------------------------------------------------------------------------------
-- A trigger, reported separately from the table it hangs off.
-- ---------------------------------------------------------------------------------------------------------
CREATE FUNCTION public.child_composite_stamp() RETURNS trigger AS $$
BEGIN
    IF NEW.description IS NULL THEN
        NEW.description := 'stamped';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tr_child_composite_before_insert
    BEFORE INSERT ON public.child_composite
    FOR EACH ROW EXECUTE FUNCTION public.child_composite_stamp();


-- ---------------------------------------------------------------------------------------------------------
-- Routines. PostgreSQL has no stored procedures in the SQL Server sense until CALL arrived in 11, and its
-- functions can return a set in four different ways. Each one reaches the reader differently, and
-- information_schema alone cannot tell them apart - data_type comes back as 'record' or 'USER-DEFINED' -
-- which is why the reader joins pg_proc and reads proretset.
-- ---------------------------------------------------------------------------------------------------------

-- 1. Plain scalar function.
CREATE FUNCTION public.line_total(quantity integer, unit_price numeric)
    RETURNS numeric
    LANGUAGE sql IMMUTABLE
    AS $$ SELECT quantity * unit_price $$;

-- 2. Scalar function with a default argument, and a reserved word as a parameter name.
CREATE FUNCTION public.is_high_value("value" numeric, threshold numeric DEFAULT 1000)
    RETURNS boolean
    LANGUAGE sql IMMUTABLE
    AS $$ SELECT "value" > threshold $$;

-- 3. RETURNS TABLE - the columns arrive as TABLE mode arguments.
CREATE FUNCTION public.children_of(parent_key_one integer, parent_key_two integer)
    RETURNS TABLE (child_id integer, description varchar(50))
    LANGUAGE sql STABLE
    AS $$
        SELECT c.child_id, c.description
        FROM public.child_composite c
        WHERE c.parent_key_one = children_of.parent_key_one
          AND c.parent_key_two = children_of.parent_key_two
    $$;

-- 4. RETURNS SETOF a table type - the columns come from the composite type's attributes.
CREATE FUNCTION public.all_parents()
    RETURNS SETOF public.parent_composite
    LANGUAGE sql STABLE
    AS $$ SELECT * FROM public.parent_composite $$;

-- 5. RETURNS SETOF a scalar - a single unnamed column.
CREATE FUNCTION public.parent_names()
    RETURNS SETOF varchar
    LANGUAGE sql STABLE
    AS $$ SELECT name FROM public.parent_composite $$;

-- 6. OUT and INOUT parameters, which is how PostgreSQL spells a multi-value return.
CREATE FUNCTION public.count_children(IN parent_key_one integer, OUT child_count integer, INOUT running_total integer)
    LANGUAGE plpgsql
    AS $$
    BEGIN
        SELECT count(*) INTO child_count
        FROM public.child_composite c
        WHERE c.parent_key_one = count_children.parent_key_one;
        running_total := running_total + child_count;
    END;
    $$;

-- 7. A function with no parameters at all.
CREATE FUNCTION public.no_parameters()
    RETURNS integer
    LANGUAGE sql STABLE
    AS $$ SELECT 42 $$;

-- 8. A true PROCEDURE, which returns nothing and is invoked with CALL.
CREATE PROCEDURE public.touch_parent(IN key_one integer, IN key_two integer)
    LANGUAGE plpgsql
    AS $$
    BEGIN
        UPDATE public.parent_composite
        SET name = name
        WHERE parent_composite.key_one = touch_parent.key_one
          AND parent_composite.key_two = touch_parent.key_two;
    END;
    $$;

-- 9. A function returning a composite type, whose result columns are that type's attributes.
CREATE FUNCTION public.split_name(whole_name text)
    RETURNS public.full_name
    LANGUAGE sql IMMUTABLE
    AS $$ SELECT ROW(split_part(whole_name, ' ', 1), split_part(whole_name, ' ', 2))::public.full_name $$;

-- 10. A function in a non-default schema.
CREATE FUNCTION another.schema_scoped(n integer)
    RETURNS integer
    LANGUAGE sql IMMUTABLE
    AS $$ SELECT n * 2 $$;


-- ---------------------------------------------------------------------------------------------------------
-- Seed rows, so anything that executes rather than merely reads has something to work with.
-- ---------------------------------------------------------------------------------------------------------
INSERT INTO public.parent_composite (key_one, key_two, alt_key, name) VALUES
    (1, 1, 10, 'First parent'),
    (1, 2, 20, 'Second parent');

INSERT INTO public.child_composite (parent_key_one, parent_key_two, description) VALUES
    (1, 1, 'First child'),
    (1, 1, NULL),
    (1, 2, 'Third child');

INSERT INTO public.categories (category_id, category_name) VALUES (1, 'Beverages');
INSERT INTO another.category_description (category_id, description) VALUES (1, 'Soft drinks and coffees');

REFRESH MATERIALIZED VIEW public.order_status_summary;
