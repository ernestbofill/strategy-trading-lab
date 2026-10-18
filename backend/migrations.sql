CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE EXTENSION IF NOT EXISTS citext;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE TABLE assets (
        id uuid NOT NULL,
        symbol text NOT NULL,
        exchange text NOT NULL,
        name text NOT NULL,
        kind text NOT NULL,
        currency char(3) NOT NULL,
        active boolean NOT NULL DEFAULT TRUE,
        created_at timestamptz NOT NULL DEFAULT (now()),
        CONSTRAINT pk_assets PRIMARY KEY (id),
        CONSTRAINT ck_assets_kind CHECK (kind IN ('stock', 'fund'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE TABLE users (
        id uuid NOT NULL,
        email citext NOT NULL,
        password_hash text,
        email_verified_at timestamptz,
        time_zone text NOT NULL,
        locale text NOT NULL,
        currency char(3) NOT NULL DEFAULT 'EUR',
        overview_email boolean NOT NULL DEFAULT TRUE,
        disclaimer_accepted_at timestamptz,
        created_at timestamptz NOT NULL DEFAULT (now()),
        updated_at timestamptz NOT NULL DEFAULT (now()),
        CONSTRAINT pk_users PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE TABLE asset_texts (
        asset_id uuid NOT NULL,
        locale text NOT NULL,
        what text NOT NULL,
        created_at timestamptz NOT NULL DEFAULT (now()),
        CONSTRAINT pk_asset_texts PRIMARY KEY (asset_id, locale),
        CONSTRAINT fk_asset_texts_assets_asset_id FOREIGN KEY (asset_id) REFERENCES assets (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE TABLE push_subscriptions (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        endpoint text NOT NULL,
        p256dh text NOT NULL,
        auth text NOT NULL,
        last_failed_at timestamptz,
        created_at timestamptz NOT NULL DEFAULT (now()),
        CONSTRAINT pk_push_subscriptions PRIMARY KEY (id),
        CONSTRAINT fk_push_subscriptions_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE TABLE strategies (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        template_key text NOT NULL,
        defaults_version integer NOT NULL,
        name text NOT NULL,
        mode text NOT NULL,
        status text NOT NULL,
        started_on date NOT NULL,
        ended_at timestamptz,
        notifications boolean NOT NULL DEFAULT TRUE,
        currency char(3) NOT NULL,
        params jsonb NOT NULL,
        source_strategy_id uuid,
        created_at timestamptz NOT NULL DEFAULT (now()),
        updated_at timestamptz NOT NULL DEFAULT (now()),
        budget_amount numeric(14,2) NOT NULL,
        budget_max_steps integer,
        budget_step numeric(14,2),
        CONSTRAINT pk_strategies PRIMARY KEY (id),
        CONSTRAINT ck_strategies_budget_step CHECK (budget_step <= budget_amount),
        CONSTRAINT ck_strategies_budget_steps CHECK ((budget_step IS NULL) = (budget_max_steps IS NULL)),
        CONSTRAINT ck_strategies_ended_at CHECK ((status = 'ended') = (ended_at IS NOT NULL)),
        CONSTRAINT ck_strategies_mode CHECK (mode IN ('simulation', 'live')),
        CONSTRAINT ck_strategies_status CHECK (status IN ('running', 'ended')),
        CONSTRAINT ck_strategies_template_key CHECK (template_key IN ('dip', 'comeback', 'drop', 'trend', 'ai')),
        CONSTRAINT fk_strategies_strategies_source_strategy_id FOREIGN KEY (source_strategy_id) REFERENCES strategies (id) ON DELETE SET NULL,
        CONSTRAINT fk_strategies_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE TABLE signals (
        id uuid NOT NULL,
        strategy_id uuid NOT NULL,
        asset_id uuid NOT NULL,
        side text NOT NULL,
        step_no integer,
        max_steps integer,
        step_amount numeric(14,2),
        signal_date date NOT NULL,
        close_price numeric(14,4) NOT NULL,
        facts jsonb NOT NULL,
        status text NOT NULL,
        skip_reason text,
        answered_at timestamptz,
        snoozed_until date,
        ended_on date,
        created_at timestamptz NOT NULL DEFAULT (now()),
        CONSTRAINT pk_signals PRIMARY KEY (id),
        CONSTRAINT ck_signals_answered_at CHECK ((status IN ('confirmed', 'skipped')) = (answered_at IS NOT NULL)),
        CONSTRAINT ck_signals_close_price CHECK (close_price > 0),
        CONSTRAINT ck_signals_missed_ended_on CHECK (status <> 'missed' OR ended_on IS NOT NULL),
        CONSTRAINT ck_signals_sell_no_step CHECK (side = 'buy' OR (step_no IS NULL AND max_steps IS NULL AND step_amount IS NULL)),
        CONSTRAINT ck_signals_side CHECK (side IN ('buy', 'sell')),
        CONSTRAINT ck_signals_skip_reason CHECK (skip_reason IN ('price_ran_away', 'didnt_trust_it', 'no_cash', 'other')),
        CONSTRAINT ck_signals_skip_reason_skipped CHECK (skip_reason IS NULL OR status = 'skipped'),
        CONSTRAINT ck_signals_snoozed_until CHECK (snoozed_until IS NULL OR status = 'waiting'),
        CONSTRAINT ck_signals_status CHECK (status IN ('waiting', 'confirmed', 'skipped', 'missed')),
        CONSTRAINT ck_signals_step_no CHECK (step_no IS NULL OR step_no BETWEEN 1 AND max_steps),
        CONSTRAINT ck_signals_step_together CHECK ((step_no IS NULL) = (max_steps IS NULL) AND (step_no IS NULL) = (step_amount IS NULL)),
        CONSTRAINT fk_signals_assets_asset_id FOREIGN KEY (asset_id) REFERENCES assets (id),
        CONSTRAINT fk_signals_strategies_strategy_id FOREIGN KEY (strategy_id) REFERENCES strategies (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE TABLE strategy_assets (
        strategy_id uuid NOT NULL,
        asset_id uuid NOT NULL,
        active boolean NOT NULL,
        added_at timestamptz NOT NULL,
        removed_at timestamptz,
        created_at timestamptz NOT NULL DEFAULT (now()),
        CONSTRAINT pk_strategy_assets PRIMARY KEY (strategy_id, asset_id),
        CONSTRAINT ck_strategy_assets_removed CHECK (removed_at IS NULL OR NOT active),
        CONSTRAINT fk_strategy_assets_assets_asset_id FOREIGN KEY (asset_id) REFERENCES assets (id),
        CONSTRAINT fk_strategy_assets_strategies_strategy_id FOREIGN KEY (strategy_id) REFERENCES strategies (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE TABLE notifications (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        signal_id uuid,
        kind text NOT NULL,
        channel text NOT NULL,
        for_date date NOT NULL,
        sent_at timestamptz,
        failed_at timestamptz,
        error text,
        created_at timestamptz NOT NULL DEFAULT (now()),
        CONSTRAINT pk_notifications PRIMARY KEY (id),
        CONSTRAINT ck_notifications_channel CHECK (channel IN ('push', 'email')),
        CONSTRAINT ck_notifications_kind CHECK (kind IN ('signal', 'snoozed')),
        CONSTRAINT ck_notifications_signal_id CHECK ((kind IN ('signal', 'snoozed')) = (signal_id IS NOT NULL)),
        CONSTRAINT fk_notifications_signals_signal_id FOREIGN KEY (signal_id) REFERENCES signals (id) ON DELETE CASCADE,
        CONSTRAINT fk_notifications_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE TABLE signal_trades (
        id uuid NOT NULL,
        signal_id uuid NOT NULL,
        strategy_id uuid NOT NULL,
        asset_id uuid NOT NULL,
        trade_date date NOT NULL,
        side text NOT NULL,
        step_no integer,
        price numeric(14,4) NOT NULL,
        shares numeric(18,6) NOT NULL,
        amount numeric(14,2) NOT NULL,
        fee numeric(14,2) NOT NULL DEFAULT 0.0,
        fx_rate numeric(18,8) NOT NULL,
        created_at timestamptz NOT NULL DEFAULT (now()),
        CONSTRAINT pk_signal_trades PRIMARY KEY (id),
        CONSTRAINT ck_signal_trades_amount CHECK (amount > 0),
        CONSTRAINT ck_signal_trades_fee CHECK (fee >= 0),
        CONSTRAINT ck_signal_trades_fx_rate CHECK (fx_rate > 0),
        CONSTRAINT ck_signal_trades_price CHECK (price > 0),
        CONSTRAINT ck_signal_trades_shares CHECK (shares > 0),
        CONSTRAINT ck_signal_trades_side CHECK (side IN ('buy', 'sell')),
        CONSTRAINT ck_signal_trades_step_no CHECK (step_no IS NULL OR step_no >= 1),
        CONSTRAINT fk_signal_trades_assets_asset_id FOREIGN KEY (asset_id) REFERENCES assets (id),
        CONSTRAINT fk_signal_trades_signals_signal_id FOREIGN KEY (signal_id) REFERENCES signals (id) ON DELETE CASCADE,
        CONSTRAINT fk_signal_trades_strategies_strategy_id FOREIGN KEY (strategy_id) REFERENCES strategies (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE TABLE user_trades (
        id uuid NOT NULL,
        strategy_id uuid NOT NULL,
        asset_id uuid NOT NULL,
        trade_date date NOT NULL,
        side text NOT NULL,
        price numeric(14,4) NOT NULL,
        entered_as text NOT NULL,
        amount numeric(14,2) NOT NULL,
        shares numeric(18,6) NOT NULL,
        fee numeric(14,2) NOT NULL DEFAULT 0.0,
        fx_rate numeric(18,8) NOT NULL,
        fx_source text NOT NULL,
        source text NOT NULL,
        signal_id uuid,
        deleted_at timestamptz,
        created_at timestamptz NOT NULL DEFAULT (now()),
        updated_at timestamptz NOT NULL DEFAULT (now()),
        CONSTRAINT pk_user_trades PRIMARY KEY (id),
        CONSTRAINT ck_user_trades_amount CHECK (amount > 0),
        CONSTRAINT ck_user_trades_entered_as CHECK (entered_as IN ('amount', 'shares')),
        CONSTRAINT ck_user_trades_fee CHECK (fee >= 0),
        CONSTRAINT ck_user_trades_fx_rate CHECK (fx_rate > 0),
        CONSTRAINT ck_user_trades_fx_source CHECK (fx_source IN ('reference', 'user')),
        CONSTRAINT ck_user_trades_price CHECK (price > 0),
        CONSTRAINT ck_user_trades_shares CHECK (shares > 0),
        CONSTRAINT ck_user_trades_side CHECK (side IN ('buy', 'sell')),
        CONSTRAINT ck_user_trades_source CHECK (source IN ('signal', 'manual')),
        CONSTRAINT ck_user_trades_source_signal CHECK ((source = 'signal') = (signal_id IS NOT NULL)),
        CONSTRAINT fk_user_trades_assets_asset_id FOREIGN KEY (asset_id) REFERENCES assets (id),
        CONSTRAINT fk_user_trades_signals_signal_id FOREIGN KEY (signal_id) REFERENCES signals (id),
        CONSTRAINT fk_user_trades_strategies_strategy_id FOREIGN KEY (strategy_id) REFERENCES strategies (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    INSERT INTO users (id, created_at, currency, disclaimer_accepted_at, email, email_verified_at, locale, overview_email, password_hash, time_zone, updated_at)
    VALUES ('019a0000-0000-7000-8000-000000000001', TIMESTAMPTZ '2026-10-18T00:00:00+00:00', 'EUR', TIMESTAMPTZ '2026-10-18T00:00:00+00:00', 'dev@strategytradinglab.local', NULL, 'en', TRUE, NULL, 'Europe/Madrid', TIMESTAMPTZ '2026-10-18T00:00:00+00:00');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_assets_symbol_exchange ON assets (symbol, exchange);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_notifications_signal_id ON notifications (signal_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_notifications_user_id_kind_channel_for_date_signal_id ON notifications (user_id, kind, channel, for_date, signal_id) NULLS NOT DISTINCT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_push_subscriptions_endpoint ON push_subscriptions (endpoint);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_push_subscriptions_user_id ON push_subscriptions (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_signal_trades_asset_id ON signal_trades (asset_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_signal_trades_signal_id ON signal_trades (signal_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_signal_trades_strategy_id_asset_id_trade_date ON signal_trades (strategy_id, asset_id, trade_date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_signals_asset_id ON signals (asset_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_signals_strategy_id_asset_id_signal_date_side ON signals (strategy_id, asset_id, signal_date, side);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_signals_strategy_id_status ON signals (strategy_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_signals_strategy_id_waiting ON signals (strategy_id) WHERE status = 'waiting';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_strategies_source_strategy_id ON strategies (source_strategy_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_strategies_user_id ON strategies (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_strategy_assets_asset_id ON strategy_assets (asset_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_user_trades_asset_id ON user_trades (asset_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_user_trades_signal_id ON user_trades (signal_id) WHERE deleted_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE INDEX ix_user_trades_strategy_id_asset_id_trade_date ON user_trades (strategy_id, asset_id, trade_date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_users_email ON users (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    CREATE UNIQUE INDEX ux_strategies_user_id_running_name ON strategies (user_id, lower(name)) WHERE status = 'running';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261018100652_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261018100652_InitialCreate', '10.0.4');
    END IF;
END $EF$;
COMMIT;

