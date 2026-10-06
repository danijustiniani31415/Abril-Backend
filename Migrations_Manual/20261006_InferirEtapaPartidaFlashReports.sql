-- Inferir etapa y partida de Flash Reports antiguos (ss_accidente_incidente) por REGLAS de palabras clave.
-- Reemplaza la versión por coincidencia automática (daba demasiados falsos positivos: "piso 5" ≠ etapa PISOS).
-- Regla D1: hay un solo entorno de BD. Los pasos 1 y 2 NO modifican datos; el paso 4 sí, tras revisar.
--
-- Etapas (ssoma_flash_etapa_proyecto): 1 Limpieza, 2 Acero, 3 Andamios en acabados, 4 Carpintería madera,
--   5 Encofrado y desencofrado, 6 Inst. eléctricas, 7 Inst. sanitarias, 9 Mov. de tierras, 10 Muros y tabiques,
--   11 Obras provisionales, 12 Pintura y revestimientos, 13 Pisos, 14 Post venta, 15 Tarrajeos,
--   16 Transporte vertical y horizontal, 17 Varios estructuras.
-- Partidas: solo se infieren las inequívocas; la mayoría de incidentes no corresponde a ninguna partida de costo.

-- ── PASO 1: tabla temporal con la propuesta (no toca ss_accidente_incidente) ──────
DROP TABLE IF EXISTS inferencia_flash;
CREATE TEMP TABLE inferencia_flash AS
WITH base AS (
    SELECT a.id, a.codigo, a.fecha, a.descripcion, a.etapa_proyecto_id, a.partida_id,
           lower(coalesce(a.descripcion, '') || ' ' || coalesce(a.lugar_exacto, '')) AS t
    FROM ss_accidente_incidente a
    WHERE a.etapa_proyecto_id IS NULL OR a.partida_id IS NULL
),
etapa AS (
    SELECT b.id,
      CASE
        WHEN b.t ~ 'desencofr|encofrad'                                           THEN 5
        WHEN b.t ~ 'fierrer|armado de (columna|viga|acero)|habilitado de acero|acero corrugado|varilla de acero|estribo' THEN 2
        WHEN b.t ~ 'excavadora|excavaci|movimiento de tierra|perfilad|talud|muro pantalla' THEN 9
        WHEN b.t ~ 'tarraje'                                                      THEN 15
        WHEN b.t ~ 'pintur|pintor|empast|lijad'                                   THEN 12
        WHEN b.t ~ 'cablead|lumbrer|tablero el[eé]ctrico|instalaci[oó]n(es)? el[eé]ctrica|iiee' THEN 6
        WHEN b.t ~ 'instalaciones sanitarias|iiss|ii\.ss'                         THEN 7
        WHEN b.t ~ 'asentad|tabiquer|drywall|solaque'                             THEN 10
        WHEN b.t ~ 'porcelanato|contrapiso|piso laminado|piso spc|enchape de piso|piso cer[aá]mico' THEN 13
        WHEN b.t ~ 'torre gr[uú]a|elevador|izaje|montacarga'                      THEN 16
        WHEN b.t ~ 'limpieza'                                                     THEN 1
      END AS etapa_id,
      substring(b.t from 'desencofr\w*|encofrad\w*|fierrer\w*|armado de \w+|habilitado de acero|acero corrugado|varilla de acero|estribo\w*|excavadora|excavaci\w*|movimiento de tierra|perfilad\w*|talud|muro pantalla|tarraje\w*|pintur\w*|pintor|empast\w*|lijad\w*|cablead\w*|lumbrer\w*|tablero el.ctrico|instalaci.n(es)? el.ctrica|iiee|instalaciones sanitarias|iiss|asentad\w*|tabiquer\w*|drywall|solaque\w*|porcelanato|contrapiso|piso laminado|piso spc|torre gr.a|elevador|izaje|montacarga|limpieza') AS etapa_palabra
    FROM base b
    WHERE b.etapa_proyecto_id IS NULL
),
partida AS (
    SELECT b.id,
      CASE
        WHEN b.t ~ 'quema(do)? de tecnopor|quemado de tecnopor'  THEN 82
        WHEN b.t ~ 'placing boom'                                 THEN 64
        WHEN b.t ~ 'bomba (de concreto|pluma)|bombeo de concreto' THEN 68
        WHEN b.t ~ 'demolici'                                     THEN 47
        WHEN b.t ~ 'excavadora|excavaci[oó]n (a )?m[aá]quina'     THEN 48
        WHEN b.t ~ 'eliminaci[oó]n masiva'                        THEN 50
        WHEN b.t ~ 'ba[nñ]os? port[aá]til'                        THEN 9
        WHEN b.t ~ 'desencofr|encofrad'                           THEN 70
      END AS partida_id
    FROM base b
    WHERE b.partida_id IS NULL
)
SELECT b.id, b.codigo, b.fecha, left(b.descripcion, 160) AS descripcion,
       e.etapa_id, e.etapa_palabra, p.partida_id
FROM base b
LEFT JOIN etapa   e ON e.id = b.id
LEFT JOIN partida p ON p.id = b.id
WHERE e.etapa_id IS NOT NULL OR p.partida_id IS NOT NULL;

-- ── PASO 2: revisar la propuesta (con nombres) ────────────────────────────────────
SELECT i.id, i.codigo, i.fecha, i.descripcion,
       ep.nombre AS etapa_propuesta, i.etapa_palabra AS por_palabra,
       pa.partida_description AS partida_propuesta
FROM inferencia_flash i
LEFT JOIN ssoma_flash_etapa_proyecto ep ON ep.id = i.etapa_id
LEFT JOIN partida pa ON pa.partida_id = i.partida_id
ORDER BY ep.nombre, i.fecha DESC;

-- Resumen por etapa (para detectar reglas demasiado amplias):
SELECT ep.nombre AS etapa, COUNT(*) AS casos
FROM inferencia_flash i JOIN ssoma_flash_etapa_proyecto ep ON ep.id = i.etapa_id
GROUP BY ep.nombre ORDER BY casos DESC;

-- ── PASO 3: descartar lo dudoso (borrar de la tabla temporal los ids que NO quieras aplicar) ──
-- DELETE FROM inferencia_flash WHERE id IN (...);
-- O quitar solo la etapa de un caso:  UPDATE inferencia_flash SET etapa_id = NULL WHERE id IN (...);

-- ── PASO 4: aplicar (modifica datos; solo rellena los que están en NULL) ──────────
-- Correr en la MISMA sesión de pgAdmin que los pasos anteriores (la tabla es TEMP).
-- UPDATE ss_accidente_incidente a
--    SET etapa_proyecto_id = i.etapa_id
--   FROM inferencia_flash i
--  WHERE a.id = i.id AND i.etapa_id IS NOT NULL AND a.etapa_proyecto_id IS NULL;
--
-- UPDATE ss_accidente_incidente a
--    SET partida_id = i.partida_id
--   FROM inferencia_flash i
--  WHERE a.id = i.id AND i.partida_id IS NOT NULL AND a.partida_id IS NULL;
