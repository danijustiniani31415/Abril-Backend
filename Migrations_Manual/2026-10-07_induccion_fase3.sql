-- ============================================================================
-- INDUCCIÓN DIGITAL — Fase 3: vigencia, bloqueo del ATS, exenciones y alertas.
-- Solo CREA tablas; no cambia datos ni bloquea nada. El bloqueo del ATS viene APAGADO en todos los proyectos
-- (se enciende uno por uno desde la pestaña «Cumplimiento»). Idempotente.
-- ============================================================================

BEGIN;

-- Bloqueo por proyecto: si activo, un trabajador sin inducción vigente no puede firmar ATS en ese proyecto.
CREATE TABLE IF NOT EXISTS ss_ind_gate_proyecto (
    id                      serial PRIMARY KEY,
    proyecto_id             int NOT NULL UNIQUE,
    activo                  boolean NOT NULL DEFAULT false,
    actualizado_en          timestamptz NOT NULL DEFAULT now(),
    actualizado_por_user_id int NULL
);

-- Exención temporal: trabajadores inducidos en papel antes de digitalizar, o casos puntuales autorizados.
-- Nunca se borra: se revoca (queda quién, cuándo y por qué).
CREATE TABLE IF NOT EXISTS ss_ind_exencion (
    id                    serial PRIMARY KEY,
    worker_id             int NOT NULL,
    proyecto_id           int NOT NULL,
    motivo                text NOT NULL,
    hasta                 date NULL,                  -- null = sin vencimiento
    creado_por_user_id    int NULL,
    creado_en             timestamptz NOT NULL DEFAULT now(),
    revocada_en           timestamptz NULL,
    revocada_por_user_id  int NULL
);
CREATE INDEX IF NOT EXISTS ix_ss_ind_exencion_proyecto ON ss_ind_exencion (proyecto_id, worker_id);

-- Resumen semanal de inducciones por renovar, para no avisar dos veces lo mismo (una fila por proyecto y semana).
CREATE TABLE IF NOT EXISTS ss_ind_alerta (
    id           serial PRIMARY KEY,
    proyecto_id  int NOT NULL,
    tipo         text NOT NULL,
    periodo      text NOT NULL,                       -- ej. 2026-W41
    detalle      text NULL,
    creada_en    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_ss_ind_alerta UNIQUE (proyecto_id, tipo, periodo)
);

-- Tipo de notificación (campanita) del resumen semanal. Mismo patrón que ATS_PENDIENTE_FIRMA (2026-09-29).
-- Sin esta fila el aviso no se crea.
INSERT INTO notificacion_tipo (codigo, nombre, orden, created_date_time, active, state)
VALUES ('INDUCCION_POR_RENOVAR', 'Inducciones por renovar', 51, now(), true, true)
ON CONFLICT (codigo) WHERE (state = true)
DO UPDATE SET nombre = EXCLUDED.nombre, orden = EXCLUDED.orden, active = true, updated_date_time = now();

COMMIT;

-- Verificación (solo lectura):
-- SELECT codigo, nombre FROM notificacion_tipo WHERE codigo = 'INDUCCION_POR_RENOVAR';  -- 1 fila
-- SELECT count(*) FROM ss_ind_gate_proyecto;   -- 0 (ningún bloqueo encendido)
-- SELECT count(*) FROM ss_ind_exencion;        -- 0
-- SELECT count(*) FROM ss_ind_alerta;          -- 0
