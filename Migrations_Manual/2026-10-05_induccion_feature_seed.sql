-- ============================================================================
-- Inducción digital — featureKey usada por InduccionController ([RequireFeature]) y,
-- después, por las rutas del frontend.
--
-- Se concede solo a quienes dictan/gestionan inducciones: ADMINISTRADOR DEL SISTEMA (1),
-- JEFE SSOMA (9) y COORDINADOR SSOMA (70) — ids de Shared/Constants/Roles.cs.
-- Cualquier otro rol se agrega desde /security/roles. Las rutas del trabajador
-- (publico/{token}) no necesitan permiso: son [AllowAnonymous].
--
-- Idempotente. Después de correrlo, cerrar sesión y volver a entrar (allowed_features).
-- ============================================================================
BEGIN;

INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.induccion', m.module_id
FROM module m
WHERE m.module_name = 'SSOMA'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.induccion');

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE f.feature_key = 'ssoma.gestion.induccion'
  AND r.role_id::text IN ('1', '9', '70')
  AND NOT EXISTS (
    SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

COMMIT;

-- Verificación (solo lectura):
-- SELECT r.role_id, r.role_description
-- FROM role_feature rf
-- JOIN feature f ON f.feature_id = rf.feature_id
-- JOIN role r ON r.role_id = rf.role_id
-- WHERE f.feature_key = 'ssoma.gestion.induccion';
-- Esperado: 3 filas (1, 9, 70).
