-- Partalog read-heavy index audit
-- Run against the target database before production cutover:
--   psql "$DATABASE_URL" -f backend/db/read_heavy_index_audit.sql
--
-- This script is intentionally read-only. It highlights missing extensions,
-- high sequential scan tables, invalid indexes, and the expected indexes for
-- chat/catalog read paths.

\echo '== Extensions =='
SELECT extname, extversion
FROM pg_extension
WHERE extname IN ('vector', 'pg_trgm', 'unaccent', 'pg_stat_statements')
ORDER BY extname;

\echo '== Large/high sequential scan tables =='
SELECT
    schemaname,
    relname,
    n_live_tup,
    seq_scan,
    idx_scan,
    CASE
        WHEN seq_scan + idx_scan = 0 THEN 0
        ELSE round((seq_scan::numeric / (seq_scan + idx_scan)) * 100, 2)
    END AS seq_scan_pct
FROM pg_stat_user_tables
WHERE n_live_tup > 10000
ORDER BY seq_scan_pct DESC, n_live_tup DESC
LIMIT 25;

\echo '== Unused or rarely used indexes on large tables =='
SELECT
    schemaname,
    relname,
    indexrelname,
    idx_scan,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
FROM pg_stat_user_indexes
WHERE idx_scan < 5
  AND pg_relation_size(indexrelid) > 10 * 1024 * 1024
ORDER BY pg_relation_size(indexrelid) DESC
LIMIT 25;

\echo '== Invalid indexes =='
SELECT
    n.nspname AS schema_name,
    c.relname AS index_name,
    t.relname AS table_name
FROM pg_index i
JOIN pg_class c ON c.oid = i.indexrelid
JOIN pg_class t ON t.oid = i.indrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE NOT i.indisvalid OR NOT i.indisready
ORDER BY schema_name, table_name, index_name;

\echo '== Expected chat/catalog indexes =='
WITH expected(index_name) AS (
    VALUES
        ('idx_catalog_items_embedding_hnsw'),
        ('IX_CatalogItems_CatalogId'),
        ('IX_CatalogItems_PageId'),
        ('IX_CatalogItems_Code'),
        ('IX_PolicyThresholds_ActiveScope'),
        ('UX_PolicyThresholds_ActiveScope'),
        ('IX_AiCapacityLeases_ExpiresAt'),
        ('IX_AiCapacityLeases_PoolName_ExpiresAt'),
        ('IX_AiCapacityLeases_PoolName_PartitionKey_ExpiresAt')
)
SELECT
    e.index_name,
    CASE WHEN c.relname IS NULL THEN 'missing' ELSE 'present' END AS status
FROM expected e
LEFT JOIN pg_class c ON c.relname = e.index_name
ORDER BY e.index_name;

\echo '== Top slow queries if pg_stat_statements is enabled =='
\set pg_stat_statements_enabled false
SELECT EXISTS (
    SELECT 1
    FROM pg_extension
    WHERE extname = 'pg_stat_statements'
) AS pg_stat_statements_enabled
\gset

\if :pg_stat_statements_enabled
SELECT
    queryid,
    calls,
    round(total_exec_time::numeric, 2) AS total_exec_ms,
    round(mean_exec_time::numeric, 2) AS mean_exec_ms,
    rows,
    left(regexp_replace(query, '\s+', ' ', 'g'), 240) AS query_preview
FROM pg_stat_statements
ORDER BY total_exec_time DESC
LIMIT 20;
\else
SELECT 'pg_stat_statements extension is not enabled; slow query summary is unavailable.' AS message;
\endif
