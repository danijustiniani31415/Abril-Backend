-- Migración manual (pgAdmin) — que SOLO Almendra Huaripoma pueda eliminar observaciones de Revisiones.
-- El botón de eliminar y el endpoint ya exigen la feature 'arquitectura-comercial.revisiones.eliminar'
-- (ver 20260911_AddFeatureEliminarRevisionesObservaciones.sql). Los permisos se dan por ROL, así que
-- se crea un rol propio con solo esa feature y se le asigna únicamente a ella.
-- Después de correrlo, Almendra debe cerrar sesión y volver a entrar (D4).

-- 0) Verificar ANTES de correr (solo lectura): debe devolver 1 fila (la persona) y 1 fila (la feature).
-- SELECT p.person_id, p.user_id, p.full_name FROM person p WHERE p.full_name ILIKE '%huaripoma%';
-- SELECT feature_id FROM feature WHERE feature_key = 'arquitectura-comercial.revisiones.eliminar';

-- 1) Feature (por si la migración 20260911 no se corrió)
INSERT INTO feature (feature_key, module_id)
SELECT 'arquitectura-comercial.revisiones.eliminar', module_id
FROM feature
WHERE feature_key = 'arquitectura-comercial.revisiones'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'arquitectura-comercial.revisiones.eliminar');

-- 2) Rol dedicado
INSERT INTO role (role_description, created_user_id)
SELECT 'ELIMINAR OBSERVACIONES REVISIONES', 1
WHERE NOT EXISTS (SELECT 1 FROM role WHERE role_description = 'ELIMINAR OBSERVACIONES REVISIONES');

-- 3) El rol tiene solo esa feature
INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
JOIN feature f ON f.feature_key = 'arquitectura-comercial.revisiones.eliminar'
WHERE r.role_description = 'ELIMINAR OBSERVACIONES REVISIONES'
ON CONFLICT DO NOTHING;

-- 4) Asignarlo a Almendra Huaripoma (usuario de su persona). Confirmar que el ILIKE devuelve UNA sola persona.
INSERT INTO user_role (user_id, role_id, created_user_id)
SELECT p.user_id, r.role_id, 1
FROM person p
JOIN role r ON r.role_description = 'ELIMINAR OBSERVACIONES REVISIONES'
WHERE p.full_name ILIKE '%huaripoma%'
  AND p.user_id IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM user_role ur WHERE ur.user_id = p.user_id AND ur.role_id = r.role_id);
