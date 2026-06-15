CREATE SCHEMA IF NOT EXISTS geomain;

CREATE TABLE IF NOT EXISTS geomain.datasets (
    dataset_id uuid PRIMARY KEY,
    name text NOT NULL UNIQUE,
    description text,
    source_uri text,
    source_type text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS geomain.ingest_jobs (
    job_id uuid PRIMARY KEY,
    dataset_id uuid REFERENCES geomain.datasets(dataset_id),
    dataset_name text,
    source_uri text NOT NULL,
    source_type text,
    target_schema text NOT NULL DEFAULT 'public',
    target_table text NOT NULL,
    if_exists text NOT NULL DEFAULT 'fail',
    status text NOT NULL DEFAULT 'queued',
    feature_count bigint NOT NULL DEFAULT 0,
    geometry_type text,
    source_crs text,
    target_crs text NOT NULL DEFAULT 'EPSG:4326',
    bounds jsonb,
    notes text,
    error_message text,
    created_at timestamptz NOT NULL DEFAULT now(),
    started_at timestamptz,
    finished_at timestamptz,
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ingest_jobs_if_exists_check CHECK (if_exists IN ('fail', 'replace', 'append')),
    CONSTRAINT ingest_jobs_status_check CHECK (status IN ('queued', 'running', 'loaded', 'failed', 'cancelled'))
);

CREATE TABLE IF NOT EXISTS geomain.ingest_events (
    event_id bigserial PRIMARY KEY,
    job_id uuid REFERENCES geomain.ingest_jobs(job_id) ON DELETE CASCADE,
    event_type text NOT NULL,
    message text,
    payload jsonb,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS geomain.layer_loads (
    load_id bigserial PRIMARY KEY,
    job_id uuid REFERENCES geomain.ingest_jobs(job_id) ON DELETE SET NULL,
    dataset_id uuid REFERENCES geomain.datasets(dataset_id) ON DELETE SET NULL,
    target_schema text NOT NULL,
    target_table text NOT NULL,
    feature_count bigint NOT NULL DEFAULT 0,
    geometry_type text,
    crs text NOT NULL DEFAULT 'EPSG:4326',
    loaded_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ingest_jobs_status_idx ON geomain.ingest_jobs(status);
CREATE INDEX IF NOT EXISTS ingest_jobs_dataset_name_idx ON geomain.ingest_jobs(dataset_name);
CREATE INDEX IF NOT EXISTS ingest_events_job_id_idx ON geomain.ingest_events(job_id);
CREATE INDEX IF NOT EXISTS layer_loads_job_id_idx ON geomain.layer_loads(job_id);
