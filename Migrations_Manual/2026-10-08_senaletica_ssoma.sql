-- Señalética de seguridad (NTP 399.010-1): catálogo editable de señales, solo SSOMA.
-- Correr en pgAdmin. Después: cerrar sesión y volver a entrar (allowed_features).
BEGIN;

CREATE TABLE IF NOT EXISTS ss_senal (
    id              SERIAL PRIMARY KEY,
    tipo            VARCHAR(20)  NOT NULL,   -- prohibicion | advertencia | obligacion | emergencia | incendio
    texto           VARCHAR(200) NOT NULL,
    detalle         VARCHAR(300) NULL,
    icono           VARCHAR(60)  NULL,       -- ícono de la biblioteca (tabler), ej. ti-helmet
    imagen_url      TEXT         NULL,       -- pictograma propio; tiene prioridad sobre icono
    es_basica       BOOLEAN      NOT NULL DEFAULT FALSE,
    activo          BOOLEAN      NOT NULL DEFAULT TRUE,
    orden           INTEGER      NOT NULL DEFAULT 1000,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    created_by_id   INTEGER      NULL,
    updated_at      TIMESTAMPTZ  NULL,
    updated_by_id   INTEGER      NULL,
    CONSTRAINT ck_ss_senal_tipo CHECK (tipo IN ('prohibicion','advertencia','obligacion','emergencia','incendio'))
);

CREATE INDEX IF NOT EXISTS ix_ss_senal_tipo ON ss_senal (tipo, orden);

-- Señales básicas (se pueden editar y desactivar, no eliminar).
INSERT INTO ss_senal (tipo, texto, icono, orden, es_basica)
SELECT v.tipo, v.texto, v.icono, v.orden, TRUE
FROM (VALUES
  ('prohibicion', 'Prohibido fumar', 'ti-smoking', 1),
  ('prohibicion', 'Prohibido hacer fuego', 'ti-flame', 2),
  ('prohibicion', 'Prohibido el paso', 'ti-walk', 3),
  ('prohibicion', 'Prohibido el ingreso a personal no autorizado', 'ti-man', 4),
  ('prohibicion', 'Prohibido el uso de celulares', 'ti-phone', 5),
  ('prohibicion', 'Prohibido tomar fotografías', 'ti-camera', 6),
  ('prohibicion', 'Prohibido tocar', 'ti-hand-stop', 7),
  ('prohibicion', 'Prohibido arrojar basura', 'ti-trash', 8),
  ('prohibicion', 'Prohibido el paso de montacargas', 'ti-forklift', 9),
  ('prohibicion', 'Prohibido estacionar', 'ti-parking', 10),
  ('advertencia', 'Peligro', 'ti-exclamation-mark', 11),
  ('advertencia', 'Peligro: riesgo eléctrico', 'ti-bolt', 12),
  ('advertencia', 'Peligro: material inflamable', 'ti-flame', 13),
  ('advertencia', 'Peligro: sustancias tóxicas', 'ti-skull', 14),
  ('advertencia', 'Peligro: sustancias corrosivas', 'ti-flask', 15),
  ('advertencia', 'Peligro: riesgo biológico', 'ti-biohazard', 16),
  ('advertencia', 'Peligro: material radiactivo', 'ti-radioactive', 17),
  ('advertencia', 'Peligro: caída a distinto nivel', 'ti-fall', 18),
  ('advertencia', 'Precaución: piso resbaladizo', 'ti-droplet', 19),
  ('advertencia', 'Precaución: escaleras', 'ti-stairs', 20),
  ('advertencia', 'Precaución: tránsito de montacargas', 'ti-forklift', 21),
  ('advertencia', 'Precaución: tránsito de vehículos', 'ti-truck', 22),
  ('advertencia', 'Precaución: maquinaria en movimiento', 'ti-engine', 23),
  ('obligacion', 'Uso obligatorio de casco', 'ti-helmet', 24),
  ('obligacion', 'Uso obligatorio de lentes de seguridad', 'ti-eyeglass', 25),
  ('obligacion', 'Uso obligatorio de protección auditiva', 'ti-headphones', 26),
  ('obligacion', 'Uso obligatorio de respirador', 'ti-mask', 27),
  ('obligacion', 'Uso obligatorio de guantes', 'ti-hand-grab', 28),
  ('obligacion', 'Uso obligatorio de calzado de seguridad', 'ti-shoe', 29),
  ('obligacion', 'Uso obligatorio de chaleco de seguridad', 'ti-shirt', 30),
  ('obligacion', 'Uso obligatorio de arnés de seguridad', 'ti-arrow-bar-to-up', 31),
  ('obligacion', 'Uso obligatorio de EPP completo', 'ti-shield-check', 32),
  ('obligacion', 'Lávese las manos', 'ti-bucket-droplet', 33),
  ('obligacion', 'Segregue los residuos', 'ti-recycle', 34),
  ('obligacion', 'Use el camino peatonal', 'ti-walk', 35),
  ('emergencia', 'Salida', 'ti-door-exit', 36),
  ('emergencia', 'Ruta de evacuación', 'ti-arrow-up', 37),
  ('emergencia', 'Ruta de evacuación', 'ti-arrow-right', 38),
  ('emergencia', 'Ruta de evacuación', 'ti-arrow-left', 39),
  ('emergencia', 'Ruta de evacuación', 'ti-arrow-down', 40),
  ('emergencia', 'Punto de reunión', 'ti-users-group', 41),
  ('emergencia', 'Zona segura en caso de sismo', 'ti-shield-check', 42),
  ('emergencia', 'Botiquín de primeros auxilios', 'ti-first-aid-kit', 43),
  ('emergencia', 'Ducha y lavaojos de emergencia', 'ti-eye-check', 44),
  ('emergencia', 'Escalera de emergencia', 'ti-stairs', 45),
  ('incendio', 'Extintor', 'ti-fire-extinguisher', 46),
  ('incendio', 'Pulsador de alarma contra incendio', 'ti-bell-ringing', 47),
  ('incendio', 'Teléfono de emergencia', 'ti-phone', 48),
  ('incendio', 'Manguera contra incendio', 'ti-droplet', 49)
) AS v(tipo, texto, icono, orden)
WHERE NOT EXISTS (
    SELECT 1 FROM ss_senal s WHERE s.es_basica AND s.tipo = v.tipo AND s.texto = v.texto AND s.icono = v.icono
);

-- Feature: solo SSOMA. ADMINISTRADOR DEL SISTEMA (1), JEFE SSOMA (9), COORDINADOR SSOMA (70), PREVENCIONISTA (72).
INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.senaletica', m.module_id
FROM module m
WHERE m.module_name = 'SSOMA'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.senaletica');

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE f.feature_key = 'ssoma.gestion.senaletica'
  AND r.role_id::text IN ('1', '9', '70', '72')
ON CONFLICT DO NOTHING;

COMMIT;

-- Verificación:
-- SELECT tipo, count(*) FROM ss_senal GROUP BY tipo;   -- esperado: 49 señales básicas en total
-- SELECT r.role_id FROM role_feature rf JOIN feature f ON f.feature_id = rf.feature_id JOIN role r ON r.role_id = rf.role_id WHERE f.feature_key = 'ssoma.gestion.senaletica';
