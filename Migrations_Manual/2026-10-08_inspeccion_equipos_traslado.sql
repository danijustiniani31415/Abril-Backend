-- ============================================================================
-- Check list de equipos — el equipo viaja entre proyectos (parche sobre
-- 2026-10-07_inspeccion_equipos_checklist_digital.sql; correr DESPUÉS de ese).
--
-- Los códigos de campo son cortos ("281", "035") y se repiten, y el mismo equipo
-- pasa de un proyecto a otro. Por eso:
--   * La identidad del equipo es su id interno; "codigo" es solo la etiqueta que
--     trae físicamente. Se quita el índice único por (proyecto, tipo, código).
--   * ss_insp_equipo.proyecto_id pasa a significar "proyecto ACTUAL": se actualiza
--     con cada traslado. Cada inspección conserva su propio proyecto_id, así que
--     el historial por proyecto no se pierde.
--   * Nuevo evento 'traslado' en el historial del equipo.
--
-- Idempotente.
-- ============================================================================
BEGIN;

DROP INDEX IF EXISTS ux_ss_insp_equipo_proyecto_tipo_codigo;

-- Búsqueda de candidatos por código (no único)
CREATE INDEX IF NOT EXISTS ix_ss_insp_equipo_tipo_variante_codigo
    ON ss_insp_equipo (tipo_id, variante_id, codigo);

-- Permitir el evento 'traslado'
ALTER TABLE ss_insp_equipo_evento DROP CONSTRAINT IF EXISTS ss_insp_equipo_evento_evento_check;
ALTER TABLE ss_insp_equipo_evento
    ADD CONSTRAINT ss_insp_equipo_evento_evento_check
    CHECK (evento IN ('bloqueo','liberacion','baja','reactivacion','traslado'));

COMMIT;

-- Verificación (solo lectura):
-- SELECT indexname FROM pg_indexes WHERE tablename = 'ss_insp_equipo';   -- ya no debe aparecer ux_ss_insp_equipo_proyecto_tipo_codigo
-- SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ss_insp_equipo_evento_evento_check';
