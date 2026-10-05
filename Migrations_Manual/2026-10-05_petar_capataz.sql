-- ============================================================================
-- Firma de Capataz/Maestro de obra en el PETAR (primer nivel de validación, solo cuando el ejecutante es obrero).
-- Cadena del PETAR: Ejecutante(s) -> Capataz -> Supervisor (Residente/Producción) -> SSOMA.
-- Idempotente. Misma estructura que supervisor_* / ssoma_*.
-- ============================================================================

BEGIN;

ALTER TABLE ss_petar
    ADD COLUMN IF NOT EXISTS capataz_worker_id integer NULL,
    ADD COLUMN IF NOT EXISTS capataz_nombre text NULL,
    ADD COLUMN IF NOT EXISTS capataz_cargo text NULL,
    ADD COLUMN IF NOT EXISTS capataz_firma_url text NULL,
    ADD COLUMN IF NOT EXISTS capataz_firma_hash text NULL,
    ADD COLUMN IF NOT EXISTS capataz_hora_servidor timestamptz NULL;

ALTER TABLE ss_petar_grupo
    ADD COLUMN IF NOT EXISTS capataz_worker_id integer NULL,
    ADD COLUMN IF NOT EXISTS capataz_nombre text NULL,
    ADD COLUMN IF NOT EXISTS capataz_cargo text NULL,
    ADD COLUMN IF NOT EXISTS capataz_firma_url text NULL,
    ADD COLUMN IF NOT EXISTS capataz_firma_hash text NULL,
    ADD COLUMN IF NOT EXISTS capataz_hora_servidor timestamptz NULL;

-- Evidencia de presencia del Capataz cuando firma el PETAR grupal por el link público (selfie + geo), como en el ATS grupal.
ALTER TABLE ss_petar_grupo
    ADD COLUMN IF NOT EXISTS capataz_selfie_url text NULL,
    ADD COLUMN IF NOT EXISTS capataz_selfie_hash text NULL,
    ADD COLUMN IF NOT EXISTS capataz_lat numeric NULL,
    ADD COLUMN IF NOT EXISTS capataz_lng numeric NULL,
    ADD COLUMN IF NOT EXISTS capataz_precision_metros numeric NULL,
    ADD COLUMN IF NOT EXISTS capataz_hora_dispositivo timestamptz NULL;

COMMIT;
