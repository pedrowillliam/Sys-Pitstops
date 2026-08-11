-- =====================================================================
-- Sys Pitstops - MVP schema (PostgreSQL 15+)
-- Reference DDL. The source of truth is EF Core migrations; this file
-- documents the intended shape of the database.
-- =====================================================================

CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ---------------------------------------------------------------------
-- Enums
-- ---------------------------------------------------------------------
CREATE TYPE user_role AS ENUM ('ADMIN', 'ATTENDANT', 'MECHANIC');

CREATE TYPE service_order_status AS ENUM (
    'REQUESTED',
    'CONFIRMED',
    'IN_YARD',
    'AWAITING_APPROVAL',
    'IN_PROGRESS',
    'READY',
    'DELIVERED',
    'CANCELED'
);

CREATE TYPE item_type      AS ENUM ('SERVICE', 'PART');
CREATE TYPE quote_status   AS ENUM ('SENT', 'APPROVED', 'REJECTED', 'EXPIRED');
CREATE TYPE movement_type  AS ENUM ('IN', 'OUT', 'ADJUSTMENT');

-- ---------------------------------------------------------------------
-- Workshops (multi-tenant hedge: MVP always uses id = 1)
-- ---------------------------------------------------------------------
CREATE TABLE workshops (
    id          int PRIMARY KEY,
    name        text        NOT NULL,
    document    text,
    phone       text,
    address     text,
    created_at  timestamptz NOT NULL DEFAULT now()
);

INSERT INTO workshops (id, name) VALUES (1, 'Oficina Piloto');

-- ---------------------------------------------------------------------
-- Users
-- ---------------------------------------------------------------------
CREATE TABLE users (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    workshop_id    int         NOT NULL REFERENCES workshops (id),
    name           text        NOT NULL,
    email          text        NOT NULL,
    password_hash  text        NOT NULL,
    role           user_role   NOT NULL,
    is_active      boolean     NOT NULL DEFAULT true,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_users_email UNIQUE (workshop_id, email)
);

