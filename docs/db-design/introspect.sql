-- Đọc cấu trúc một database (một schema nghiệp vụ) thành JSON cho build.py.
-- psql -v schema=team -At -f introspect.sql
SELECT json_build_object(
  'tables', (
    SELECT coalesce(json_agg(t ORDER BY t.ord), '[]') FROM (
      SELECT c.relname AS name, c.oid::int AS ord,
        (SELECT json_agg(json_build_object(
            'name', a.attname,
            'type', format_type(a.atttypid, a.atttypmod),
            'nullable', NOT a.attnotnull,
            'default', pg_get_expr(d.adbin, d.adrelid),
            'identity', a.attidentity <> ''
          ) ORDER BY a.attnum)
         FROM pg_attribute a
         LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
         WHERE a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped) AS columns,
        (SELECT coalesce(json_agg(json_build_object(
            'name', k.conname,
            'type', k.contype,
            'def', pg_get_constraintdef(k.oid),
            'cols', (SELECT json_agg(a.attname ORDER BY x.n) FROM unnest(k.conkey) WITH ORDINALITY x(attnum, n)
                     JOIN pg_attribute a ON a.attrelid = k.conrelid AND a.attnum = x.attnum),
            'ref', CASE WHEN k.contype = 'f' THEN (SELECT relname FROM pg_class WHERE oid = k.confrelid) END
          ) ORDER BY k.contype DESC, k.conname), '[]')
         FROM pg_constraint k WHERE k.conrelid = c.oid) AS constraints,
        (SELECT coalesce(json_agg(pg_get_indexdef(i.indexrelid) ORDER BY ic.relname), '[]')
         FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid
         WHERE i.indrelid = c.oid
           AND NOT EXISTS (SELECT 1 FROM pg_constraint k WHERE k.conindid = i.indexrelid)) AS indexes,
        (SELECT coalesce(json_agg(json_build_object('name', g.tgname, 'def', pg_get_triggerdef(g.oid)) ORDER BY g.tgname), '[]')
         FROM pg_trigger g WHERE g.tgrelid = c.oid AND NOT g.tgisinternal) AS triggers
      FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
      WHERE n.nspname = :'schema' AND c.relkind IN ('r', 'p')
    ) t
  )
);
