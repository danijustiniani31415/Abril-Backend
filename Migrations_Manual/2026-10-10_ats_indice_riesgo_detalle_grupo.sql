-- ============================================================================
-- ATS Digital — índice que faltaba para el listado.
--
-- El listado de ATS grupales calcula "¿requiere PETAR?" por cada fila con
--   EXISTS (SELECT 1 FROM ss_ats_grupo_riesgo_detalle WHERE ats_grupo_id = g.id AND ...)
-- y esa FK no tiene índice (Postgres no los crea solo): cada fila de la página
-- recorría la tabla completa, y crece con cada ATS grupal que se crea.
--
-- Idempotente y seguro: crear un índice no cambia datos ni consultas, solo las acelera.
-- ============================================================================
CREATE INDEX IF NOT EXISTS ix_ss_ats_grupo_riesgo_detalle_grupo
    ON ss_ats_grupo_riesgo_detalle (ats_grupo_id);

-- Verificación (solo lectura):
-- SELECT indexname FROM pg_indexes WHERE tablename = 'ss_ats_grupo_riesgo_detalle';
