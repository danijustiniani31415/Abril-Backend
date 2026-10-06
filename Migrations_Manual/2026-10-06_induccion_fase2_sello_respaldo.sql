-- ============================================================================
-- INDUCCIÓN DIGITAL — Fase 2: sellado de tiempo externo (RFC 3161) y respaldo.
-- Solo CREA tablas/columnas; no cambia ningún dato ni bloquea nada (los candados contra
-- edición/borrado van en 2026-10-06_induccion_fase2_proteccion.sql, para correr al terminar
-- las pruebas). Idempotente.
-- ============================================================================

BEGIN;

-- SHA-256 REAL de los bytes del acta PDF (acta_pdf_hash es el código estable del QR, no el hash del archivo).
ALTER TABLE ss_ind_sesion ADD COLUMN IF NOT EXISTS acta_pdf_sha256 text NULL;

-- Un sello de tiempo por sesión cerrada. El digest sellado = SHA-256( acta_pdf_sha256 | último hash de la
-- auditoría ), así queda sellado tanto el PDF como la cadena de eventos que lo precede.
CREATE TABLE IF NOT EXISTS ss_ind_sello (
    id              serial PRIMARY KEY,
    sesion_id       int NOT NULL UNIQUE REFERENCES ss_ind_sesion(id),
    digest          text NOT NULL,            -- hex SHA-256 enviado a la TSA
    componentes     text NOT NULL,            -- "acta_sha256|hash_auditoria" para reproducir el digest
    proveedor       text NOT NULL,            -- URL de la TSA
    estado          text NOT NULL DEFAULT 'Pendiente',   -- Pendiente | Sellado | Error
    token           bytea NULL,               -- respuesta RFC 3161 completa (.tsr)
    sellado_en      timestamptz NULL,         -- hora que certifica la TSA (genTime)
    serial_tsa      text NULL,
    emisor_tsa      text NULL,                -- sujeto del certificado de la TSA
    intentos        int NOT NULL DEFAULT 0,
    ultimo_error    text NULL,
    ultimo_intento  timestamptz NULL,
    created_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_ss_ind_sello_estado ON ss_ind_sello (estado, ultimo_intento);

-- Respaldo de todos los archivos de la sesión (acta, firmas, selfies, versiones de documentos y un
-- manifiesto con hashes) en OTRA cuenta de almacenamiento.
CREATE TABLE IF NOT EXISTS ss_ind_respaldo (
    id              serial PRIMARY KEY,
    sesion_id       int NOT NULL UNIQUE REFERENCES ss_ind_sesion(id),
    estado          text NOT NULL DEFAULT 'Pendiente',   -- Pendiente | Respaldado | Error | NoConfigurado
    archivos        int NOT NULL DEFAULT 0,
    bytes           bigint NOT NULL DEFAULT 0,
    prefijo         text NULL,                -- carpeta dentro del contenedor de respaldo
    respaldado_en   timestamptz NULL,
    intentos        int NOT NULL DEFAULT 0,
    ultimo_error    text NULL,
    ultimo_intento  timestamptz NULL,
    created_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_ss_ind_respaldo_estado ON ss_ind_respaldo (estado, ultimo_intento);

COMMIT;

-- Verificación (solo lectura):
-- SELECT column_name FROM information_schema.columns WHERE table_name = 'ss_ind_sesion' AND column_name = 'acta_pdf_sha256';
-- SELECT count(*) FROM ss_ind_sello;     -- 0
-- SELECT count(*) FROM ss_ind_respaldo;  -- 0
