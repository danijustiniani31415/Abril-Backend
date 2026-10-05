-- ============================================================================
-- Código por proyecto del PETAR grupal, igual que el ATS grupal: "{ABREV}-PETG-0001"
-- (correlativo propio por proyecto en ss_documento_correlativo, tipo 'PETG').
-- Idempotente. Incluye el relleno de los PETAR grupales que ya existen.
-- ============================================================================

BEGIN;

ALTER TABLE ss_petar_grupo ADD COLUMN IF NOT EXISTS codigo text NULL;

-- Relleno de los existentes: correlativo por proyecto en orden de creación.
UPDATE ss_petar_grupo g
SET codigo = upper(trim(coalesce(p.abbreviation, p.codigo, 'PRY'))) || '-PETG-' || lpad(n.rn::text, 4, '0')
FROM (SELECT id, proyecto_id, row_number() OVER (PARTITION BY proyecto_id ORDER BY id) AS rn
      FROM ss_petar_grupo) n
JOIN project p ON p.project_id = n.proyecto_id
WHERE g.id = n.id AND g.codigo IS NULL;

-- Deja el correlativo al día para que el próximo PETAR grupal siga la numeración.
INSERT INTO ss_documento_correlativo (proyecto_id, tipo, ultimo)
SELECT proyecto_id, 'PETG', count(*) FROM ss_petar_grupo GROUP BY proyecto_id
ON CONFLICT (proyecto_id, tipo) DO UPDATE
SET ultimo = GREATEST(ss_documento_correlativo.ultimo, EXCLUDED.ultimo);

COMMIT;

-- Verificación (solo lectura):
-- SELECT id, proyecto_id, codigo FROM ss_petar_grupo ORDER BY proyecto_id, id;
