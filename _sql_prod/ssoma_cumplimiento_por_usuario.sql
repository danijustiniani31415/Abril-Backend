-- Cumplimiento SSOMA: el checklist pasa a ser individual por usuario (cada prevencionista /
-- coordinador lleva el suyo) en vez de uno compartido por proyecto.
-- Correr en pgAdmin. Es un solo entorno de BD (ver CLAUDE.md D1): revisar antes de ejecutar.

BEGIN;

-- 1) Nueva columna: dueño del registro
ALTER TABLE ss_cumplimiento_registro ADD COLUMN IF NOT EXISTS usuario_id INT NULL;

-- 2) Registros existentes: pasan a ser de quien los marcó (Dante Facho quedará dueño de los suyos;
--    Isabel Mejia verá su checklist limpio). Los "pendiente" sin responsable quedan en NULL
--    y no se muestran a nadie (no hay información que se pierda).
UPDATE ss_cumplimiento_registro
SET usuario_id = cumplido_por_id
WHERE usuario_id IS NULL AND cumplido_por_id IS NOT NULL;

-- 3) Cambiar la unicidad: ahora incluye al usuario.
--    El UNIQUE original se creó inline en la tabla, así que se busca por columnas en vez de por nombre.
DO $$
DECLARE r RECORD;
BEGIN
    FOR r IN
        SELECT c.conname
        FROM pg_constraint c
        WHERE c.conrelid = 'ss_cumplimiento_registro'::regclass
          AND c.contype = 'u'
          AND (SELECT array_agg(a.attname ORDER BY a.attname)
               FROM unnest(c.conkey) k JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k)
              = ARRAY['actividad_id','periodo','proyecto_id']::name[]
    LOOP
        EXECUTE format('ALTER TABLE ss_cumplimiento_registro DROP CONSTRAINT %I', r.conname);
    END LOOP;
END $$;

-- Por si el índice único lo creó EF con este nombre en algún entorno
DROP INDEX IF EXISTS ix_ss_cumplimiento_registro_actividad_id_proyecto_id_periodo;

-- (nombre corto: el que genera EF superaría los 63 caracteres de Postgres)
CREATE UNIQUE INDEX IF NOT EXISTS ix_ss_cumpl_registro_act_proy_periodo_usuario
    ON ss_cumplimiento_registro (actividad_id, proyecto_id, periodo, usuario_id);

COMMIT;

-- Verificación (solo lectura)
-- SELECT usuario_id, COUNT(*) FROM ss_cumplimiento_registro GROUP BY usuario_id ORDER BY 2 DESC;
-- SELECT indexname FROM pg_indexes WHERE tablename = 'ss_cumplimiento_registro';
