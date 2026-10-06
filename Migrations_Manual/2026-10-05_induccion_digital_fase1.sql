-- ============================================================================
-- INDUCCIÓN DIGITAL — Fase 1 (reemplaza SSO-FO-118, SSO-FO-119, SSO-FO-031, SSO-FO-091 y GTH-FO-17)
-- Idempotente. Sin FK hacia worker/project (solo ids con índice), igual de seguro que
-- no adivinar nombres de PK ajenos; las tablas propias sí llevan FK entre sí.
-- Código de sesión por proyecto: "{ABREV}-IND-0001" en ss_documento_correlativo (tipo 'IND').
-- ============================================================================

BEGIN;

-- ── Catálogo de documentos entregados (versionados, inmutables por versión) ──
CREATE TABLE IF NOT EXISTS ss_ind_documento (
    id            serial PRIMARY KEY,
    codigo        text NOT NULL,                 -- SSO-DO-04, SSO-FO-089…
    nombre        text NOT NULL,
    descripcion   text NULL,
    tipo          text NOT NULL DEFAULT 'Documento',  -- Documento | Procedimiento | IPERC | Legal
    obligatorio   boolean NOT NULL DEFAULT true,
    orden         int NOT NULL DEFAULT 0,
    activo        boolean NOT NULL DEFAULT true,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_ss_ind_documento_codigo UNIQUE (codigo)
);

