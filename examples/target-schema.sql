-- Target schema DDL with intentional differences from reference-schema.sql
-- Use this script to create the target schema in a local Oracle XE instance.
-- Connect as the APP_TARGET user before running.
--
-- Differences introduced (for tool validation):
--   Critical — MissingTable      : PAYMENTS table is absent
--   Critical — MissingColumn     : CUSTOMERS.EMAIL column is absent
--   Critical — DataTypeMismatch  : ORDERS.TOTAL_AMOUNT is VARCHAR2(50) instead of NUMBER(12,2)
--   Critical — DataLengthSmaller : ORDERS.STATUS is VARCHAR2(10) instead of VARCHAR2(30)
--   Warning  — ScaleMismatch     : PRODUCTS.PRICE is NUMBER(10,0) instead of NUMBER(10,2)
--   Warning  — NullabilityMismatch   : CUSTOMERS.STATUS allows NULL instead of NOT NULL
--   Warning  — DefaultValueMismatch  : CUSTOMERS.STATUS default is 'INACTIVE' instead of 'ACTIVE'
--   Warning  — ExtraColumn           : CUSTOMERS.INTERNAL_CODE exists only in target
--   Info     — ExtraTable            : AUDIT_LOG exists only in target

CREATE TABLE CUSTOMERS (
    ID             NUMBER(10)      NOT NULL,
    NAME           VARCHAR2(200)   NOT NULL,
    -- EMAIL column intentionally removed (MissingColumn Critical)
    PHONE          VARCHAR2(20),
    STATUS         VARCHAR2(20)    DEFAULT 'INACTIVE',  -- nullable + different default (NullabilityMismatch + DefaultValueMismatch Warning)
    CREATED_AT     DATE            NOT NULL,
    INTERNAL_CODE  NUMBER,                              -- extra column not in reference (ExtraColumn Warning)
    CONSTRAINT PK_CUSTOMERS PRIMARY KEY (ID)
);

CREATE TABLE PRODUCTS (
    ID            NUMBER(10)      NOT NULL,
    NAME          VARCHAR2(200)   NOT NULL,
    DESCRIPTION   VARCHAR2(1000),
    PRICE         NUMBER(10, 0)   NOT NULL,  -- scale changed from 2 to 0 (ScaleMismatch Warning)
    STOCK_QTY     NUMBER(10)      NOT NULL,
    CONSTRAINT PK_PRODUCTS PRIMARY KEY (ID)
);

CREATE TABLE ORDERS (
    ID            NUMBER(10)      NOT NULL,
    CUSTOMER_ID   NUMBER(10)      NOT NULL,
    TOTAL_AMOUNT  VARCHAR2(50)    NOT NULL,  -- type changed from NUMBER(12,2) to VARCHAR2(50) (DataTypeMismatch Critical)
    ORDER_DATE    DATE            NOT NULL,
    STATUS        VARCHAR2(10)    NOT NULL,  -- length reduced from 30 to 10 (DataLengthSmaller Critical)
    CONSTRAINT PK_ORDERS PRIMARY KEY (ID)
);

CREATE TABLE ORDER_ITEMS (
    ID            NUMBER(10)      NOT NULL,
    ORDER_ID      NUMBER(10)      NOT NULL,
    PRODUCT_ID    NUMBER(10)      NOT NULL,
    QUANTITY      NUMBER(10)      NOT NULL,
    UNIT_PRICE    NUMBER(12, 2)   NOT NULL,
    CONSTRAINT PK_ORDER_ITEMS PRIMARY KEY (ID)
);

-- PAYMENTS table intentionally omitted (MissingTable Critical)

CREATE TABLE AUDIT_LOG (
    ID            NUMBER(10)      NOT NULL,
    EVENT_TYPE    VARCHAR2(50)    NOT NULL,
    EVENT_DATE    DATE            NOT NULL,
    CONSTRAINT PK_AUDIT_LOG PRIMARY KEY (ID)
);
-- AUDIT_LOG exists only in target (ExtraTable Info)
