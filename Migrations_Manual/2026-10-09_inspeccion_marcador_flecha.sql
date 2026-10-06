-- ============================================================================
-- Check list de equipos — marcadores con flecha. Cada número sobre la imagen guía puede llevar una
-- flecha que apunta a la parte exacta del equipo:
--   x_pct / y_pct   = donde va el círculo con el número (ya existían)
--   px_pct / py_pct = punta de la flecha (NULL = el marcador no lleva flecha)
-- Parche sobre 2026-10-07_inspeccion_equipos_checklist_digital.sql. Idempotente.
-- ============================================================================
BEGIN;

ALTER TABLE ss_insp_item_marcador ADD COLUMN IF NOT EXISTS px_pct NUMERIC(5,2);
ALTER TABLE ss_insp_item_marcador ADD COLUMN IF NOT EXISTS py_pct NUMERIC(5,2);

-- La punta se define completa (x e y) o no se define.
ALTER TABLE ss_insp_item_marcador DROP CONSTRAINT IF EXISTS ck_ss_insp_item_marcador_punta;
ALTER TABLE ss_insp_item_marcador ADD CONSTRAINT ck_ss_insp_item_marcador_punta
    CHECK ((px_pct IS NULL AND py_pct IS NULL)
        OR (px_pct IS NOT NULL AND py_pct IS NOT NULL AND px_pct BETWEEN 0 AND 100 AND py_pct BETWEEN 0 AND 100));

COMMIT;

-- Verificación (solo lectura):
-- SELECT column_name FROM information_schema.columns WHERE table_name = 'ss_insp_item_marcador';  -- debe listar px_pct y py_pct