-- ---------------------------------------------------------------------
-- Customers
-- ---------------------------------------------------------------------
CREATE TABLE customers (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    workshop_id  int         NOT NULL REFERENCES workshops (id),
    name         text        NOT NULL,
    phone        text        NOT NULL,
    document     text,
    email        text,
    notes        text,
    is_active    boolean     NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_customers_phone ON customers (workshop_id, phone);
CREATE INDEX ix_customers_name  ON customers (workshop_id, lower(name));

-- ---------------------------------------------------------------------
-- Vehicles
-- owner_id is the CURRENT owner. Historical ownership is preserved by
-- the customer_id snapshot stored on each service order.
-- ---------------------------------------------------------------------
CREATE TABLE vehicles (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    workshop_id  int         NOT NULL REFERENCES workshops (id),
    owner_id     uuid        NOT NULL REFERENCES customers (id),
    plate        text        NOT NULL,
    brand        text        NOT NULL,
    model        text        NOT NULL,
    model_year   int,
    color        text,
    vin          text,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_vehicles_plate UNIQUE (workshop_id, plate)
);

CREATE INDEX ix_vehicles_owner ON vehicles (owner_id);

-- ---------------------------------------------------------------------
-- Parts (inventory)
-- quantity_on_hand is maintained by the application, and every change
-- must be accompanied by a stock_movements row.
-- ---------------------------------------------------------------------
CREATE TABLE parts (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    workshop_id       int            NOT NULL REFERENCES workshops (id),
    sku               text           NOT NULL,
    name              text           NOT NULL,
    description       text,
    unit              text           NOT NULL DEFAULT 'UN',
    sale_price        numeric(12, 2) NOT NULL CHECK (sale_price >= 0),
    cost_price        numeric(12, 2) NOT NULL DEFAULT 0 CHECK (cost_price >= 0),
    quantity_on_hand  numeric(10, 3) NOT NULL DEFAULT 0,
    min_quantity      numeric(10, 3) NOT NULL DEFAULT 0,
    location          text,
    is_active         boolean        NOT NULL DEFAULT true,
    created_at        timestamptz    NOT NULL DEFAULT now(),
    updated_at        timestamptz    NOT NULL DEFAULT now(),
    CONSTRAINT uq_parts_sku UNIQUE (workshop_id, sku)
);

CREATE INDEX ix_parts_name ON parts (workshop_id, lower(name));
CREATE INDEX ix_parts_low_stock ON parts (workshop_id)
    WHERE quantity_on_hand <= min_quantity;

-- ---------------------------------------------------------------------
-- Service orders
-- customer_id is a SNAPSHOT of who owned the vehicle at opening time.
-- Never resolve the customer through vehicles.owner_id in reports.
-- ---------------------------------------------------------------------
CREATE SEQUENCE service_order_number_seq START 1;

CREATE TABLE service_orders (
    id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    workshop_id          int                  NOT NULL REFERENCES workshops (id),
    number               int                  NOT NULL DEFAULT nextval('service_order_number_seq'),
    vehicle_id           uuid                 NOT NULL REFERENCES vehicles (id),
    customer_id          uuid                 NOT NULL REFERENCES customers (id),
    mechanic_id          uuid                 REFERENCES users (id),
    created_by           uuid                 NOT NULL REFERENCES users (id),
    status               service_order_status NOT NULL DEFAULT 'REQUESTED',
    mileage              int,
    reported_issue       text,
    diagnosis            text,
    discount_amount      numeric(12, 2)       NOT NULL DEFAULT 0 CHECK (discount_amount >= 0),
    approval_waived_note text,
    scheduled_at         timestamptz,
    opened_at            timestamptz          NOT NULL DEFAULT now(),
    closed_at            timestamptz,
    created_at           timestamptz          NOT NULL DEFAULT now(),
    updated_at           timestamptz          NOT NULL DEFAULT now(),
    CONSTRAINT uq_service_orders_number UNIQUE (workshop_id, number)
);

CREATE INDEX ix_so_status     ON service_orders (workshop_id, status);
CREATE INDEX ix_so_vehicle    ON service_orders (vehicle_id, opened_at DESC);
CREATE INDEX ix_so_customer   ON service_orders (customer_id, opened_at DESC);
CREATE INDEX ix_so_mechanic   ON service_orders (mechanic_id, status);
CREATE INDEX ix_so_opened_at  ON service_orders (workshop_id, opened_at);

-- ---------------------------------------------------------------------
-- Service order items
-- unit_price and description are FROZEN at insert time. Changing a part
-- price later must never alter historical revenue.
-- ---------------------------------------------------------------------
CREATE TABLE service_order_items (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    service_order_id  uuid           NOT NULL REFERENCES service_orders (id) ON DELETE CASCADE,
    item_type         item_type      NOT NULL,
    part_id           uuid           REFERENCES parts (id),
    description       text           NOT NULL,
    quantity          numeric(10, 3) NOT NULL CHECK (quantity > 0),
    unit_price        numeric(12, 2) NOT NULL CHECK (unit_price >= 0),
    created_by        uuid           NOT NULL REFERENCES users (id),
    created_at        timestamptz    NOT NULL DEFAULT now(),
    CONSTRAINT ck_item_part_required
        CHECK ((item_type = 'PART' AND part_id IS NOT NULL)
            OR (item_type = 'SERVICE' AND part_id IS NULL))
);

CREATE INDEX ix_soi_order ON service_order_items (service_order_id);

-- ---------------------------------------------------------------------
-- Status history (feeds the average execution time KPI)
-- ---------------------------------------------------------------------
CREATE TABLE service_order_status_history (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    service_order_id  uuid                 NOT NULL REFERENCES service_orders (id) ON DELETE CASCADE,
    from_status       service_order_status,
    to_status         service_order_status NOT NULL,
    changed_by        uuid                 NOT NULL REFERENCES users (id),
    note              text,
    changed_at        timestamptz          NOT NULL DEFAULT now()
);

CREATE INDEX ix_sosh_order ON service_order_status_history (service_order_id, changed_at);

-- ---------------------------------------------------------------------
-- Media attached to the technical report
-- ---------------------------------------------------------------------
CREATE TABLE service_order_media (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    service_order_id  uuid        NOT NULL REFERENCES service_orders (id) ON DELETE CASCADE,
    storage_key       text        NOT NULL,
    content_type      text        NOT NULL,
    size_bytes        bigint,
    caption           text,
    uploaded_by       uuid        NOT NULL REFERENCES users (id),
    uploaded_at       timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_som_order ON service_order_media (service_order_id);

-- ---------------------------------------------------------------------
-- Quotes
-- items_snapshot freezes what the customer actually saw and approved.
-- ---------------------------------------------------------------------
CREATE TABLE quotes (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    service_order_id  uuid           NOT NULL REFERENCES service_orders (id) ON DELETE CASCADE,
    public_token      text           NOT NULL,
    status            quote_status   NOT NULL DEFAULT 'SENT',
    items_snapshot    jsonb          NOT NULL,
    total_amount      numeric(12, 2) NOT NULL CHECK (total_amount >= 0),
    rejection_reason  text,
    sent_by           uuid           NOT NULL REFERENCES users (id),
    sent_at           timestamptz    NOT NULL DEFAULT now(),
    expires_at        timestamptz    NOT NULL,
    responded_at      timestamptz,
    CONSTRAINT uq_quotes_token UNIQUE (public_token)
);

CREATE INDEX ix_quotes_order ON quotes (service_order_id, sent_at DESC);

-- ---------------------------------------------------------------------
-- Stock movements (audit trail; quantity is always positive,
-- direction is given by movement_type)
-- ---------------------------------------------------------------------
CREATE TABLE stock_movements (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    workshop_id       int            NOT NULL REFERENCES workshops (id),
    part_id           uuid           NOT NULL REFERENCES parts (id),
    movement_type     movement_type  NOT NULL,
    quantity          numeric(10, 3) NOT NULL CHECK (quantity > 0),
    unit_cost         numeric(12, 2),
    service_order_id  uuid           REFERENCES service_orders (id),
    user_id           uuid           NOT NULL REFERENCES users (id),
    note              text,
    created_at        timestamptz    NOT NULL DEFAULT now()
);

CREATE INDEX ix_sm_part  ON stock_movements (part_id, created_at DESC);
CREATE INDEX ix_sm_order ON stock_movements (service_order_id);

-- =====================================================================
-- Reference queries for the dashboard
-- =====================================================================

-- Revenue and average ticket for a period
-- SELECT count(*) AS orders,
--        sum(t.total) AS revenue,
--        avg(t.total) AS average_ticket
-- FROM (
--     SELECT so.id,
--            coalesce(sum(i.quantity * i.unit_price), 0) - so.discount_amount AS total
--     FROM service_orders so
--     LEFT JOIN service_order_items i ON i.service_order_id = so.id
--     WHERE so.workshop_id = 1
--       AND so.status IN ('READY', 'DELIVERED')
--       AND so.closed_at >= $1 AND so.closed_at < $2
--     GROUP BY so.id
-- ) t;

-- Average execution time (IN_PROGRESS -> READY)
-- SELECT avg(r.changed_at - p.changed_at) AS average_execution
-- FROM service_order_status_history p
-- JOIN service_order_status_history r
--   ON r.service_order_id = p.service_order_id AND r.to_status = 'READY'
-- WHERE p.to_status = 'IN_PROGRESS';
