-- ============================================================================
-- Limpieza de firmas de validación guardadas FUERA DE ORDEN (antes de las reglas nuevas).
-- Cadena obligatoria: Capataz -> Residente/Producción (Autoriza / Supervisor del PETAR) -> SSOMA.
--
-- OJO (D1): la BD es la de producción. Orden de trabajo:
--   1) Correr la PARTE A (solo lectura) y revisar qué filas aparecen.
--   2) Correr la PARTE B tal cual: termina en ROLLBACK, solo muestra cuántas filas tocaría.
--   3) Si los conteos son los esperados, cambiar el ROLLBACK final por COMMIT y volver a correrla.
-- Lo que se limpia queda para volver a firmar EN ORDEN; las firmas de los trabajadores no se tocan.
-- ============================================================================

-- ============================ PARTE A — SOLO LECTURA ========================

-- A1. PETAR grupales con firma de Supervisor/SSOMA y NINGÚN trabajador adherido firmado.
SELECT g.id, g.codigo, g.ats_grupo_id, g.estado,
       g.supervisor_nombre, g.supervisor_hora_servidor, g.ssoma_nombre, g.ssoma_hora_servidor
FROM ss_petar_grupo g
WHERE (g.supervisor_firma_url IS NOT NULL OR g.ssoma_firma_url IS NOT NULL)
  AND NOT EXISTS (SELECT 1 FROM ss_petar p WHERE p.petar_grupo_id = g.id AND p.firma_url IS NOT NULL)
ORDER BY g.id;

-- A2. PETAR grupales con SSOMA firmado SIN firma del Supervisor.
SELECT g.id, g.codigo, g.ats_grupo_id, g.ssoma_nombre, g.ssoma_hora_servidor
FROM ss_petar_grupo g
WHERE g.ssoma_firma_url IS NOT NULL AND g.supervisor_firma_url IS NULL
ORDER BY g.id;

-- A3. PETAR individuales con SSOMA firmado SIN firma del Supervisor.
SELECT p.id, p.codigo, p.estado, p.ssoma_nombre, p.ssoma_hora_servidor
FROM ss_petar p
WHERE p.petar_grupo_id IS NULL AND p.ssoma_firma_url IS NOT NULL AND p.supervisor_firma_url IS NULL
ORDER BY p.id;

-- A4. ATS (cualquiera) con Visto Bueno SSOMA SIN firma de Autoriza.
SELECT a.id, a.codigo, a.ats_grupo_id, a.ssoma_nombre, a.ssoma_hora_servidor
FROM ss_ats a
WHERE a.ssoma_firma_url IS NOT NULL AND a.autoriza_firma_url IS NULL
ORDER BY a.id;

-- A5. SOLO REVISIÓN (no se corrige solo): PETAR grupales con Supervisor firmado cuyo ATS grupal NO tiene firma
--     de Capataz. Si la cuadrilla es de obreros, el Supervisor firmó antes que el Capataz; si es de Staff está bien.
SELECT g.id AS petar_grupo_id, g.codigo, ag.id AS ats_grupo_id, ag.codigo AS ats_codigo,
       g.supervisor_nombre, g.supervisor_hora_servidor
FROM ss_petar_grupo g
JOIN ss_ats_grupo ag ON ag.id = g.ats_grupo_id
WHERE g.supervisor_firma_url IS NOT NULL AND ag.capataz_firma_url IS NULL
ORDER BY g.id;

-- A6. SOLO REVISIÓN: ATS grupales todavía ABIERTOS ('Activo') que ya traen firmas de validación (del flujo anterior,
--     donde se podía validar sin cerrar). Con la regla nueva deberían validarse con la cuadrilla cerrada.
SELECT ag.id, ag.codigo, ag.estado,
       (ag.capataz_firma_url IS NOT NULL) AS capataz_firmado,
       count(*) FILTER (WHERE a.autoriza_firma_url IS NOT NULL) AS autoriza_firmados,
       count(*) FILTER (WHERE a.ssoma_firma_url IS NOT NULL)    AS ssoma_firmados,
       count(*) FILTER (WHERE a.estado = 'Firmado')             AS ejecutantes_firmados
FROM ss_ats_grupo ag
LEFT JOIN ss_ats a ON a.ats_grupo_id = ag.id
WHERE ag.estado = 'Activo'
GROUP BY ag.id, ag.codigo, ag.estado, ag.capataz_firma_url
HAVING ag.capataz_firma_url IS NOT NULL
    OR count(*) FILTER (WHERE a.autoriza_firma_url IS NOT NULL OR a.ssoma_firma_url IS NOT NULL) > 0
ORDER BY ag.id;

-- ============================ PARTE B — CORRECCIÓN ==========================
-- Termina en ROLLBACK (ensayo). Cambiar a COMMIT solo tras revisar los conteos.

