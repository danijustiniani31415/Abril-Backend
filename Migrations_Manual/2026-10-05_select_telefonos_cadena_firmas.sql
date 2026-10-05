-- SOLO LECTURA. Quiénes firman la cadena del ATS/PETAR por obra y qué contacto les falta.
-- Residente, Producción, Capataz/Maestro y SSOMA (coordinador del proyecto por correo).
WITH roles AS (
    SELECT pr.project_id, 'Residente' AS rol, pr.residente_workers_id AS worker_id
    FROM project pr
    WHERE pr.state AND pr.active AND pr.residente_workers_id IS NOT NULL

    UNION

    SELECT v.proyecto_id, CASE
               WHEN pu.nombre ILIKE '%Producción%' THEN 'Producción'
               ELSE 'Capataz/Maestro' END, v.worker_id
    FROM worker_vinculaciones v
    JOIN puesto pu ON pu.puesto_id = v.puesto_id
    WHERE v.fecha_fin IS NULL AND v.proyecto_id IS NOT NULL
      AND (pu.nombre ILIKE '%Ingeniero de Producción%' OR pu.nombre ILIKE '%Arquitecto de Producción%'
        OR pu.nombre ILIKE '%Capataz%' OR pu.nombre ILIKE '%Maestro de Obra%')

    UNION

    SELECT pr.project_id, 'SSOMA', w.id
    FROM project pr
    JOIN workers w ON lower(w.email_corporativo) = lower(trim(pr.email_coord_ssoma))
    WHERE pr.state AND pr.active AND pr.email_coord_ssoma IS NOT NULL
)
SELECT pr.project_description AS obra,
       r.rol,
       p.full_name            AS nombre,
       w.email_corporativo    AS correo,
       w.celular_corporativo  AS celular_corporativo,
       p.phone_number         AS celular_personal,
       CASE WHEN coalesce(nullif(trim(w.celular_corporativo), ''), p.phone_number::text) IS NULL
            THEN 'FALTA' ELSE 'ok' END AS telefono
FROM roles r
JOIN project pr ON pr.project_id = r.project_id
JOIN workers w  ON w.id = r.worker_id AND w.workers_estado_id = 1
JOIN person p   ON p.person_id = w.person_id
ORDER BY (coalesce(nullif(trim(w.celular_corporativo), ''), p.phone_number::text) IS NULL) DESC,
         pr.project_description, r.rol, p.full_name;