-- Una fila por versión publicada. Nunca se edita: un cambio crea la versión siguiente.
CREATE TABLE IF NOT EXISTS ss_ind_documento_version (
    id                   serial PRIMARY KEY,
    documento_id         int NOT NULL REFERENCES ss_ind_documento(id),
    version              int NOT NULL,
    archivo_url          text NOT NULL,
    archivo_hash         text NOT NULL,          -- SHA-256 del archivo
    es_cambio_material   boolean NOT NULL DEFAULT false,
    nota_cambio          text NULL,
    vigente              boolean NOT NULL DEFAULT true,
    publicado_por_user_id int NULL,
    publicado_en         timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_ss_ind_documento_version UNIQUE (documento_id, version)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ss_ind_documento_version_vigente
    ON ss_ind_documento_version (documento_id) WHERE vigente;

-- ── Temario (SSO-FO-031, 28 temas) editable ──
CREATE TABLE IF NOT EXISTS ss_ind_tema (
    id          serial PRIMARY KEY,
    orden       int NOT NULL,
    descripcion text NOT NULL,
    activo      boolean NOT NULL DEFAULT true
);

-- ── Banco de preguntas (borrador editable) ──
CREATE TABLE IF NOT EXISTS ss_ind_pregunta (
    id          serial PRIMARY KEY,
    tipo        text NOT NULL,                   -- VF | Alternativa | Relacionar
    enunciado   text NOT NULL,
    puntos      numeric(5,2) NOT NULL DEFAULT 1,
    orden       int NOT NULL DEFAULT 0,
    activo      boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now()
);

-- VF: 1 opción por pregunta con es_correcta = (la afirmación es verdadera).
-- Alternativa: N opciones, es_correcta marca la correcta.
-- Relacionar: cada fila es un ítem (texto) y par_texto su pareja correcta.
CREATE TABLE IF NOT EXISTS ss_ind_pregunta_opcion (
    id           serial PRIMARY KEY,
    pregunta_id  int NOT NULL REFERENCES ss_ind_pregunta(id) ON DELETE CASCADE,
    texto        text NOT NULL,
    es_correcta  boolean NOT NULL DEFAULT false,
    par_texto    text NULL,
    orden        int NOT NULL DEFAULT 0
);

-- Evaluación publicada: foto inmutable del banco (preguntas + claves) al publicar.
CREATE TABLE IF NOT EXISTS ss_ind_evaluacion_version (
    id             serial PRIMARY KEY,
    version        int NOT NULL,
    nota_minima    numeric(5,2) NOT NULL DEFAULT 14,
    nota_maxima    numeric(5,2) NOT NULL DEFAULT 20,
    duracion_min   int NOT NULL DEFAULT 15,
    contenido_json jsonb NOT NULL,
    contenido_hash text NOT NULL,
    vigente        boolean NOT NULL DEFAULT true,
    publicado_por_user_id int NULL,
    publicado_en   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_ss_ind_evaluacion_version UNIQUE (version)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ss_ind_evaluacion_version_vigente
    ON ss_ind_evaluacion_version ((true)) WHERE vigente;

-- ── Sesión de inducción (la "sala") ──
CREATE TABLE IF NOT EXISTS ss_ind_sesion (
    id                       serial PRIMARY KEY,
    codigo                   text NULL,
    proyecto_id              int NOT NULL,
    fecha                    date NOT NULL,
    tipo                     text NOT NULL DEFAULT 'InduccionHombreNuevo',
    horas                    numeric(4,1) NOT NULL DEFAULT 2,
    instructor_worker_id     int NOT NULL,
    instructor_nombre        text NULL,
    instructor_cargo         text NULL,
    evaluacion_version_id    int NOT NULL REFERENCES ss_ind_evaluacion_version(id),
    qr_token                 uuid NOT NULL DEFAULT gen_random_uuid(),
    qr_expira_en             timestamptz NOT NULL,
    estado                   text NOT NULL DEFAULT 'Abierta',   -- Abierta | Cerrada | Anulada
    instructor_firma_url     text NULL,
    instructor_firma_hash    text NULL,
    instructor_hora_servidor timestamptz NULL,
    cerrada_en               timestamptz NULL,
    acta_pdf_url             text NULL,
    acta_pdf_hash            text NULL,
    anulado_motivo           text NULL,
    anulado_por_user_id      int NULL,
    anulado_en               timestamptz NULL,
    created_by_user_id       int NULL,
    created_at               timestamptz NOT NULL DEFAULT now(),
    updated_at               timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_ss_ind_sesion_qr UNIQUE (qr_token)
);
CREATE INDEX IF NOT EXISTS ix_ss_ind_sesion_proyecto_fecha ON ss_ind_sesion (proyecto_id, fecha DESC);

-- Foto de los documentos (con versión) y temas que cubrió ESA sesión.
CREATE TABLE IF NOT EXISTS ss_ind_sesion_documento (
    id                   serial PRIMARY KEY,
    sesion_id            int NOT NULL REFERENCES ss_ind_sesion(id) ON DELETE CASCADE,
    documento_version_id int NOT NULL REFERENCES ss_ind_documento_version(id),
    CONSTRAINT uq_ss_ind_sesion_documento UNIQUE (sesion_id, documento_version_id)
);
CREATE TABLE IF NOT EXISTS ss_ind_sesion_tema (
    id         serial PRIMARY KEY,
    sesion_id  int NOT NULL REFERENCES ss_ind_sesion(id) ON DELETE CASCADE,
    tema_id    int NULL REFERENCES ss_ind_tema(id),
    orden      int NOT NULL,
    descripcion text NOT NULL,                  -- snapshot del texto
    CONSTRAINT uq_ss_ind_sesion_tema UNIQUE (sesion_id, orden)
);

-- ── Asistencia / firma de cada trabajador ──
CREATE TABLE IF NOT EXISTS ss_ind_asistencia (
    id                    serial PRIMARY KEY,
    sesion_id             int NOT NULL REFERENCES ss_ind_sesion(id) ON DELETE CASCADE,
    worker_id             int NOT NULL,
    -- snapshot de los datos que hoy van a mano en los formatos
    nombre_completo       text NULL,            -- "Apellidos Nombres"
    documento_identidad   text NULL,
    categoria             text NULL,
    especialidad          text NULL,
    contratista_nombre    text NULL,
    experiencia_actividad text NULL,
    estado                text NOT NULL DEFAULT 'Pendiente',  -- Pendiente | Firmado | Rechazado
    origen_offline        boolean NOT NULL DEFAULT false,
    hora_servidor_firma   timestamptz NULL,
    hora_dispositivo      timestamptz NULL,
    lat                   numeric(10,7) NULL,
    lng                   numeric(10,7) NULL,
    precision_metros      numeric(8,2) NULL,
    selfie_url            text NULL,
    selfie_hash           text NULL,
    firma_url             text NULL,
    firma_hash            text NULL,
    ip_origen             text NULL,
    user_agent            text NULL,
    declaracion_version   text NULL,            -- versión del texto "Declaración y compromiso"
    intentos              int NOT NULL DEFAULT 0,
    nota                  numeric(5,2) NULL,
    aprobado              boolean NOT NULL DEFAULT false,
    hash_verificacion     text NULL,            -- fijo al firmar, base del QR del acta
    validado_por_user_id  int NULL,
    validado_en           timestamptz NULL,
    rechazo_motivo        text NULL,
    created_at            timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_ss_ind_asistencia UNIQUE (sesion_id, worker_id)
);
CREATE INDEX IF NOT EXISTS ix_ss_ind_asistencia_worker ON ss_ind_asistencia (worker_id, estado);

-- Recibido por documento (reemplaza el "firma recibido" de SSO-FO-118/119).
CREATE TABLE IF NOT EXISTS ss_ind_asistencia_documento (
    id                   serial PRIMARY KEY,
    asistencia_id        int NOT NULL REFERENCES ss_ind_asistencia(id) ON DELETE CASCADE,
    documento_version_id int NOT NULL REFERENCES ss_ind_documento_version(id),
    recibido_en          timestamptz NOT NULL DEFAULT now(),
    segundos_abierto     int NULL,
    CONSTRAINT uq_ss_ind_asistencia_documento UNIQUE (asistencia_id, documento_version_id)
);

-- Cada intento de evaluación queda guardado (intentos ilimitados, nota mínima 14).
CREATE TABLE IF NOT EXISTS ss_ind_intento (
    id                    serial PRIMARY KEY,
    asistencia_id         int NOT NULL REFERENCES ss_ind_asistencia(id) ON DELETE CASCADE,
    evaluacion_version_id int NOT NULL REFERENCES ss_ind_evaluacion_version(id),
    respuestas_json       jsonb NOT NULL,
    nota                  numeric(5,2) NOT NULL,
    aprobado              boolean NOT NULL,
    iniciado_en           timestamptz NULL,
    creado_en             timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_ss_ind_intento_asistencia ON ss_ind_intento (asistencia_id);

-- ── Auditoría INSERT-ONLY con hash encadenado (por sesión) ──
CREATE TABLE IF NOT EXISTS ss_ind_audit_log (
    id            bigserial PRIMARY KEY,
    sesion_id     int NOT NULL REFERENCES ss_ind_sesion(id),
    evento        text NOT NULL,
    user_id       int NULL,
    ip_origen     text NULL,
    detalle       text NULL,
    created_at    timestamptz NOT NULL DEFAULT now(),
    hash_anterior text NULL,
    hash          text NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_ss_ind_audit_log_sesion ON ss_ind_audit_log (sesion_id, id);

-- ── Datos iniciales ──
-- Temario SSO-FO-031 (los 28 temas del formato vigente).
INSERT INTO ss_ind_tema (orden, descripcion)
SELECT v.orden, v.descripcion
FROM (VALUES
 (1,'Introducción sobre Abril Grupo Inmobiliario (La Organización)'),
 (2,'Política de Seguridad y Salud en el Trabajo de la Organización'),
 (3,'Descripción del Sistema de Gestión de Seguridad y Salud en el Trabajo de la Obra (Organigrama)'),
 (4,'Reglamento Interno de Seguridad y Salud en el Trabajo - RISST'),
 (5,'Derechos y Obligaciones de los Trabajadores (as) y Supervisores (as)'),
 (6,'Conceptos de Acto sub estandar, Condición sub estandar, Incidentes y Accidentes de Trabajo'),
 (7,'Conceptos Peligro y Riesgo (Prácticas seguras en el Proyecto)'),
 (8,'Tipos de Peligro (Físicos, Químicos, Biológicos, Disergonómicos, Psicosociales, Locativos, Mecánicos y eléctricos)'),
 (9,'Principales Enfermedades Ocupacionales (Sordera, Silicosis, Tendinitis, lumbalgias, etc)'),
 (10,'Reglas de Tránsito (Paso peatonal durante maniobras manuales y con maquinarias pesadas)'),
 (11,'Señalización de seguridad en obra (Códigos de colores establecidos)'),
 (12,'Reconocimiento y Control de Sustancias Peligrosas (Rombo NFPA 704, DOT, ONU, ETC)'),
 (13,'Procedimientos en Casos de Emergencia (Uso de rutas de evacuación)'),
 (14,'Equipos de protección personal (Conceptos, recomendaciones y uso en el proyecto)'),
 (15,'Análisis de Trabajo Seguro - ATS (Concepto, elaboración y uso en el Proyecto)'),
 (16,'Trabajos en Caliente (Concepto, Recomendaciones de seguridad, permisos de trabajo)'),
 (17,'Trabajos en Altura (Concepto, Recomendaciones de seguridad, permisos de trabajo)'),
 (18,'Trabajos en Espacios Confinados (Concepto, Recomendaciones de seguridad, permisos de trabajo)'),
 (19,'Uso de andamios (Concepto, Recomendaciones de seguridad, inspecciones y permisos de trabajo)'),
 (20,'Uso de escaleras portátiles (Concepto, Recomendaciones de seguridad e inspecciones)'),
 (21,'Herramientas eléctricas portátiles (Concepto, Recomendaciones de seguridad e inspecciones)'),
 (22,'Orden y limpieza (Importancia y recomendaciones)'),
 (23,'Mapa de Riesgos y Plano de Evacuación del Proyecto'),
 (24,'Efectos nocivos para la salud por la exposición prolongada a la radiación solar'),
 (25,'Difusión de la Política SIG'),
 (26,'Segregación y disposición de residuos sólidos peligrosos y no peligrosos'),
 (27,'Difusión de la matriz de aspectos e impactos y los controles'),
 (28,'3R: Reduce, Recicla, Reutiliza')
) AS v(orden, descripcion)
WHERE NOT EXISTS (SELECT 1 FROM ss_ind_tema);

-- Documentos de SSO-FO-118/119 (los archivos/versiones se suben desde el front).
INSERT INTO ss_ind_documento (codigo, nombre, tipo, orden)
VALUES
 ('SSO-DO-04',  'Reglamento Interno de Seguridad y Salud en el Trabajo', 'Legal', 1),
 ('SSO-DO-01',  'Recomendaciones de Seguridad y Salud en el Trabajo',   'Documento', 2),
 ('SSO-FO-089', 'IPERC de Línea Base',                                   'IPERC', 3),
 ('SSO-PETS',   'Procedimiento Escrito de Trabajo Seguro',               'Procedimiento', 4),
 ('SSO-FUNC',   'Funciones del Puesto',                                  'Documento', 5)
ON CONFLICT (codigo) DO NOTHING;

COMMIT;

-- Verificación (solo lectura):
-- SELECT count(*) FROM ss_ind_tema;                       -- 28
-- SELECT codigo, nombre FROM ss_ind_documento ORDER BY orden;