BEGIN;

-- B1. PETAR grupales con Supervisor/SSOMA y sin ningún adherido firmado -> quitar ambas firmas.
WITH x AS (
    UPDATE ss_petar_grupo g SET
        supervisor_worker_id = NULL, supervisor_nombre = NULL, supervisor_cargo = NULL,
        supervisor_firma_url = NULL, supervisor_firma_hash = NULL, supervisor_hora_servidor = NULL,
        ssoma_worker_id = NULL, ssoma_nombre = NULL, ssoma_cargo = NULL,
        ssoma_firma_url = NULL, ssoma_firma_hash = NULL, ssoma_hora_servidor = NULL,
        updated_at = now()
    WHERE (g.supervisor_firma_url IS NOT NULL OR g.ssoma_firma_url IS NOT NULL)
      AND NOT EXISTS (SELECT 1 FROM ss_petar p WHERE p.petar_grupo_id = g.id AND p.firma_url IS NOT NULL)
    RETURNING g.id)
SELECT 'B1 petar_grupo sin adheridos' AS paso, count(*) AS filas FROM x;

-- B2. PETAR grupales con SSOMA sin Supervisor -> quitar la firma de SSOMA (volverá a firmar después del Supervisor).
WITH x AS (
    UPDATE ss_petar_grupo g SET
        ssoma_worker_id = NULL, ssoma_nombre = NULL, ssoma_cargo = NULL,
        ssoma_firma_url = NULL, ssoma_firma_hash = NULL, ssoma_hora_servidor = NULL,
        updated_at = now()
    WHERE g.ssoma_firma_url IS NOT NULL AND g.supervisor_firma_url IS NULL
    RETURNING g.id)
SELECT 'B2 petar_grupo ssoma sin supervisor' AS paso, count(*) AS filas FROM x;

-- B3. PETAR individuales con SSOMA sin Supervisor -> quitar la firma de SSOMA (el estado sigue 'Borrador').
WITH x AS (
    UPDATE ss_petar p SET
        ssoma_worker_id = NULL, ssoma_nombre = NULL, ssoma_cargo = NULL,
        ssoma_firma_url = NULL, ssoma_firma_hash = NULL, ssoma_hora_servidor = NULL,
        updated_at = now()
    WHERE p.petar_grupo_id IS NULL AND p.ssoma_firma_url IS NOT NULL AND p.supervisor_firma_url IS NULL
    RETURNING p.id)
SELECT 'B3 petar individual ssoma sin supervisor' AS paso, count(*) AS filas FROM x;

-- B4. ATS con Visto Bueno SSOMA sin Autoriza -> quitar la firma de SSOMA.
WITH x AS (
    UPDATE ss_ats a SET
        ssoma_worker_id = NULL, ssoma_nombre = NULL, ssoma_cargo = NULL,
        ssoma_firma_url = NULL, ssoma_firma_hash = NULL, ssoma_hora_servidor = NULL
    WHERE a.ssoma_firma_url IS NOT NULL AND a.autoriza_firma_url IS NULL
    RETURNING a.id)
SELECT 'B4 ats ssoma sin autoriza' AS paso, count(*) AS filas FROM x;

-- Las partes A5 y A6 son de revisión: decide fila por fila (no se corrigen automáticamente porque dependen de si
-- la cuadrilla es de obreros y de si ya se usó el documento en obra).

ROLLBACK;   -- <-- cambiar a COMMIT solo después de revisar los conteos de arriba

-- ============================ PARTE C — REGULARIZAR CUADRILLAS ABIERTAS ======
-- Cuadrillas 'Activo' que YA tienen la validación completa (resultado de A6: ATSG-0001 y ATSG-0002).
-- No se limpian: su cadena es válida. Solo se pasan a 'Cerrado' (la regla nueva exige cuadrilla cerrada para validar).
-- Se limita a ids explícitos (3 y 4) y deja el evento de auditoría. ROLLBACK por defecto.

BEGIN;

WITH x AS (
    UPDATE ss_ats_grupo
    SET estado = 'Cerrado', updated_at = now()
    WHERE id IN (3, 4) AND estado = 'Activo'
    RETURNING id)
SELECT 'C1 cuadrillas cerradas' AS paso, count(*) AS filas FROM x;

INSERT INTO ss_ats_grupo_evento (ats_grupo_id, evento, worker_id, detalle, created_at)
SELECT id, 'Cerrado', NULL, 'Regularizado por migración: cadena de validación ya completa (regla nueva: validar solo con cuadrilla cerrada)', now()
FROM ss_ats_grupo WHERE id IN (3, 4) AND estado = 'Cerrado';

ROLLBACK;   -- <-- cambiar a COMMIT tras ver C1 = 2
