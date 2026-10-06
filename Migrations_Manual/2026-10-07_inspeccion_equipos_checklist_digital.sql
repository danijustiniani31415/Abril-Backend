-- ============================================================================
-- Check list digital de inspección pre-uso de equipos (SSO-FO-049 arnés,
-- SSO-FO-056 minicargador y los que vengan). NO confundir con ss_checklist_*
-- (checklists de avance por proyecto): este es un registro operativo por equipo.
--
-- Flujo (mismo criterio de personas que el ATS). Cada tipo define cómo se
-- identifica (ss_insp_equipo_tipo.identifica_por):
--   * 'trabajador' (arnés SSO-FO-049): UN check list POR PERSONA. QR fijo del
--     proyecto; la persona elige su nombre, marca los equipos que usa hoy
--     (arnés, línea de anclaje doble, tambor retráctil…) y escribe el código
--     de cada uno. Cada equipo trae SU imagen con marcadores numerados.
--   * 'equipo' (minicargador SSO-FO-056): QR pegado al equipo (qr_token).
--   En ambos la página es pública (sin login): el operador elige su nombre de
--   la lista de workers YA registrados en el proyecto, confirma los últimos
--   dígitos de su DNI y llena el check list (selfie + geolocalización + firma)
--   — igual que la adhesión del ATS. Obreros sin cuenta incluidos.
--   * Después firman por cadena, con cuenta/correo: Capataz (si el formato lo
--     pide y el operador es obrero), Residente / Ing. de Producción y
--     Prevencionista SSOMA.
--   * Un ítem CRÍTICO en "no cumple" y sin corregir bloquea el equipo
--     (estado = 'fuera_servicio') hasta que se registre la liberación
--     (ss_insp_equipo_evento) con evidencia.
--
-- Cada formato define su propia plantilla (ítems, modo de respuesta C/NC/NA o
-- SI/NO/NA, si lleva turno, columna "¿se corrigió?", etc.). Las respuestas
-- guardan SNAPSHOT del ítem: editar la plantilla después no altera
-- inspecciones ya firmadas.
--
-- Idempotente. Un solo entorno real de BD: revisar antes de correr.
-- ============================================================================

BEGIN;

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. CATÁLOGO: tipo de equipo = un formato (SSO-FO-xxx)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_insp_equipo_tipo (
    id                    SERIAL PRIMARY KEY,
    nombre                VARCHAR(150) NOT NULL,
    codigo_formato        VARCHAR(30)  NOT NULL UNIQUE,      -- 'SSO-FO-049'
    version               VARCHAR(10),
    fecha_version         DATE,
    elaboracion           VARCHAR(40),                       -- 'SSOMA'
    revision              VARCHAR(40),                       -- 'JSSOMA'
    aprobacion            VARCHAR(40),                       -- 'GP'

    -- 'C_NC_NA' (Cumple / No cumple / No aplica) | 'SI_NO_NA'
    -- 'trabajador' = un check list por persona, equipos escogidos por ella (QR del proyecto)
    -- 'equipo'     = el check list es de un equipo registrado (QR pegado al equipo)
    identifica_por        VARCHAR(10)  NOT NULL DEFAULT 'equipo'
                          CHECK (identifica_por IN ('trabajador','equipo')),
    modo_respuesta        VARCHAR(10)  NOT NULL DEFAULT 'C_NC_NA'
                          CHECK (modo_respuesta IN ('C_NC_NA','SI_NO_NA')),
    instrucciones         TEXT,                              -- texto impreso en el formato
    requiere_turno        BOOLEAN NOT NULL DEFAULT FALSE,    -- Día / Noche (arnés)
    registra_correccion   BOOLEAN NOT NULL DEFAULT FALSE,    -- columna "¿Se corrigió? SI/NO" (minicargador)
    requiere_planificada  BOOLEAN NOT NULL DEFAULT FALSE,    -- Planificada / No planificada (minicargador)
    requiere_empresa_tipo BOOLEAN NOT NULL DEFAULT FALSE,    -- Casa / Contratista (minicargador)
    permite_variantes     BOOLEAN NOT NULL DEFAULT FALSE,    -- "equipo a usar hoy": varias opciones marcables (arnés)

    -- Cadena de firmas que exige ESTE formato (el operador siempre firma)
    req_firma_capataz     BOOLEAN NOT NULL DEFAULT FALSE,
    req_firma_residente   BOOLEAN NOT NULL DEFAULT TRUE,     -- Ing. Producción / Residente
    req_firma_ssoma       BOOLEAN NOT NULL DEFAULT TRUE,     -- Prevencionista / Resp. SSOMA

    activo                BOOLEAN NOT NULL DEFAULT TRUE,
    created_at            TIMESTAMP NOT NULL DEFAULT now(),
    updated_at            TIMESTAMP NOT NULL DEFAULT now()
);

-- Opciones de "equipo a usar hoy" (arnés: línea de anclaje doble, tambor retráctil…)
CREATE TABLE IF NOT EXISTS ss_insp_equipo_tipo_variante (
    id        SERIAL PRIMARY KEY,
    tipo_id   INT NOT NULL REFERENCES ss_insp_equipo_tipo(id) ON DELETE CASCADE,
    nombre    VARCHAR(150) NOT NULL,
    -- Imagen LIMPIA (sin círculos numerados), subida desde la administración al
    -- storage; aquí solo la URL. Los números se dibujan encima con
    -- ss_insp_item_marcador, así reordenar ítems nunca desfasa la guía.
    imagen_url VARCHAR(500),
    orden     INT NOT NULL DEFAULT 0,
    activo    BOOLEAN NOT NULL DEFAULT TRUE,
    UNIQUE (tipo_id, nombre)
);

-- Ítems de la plantilla
CREATE TABLE IF NOT EXISTS ss_insp_plantilla_item (
    id            SERIAL PRIMARY KEY,
    tipo_id       INT NOT NULL REFERENCES ss_insp_equipo_tipo(id) ON DELETE CASCADE,
    -- NULL = ítem común a todos los equipos del tipo (los "GLOBAL": se agregan al
    -- final de cada equipo). Con valor = solo para esa variante (su propia imagen
    -- y su propia numeración correlativa 1..n).
    variante_id   INT REFERENCES ss_insp_equipo_tipo_variante(id) ON DELETE CASCADE,
    orden         INT NOT NULL DEFAULT 0,                    -- el número visible se calcula por este orden
    grupo         VARCHAR(150),                              -- "RIESGO" del arnés ('CAÍDA A DISTINTO NIVEL' / 'GLOBAL')
    nivel_riesgo  VARCHAR(10),                               -- "NR": 'STOP' (ícono rojo = no usar el equipo), 'M'…
    descripcion   TEXT NOT NULL,
    es_critico    BOOLEAN NOT NULL DEFAULT FALSE,            -- no cumple (sin corregir) => bloquea el equipo
    -- true cuando la pregunta está redactada en negativo ("¿Existen fugas?"): ahí
    -- "SI" es lo MALO. La pantalla muestra SI/NO tal cual el formato; el backend
    -- lo normaliza a cumple / no_cumple.
    invertido     BOOLEAN NOT NULL DEFAULT FALSE,
    activo        BOOLEAN NOT NULL DEFAULT TRUE,
    created_at    TIMESTAMP NOT NULL DEFAULT now(),
    updated_at    TIMESTAMP NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_ss_insp_plantilla_item_tipo ON ss_insp_plantilla_item(tipo_id, variante_id, orden);

-- Marcadores sobre la imagen de la variante: un ítem puede tener varios (en el
-- arnés los ganchos "5" y la línea "4" aparecen dos veces). Posición en % del
-- ancho/alto (independiente de la resolución). El número que se dibuja NO se
-- guarda: sale del orden actual del ítem dentro de su variante.
CREATE TABLE IF NOT EXISTS ss_insp_item_marcador (
    id       SERIAL PRIMARY KEY,
    item_id  INT NOT NULL REFERENCES ss_insp_plantilla_item(id) ON DELETE CASCADE,
    x_pct    NUMERIC(5,2) NOT NULL CHECK (x_pct BETWEEN 0 AND 100),
    y_pct    NUMERIC(5,2) NOT NULL CHECK (y_pct BETWEEN 0 AND 100)
);
CREATE INDEX IF NOT EXISTS ix_ss_insp_item_marcador_item ON ss_insp_item_marcador(item_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. EQUIPO FÍSICO (lo que lleva el QR pegado)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_insp_equipo (
    id                         SERIAL PRIMARY KEY,
    tipo_id                    INT NOT NULL REFERENCES ss_insp_equipo_tipo(id),
    proyecto_id                INT NOT NULL REFERENCES project(project_id),
    -- Qué equipo concreto es (arnés, tambor retráctil…) cuando el tipo tiene variantes.
    variante_id                INT REFERENCES ss_insp_equipo_tipo_variante(id),
    codigo                     VARCHAR(60) NOT NULL,         -- código/placa/serie visible en el equipo
    descripcion                VARCHAR(250),                 -- marca, modelo, color…
    -- Foto del equipo con su código visible: se pide UNA sola vez, al registrarlo
    -- (la primera vez que alguien escribe un código nuevo en la inspección).
    foto_url                   VARCHAR(500),
    -- Dueño/usuario habitual (equipos personales como el arnés): arma la lista
    -- "mis equipos" para no volver a escribir el código ni subir foto.
    asignado_worker_id         INT REFERENCES workers(id),
    -- Inspección con la que se auto-registró (null si lo creó SSOMA desde el catálogo).
    registrado_en_inspeccion_id INT,
    empresa_tipo               VARCHAR(12) CHECK (empresa_tipo IN ('casa','contratista')),
    empresa_nombre             VARCHAR(200),                 -- contratista dueño (texto libre acotado al equipo)
    qr_token                   UUID NOT NULL UNIQUE,         -- candado de la página pública de llenado

    -- 'operativo' | 'fuera_servicio' | 'baja'
    estado                     VARCHAR(20) NOT NULL DEFAULT 'operativo'
                               CHECK (estado IN ('operativo','fuera_servicio','baja')),
    bloqueado_motivo           TEXT,
    bloqueado_en               TIMESTAMP,
    bloqueado_inspeccion_id    INT,                          -- FK abajo (referencia circular)

    activo                     BOOLEAN NOT NULL DEFAULT TRUE,
    creado_por_user_id         INT REFERENCES app_user(user_id),
    created_at                 TIMESTAMP NOT NULL DEFAULT now(),
    updated_at                 TIMESTAMP NOT NULL DEFAULT now()
);
-- Un código por proyecto y tipo de equipo (el mismo "001" puede existir en un arnés y en un tambor)
CREATE UNIQUE INDEX IF NOT EXISTS ux_ss_insp_equipo_proyecto_tipo_codigo
    ON ss_insp_equipo(proyecto_id, tipo_id, COALESCE(variante_id, 0), codigo);
CREATE INDEX IF NOT EXISTS ix_ss_insp_equipo_asignado ON ss_insp_equipo(asignado_worker_id) WHERE asignado_worker_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_ss_insp_equipo_proyecto_estado ON ss_insp_equipo(proyecto_id, estado);

-- (El código de la inspección "{ABREV}-INSP-0001" usa ss_documento_correlativo con tipo 'INSP',
--  la misma tabla que ATS/PETAR: no hace falta una tabla nueva.)

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. INSPECCIÓN (una por equipo por uso/día/turno = una celda del "registro
--    diario" semanal del formato; el PDF semanal las agrupa)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_insp_inspeccion (
    id                      SERIAL PRIMARY KEY,
    codigo                  VARCHAR(40),
    equipo_id               INT REFERENCES ss_insp_equipo(id),   -- NULL cuando el tipo identifica_por 'trabajador' (los equipos van en ss_insp_inspeccion_variante)
    tipo_id                 INT NOT NULL REFERENCES ss_insp_equipo_tipo(id),
    proyecto_id             INT NOT NULL REFERENCES project(project_id),

    fecha                   DATE NOT NULL,
    turno                   VARCHAR(8) CHECK (turno IN ('dia','noche')),
    planificada             BOOLEAN,                         -- solo si el tipo lo pide
    empresa_tipo            VARCHAR(12) CHECK (empresa_tipo IN ('casa','contratista')),
    empresa_nombre          VARCHAR(200),

    -- 'Pendiente_Firmas' (operador firmó, faltan validaciones) | 'Completo' | 'Anulado'
    estado                  VARCHAR(20) NOT NULL DEFAULT 'Pendiente_Firmas'
                            CHECK (estado IN ('Pendiente_Firmas','Completo','Anulado')),
    -- 'conforme' | 'con_observaciones' (no cumple no crítico o corregido) | 'no_conforme_critico'
    resultado               VARCHAR(25) NOT NULL DEFAULT 'conforme'
                            CHECK (resultado IN ('conforme','con_observaciones','no_conforme_critico')),
    observaciones           TEXT,

    -- Operador (quien inspecciona). Worker del proyecto, sin necesidad de cuenta.
    operador_worker_id      INT NOT NULL REFERENCES workers(id),
    hora_servidor           TIMESTAMP NOT NULL DEFAULT now(),
    hora_dispositivo        TIMESTAMP,
    origen_offline          BOOLEAN NOT NULL DEFAULT FALSE,
    client_id               UUID UNIQUE,                     -- idempotencia del envío offline (PWA)
    lat                     NUMERIC(10,7),
    lng                     NUMERIC(10,7),
    precision_metros        NUMERIC(8,2),
    selfie_url              TEXT,
    selfie_hash             VARCHAR(80),
    firma_url               TEXT,
    firma_hash              VARCHAR(80),
    ip_origen               VARCHAR(60),
    user_agent              TEXT,

    -- Capataz / Maestro de obra (solo si el tipo lo pide y el operador es obrero)
    capataz_worker_id       INT REFERENCES workers(id),
    capataz_nombre          VARCHAR(200),
    capataz_cargo           VARCHAR(120),
    capataz_firma_url       TEXT,
    capataz_firma_hash      VARCHAR(80),
    capataz_hora_servidor   TIMESTAMP,

    -- Residente / Ingeniero de Producción
    residente_worker_id     INT REFERENCES workers(id),
    residente_nombre        VARCHAR(200),
    residente_cargo         VARCHAR(120),
    residente_firma_url     TEXT,
    residente_firma_hash    VARCHAR(80),
    residente_hora_servidor TIMESTAMP,

    -- Prevencionista / Responsable SSOMA
    ssoma_worker_id         INT REFERENCES workers(id),
    ssoma_nombre            VARCHAR(200),
    ssoma_cargo             VARCHAR(120),
    ssoma_firma_url         TEXT,
    ssoma_firma_hash        VARCHAR(80),
    ssoma_hora_servidor     TIMESTAMP,

    -- Anulación: nunca se borra
    anulado_motivo          TEXT,
    anulado_por_user_id     INT REFERENCES app_user(user_id),
    anulado_en              TIMESTAMP,

    pdf_url                 TEXT,
    pdf_hash                VARCHAR(80),

    created_at              TIMESTAMP NOT NULL DEFAULT now(),
    updated_at              TIMESTAMP NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_ss_insp_inspeccion_equipo_fecha ON ss_insp_inspeccion(equipo_id, fecha DESC);
CREATE INDEX IF NOT EXISTS ix_ss_insp_inspeccion_proyecto_fecha ON ss_insp_inspeccion(proyecto_id, fecha DESC);
-- Un solo registro vigente por equipo/fecha/turno (las anuladas no cuentan)
CREATE UNIQUE INDEX IF NOT EXISTS ux_ss_insp_inspeccion_equipo_fecha_turno
    ON ss_insp_inspeccion(equipo_id, fecha, COALESCE(turno, '-'))
    WHERE estado <> 'Anulado' AND equipo_id IS NOT NULL;
-- Un solo check list vigente por persona/tipo/fecha/turno (formatos por trabajador)
CREATE UNIQUE INDEX IF NOT EXISTS ux_ss_insp_inspeccion_operador_tipo_fecha_turno
    ON ss_insp_inspeccion(operador_worker_id, tipo_id, fecha, COALESCE(turno, '-'))
    WHERE estado <> 'Anulado' AND equipo_id IS NULL;

-- FKs circulares equipo -> inspección (la que lo bloqueó / la que lo registró)
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_ss_insp_equipo_registrado_inspeccion') THEN
        ALTER TABLE ss_insp_equipo
            ADD CONSTRAINT fk_ss_insp_equipo_registrado_inspeccion
            FOREIGN KEY (registrado_en_inspeccion_id) REFERENCES ss_insp_inspeccion(id);
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_ss_insp_equipo_bloqueado_inspeccion') THEN
        ALTER TABLE ss_insp_equipo
            ADD CONSTRAINT fk_ss_insp_equipo_bloqueado_inspeccion
            FOREIGN KEY (bloqueado_inspeccion_id) REFERENCES ss_insp_inspeccion(id);
    END IF;
END $$;

-- Equipos marcados por la persona ("equipo a usar hoy") con el código que escribió.
-- Cada uno lleva sus propias respuestas, con numeración correlativa propia.
CREATE TABLE IF NOT EXISTS ss_insp_inspeccion_variante (
    id             SERIAL PRIMARY KEY,
    inspeccion_id  INT NOT NULL REFERENCES ss_insp_inspeccion(id) ON DELETE CASCADE,
    variante_id    INT REFERENCES ss_insp_equipo_tipo_variante(id) ON DELETE SET NULL,
    nombre         VARCHAR(150) NOT NULL,                    -- snapshot
    imagen_url     VARCHAR(500),                             -- snapshot (el PDF no cambia si luego reemplazan la imagen)
    codigo_equipo  VARCHAR(60) NOT NULL,                     -- código impreso en el equipo (snapshot)
    equipo_id      INT REFERENCES ss_insp_equipo(id),        -- siempre se enlaza: si el código era nuevo, se registró en ese momento con su foto
    orden          INT NOT NULL DEFAULT 0
);

-- Respuestas por ítem (snapshot del ítem)
CREATE TABLE IF NOT EXISTS ss_insp_respuesta (
    id             SERIAL PRIMARY KEY,
    inspeccion_id  INT NOT NULL REFERENCES ss_insp_inspeccion(id) ON DELETE CASCADE,
    inspeccion_variante_id INT REFERENCES ss_insp_inspeccion_variante(id) ON DELETE CASCADE,
    item_id        INT REFERENCES ss_insp_plantilla_item(id) ON DELETE SET NULL,
    numero         INT NOT NULL DEFAULT 0,                   -- número correlativo mostrado dentro de su equipo (snapshot)
    orden          INT NOT NULL,                             -- orden global dentro de la inspección
    grupo          VARCHAR(150),
    nivel_riesgo   VARCHAR(2),
    descripcion    TEXT NOT NULL,
    es_critico     BOOLEAN NOT NULL DEFAULT FALSE,
    invertido      BOOLEAN NOT NULL DEFAULT FALSE,
    -- Resultado normalizado, ya considerando "invertido": cumple | no_cumple | na
    respuesta      VARCHAR(10) NOT NULL CHECK (respuesta IN ('cumple','no_cumple','na')),
    observacion    TEXT,
    se_corrigio    BOOLEAN,                                  -- solo si el tipo registra_correccion y no_cumple
    foto_url       TEXT,
    UNIQUE (inspeccion_id, orden)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. EVENTOS DEL EQUIPO: bloqueo / liberación / baja (+ evidencia de la corrección)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_insp_equipo_evento (
    id             SERIAL PRIMARY KEY,
    equipo_id      INT NOT NULL REFERENCES ss_insp_equipo(id),
    -- 'bloqueo' (automático al inspeccionar) | 'liberacion' | 'baja' | 'reactivacion'
    evento         VARCHAR(20) NOT NULL CHECK (evento IN ('bloqueo','liberacion','baja','reactivacion')),
    inspeccion_id  INT REFERENCES ss_insp_inspeccion(id),
    user_id        INT REFERENCES app_user(user_id),         -- null en el bloqueo automático
    worker_id      INT REFERENCES workers(id),
    detalle        TEXT,                                     -- qué se corrigió / por qué se da de baja
    foto_url       TEXT,
    created_at     TIMESTAMP NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_ss_insp_equipo_evento_equipo ON ss_insp_equipo_evento(equipo_id, created_at DESC);

-- ─────────────────────────────────────────────────────────────────────────────
-- 5. AUDITORÍA insert-only con hash encadenado (mismo esquema que ss_ats_audit_log)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_insp_audit_log (
    id             BIGSERIAL PRIMARY KEY,
    inspeccion_id  INT NOT NULL REFERENCES ss_insp_inspeccion(id),
    evento         VARCHAR(40) NOT NULL,
    user_id        INT,
    ip_origen      VARCHAR(60),
    detalle        TEXT,
    created_at     TIMESTAMP NOT NULL DEFAULT now(),
    hash_anterior  VARCHAR(80),
    hash           VARCHAR(80) NOT NULL
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 6. SEED: SSO-FO-049 (arnés) y SSO-FO-056 (minicargador)
--    Se inserta el tipo solo si no existe y sus ítems solo si el tipo no tiene.
-- ─────────────────────────────────────────────────────────────────────────────

-- 6.1 Arnés / equipo de protección contra caídas
INSERT INTO ss_insp_equipo_tipo
    (nombre, codigo_formato, version, elaboracion, revision, aprobacion, modo_respuesta, instrucciones,
     identifica_por, requiere_turno, permite_variantes, req_firma_capataz, req_firma_residente, req_firma_ssoma)
VALUES
    ('Equipo de protección contra caídas (arnés)', 'SSO-FO-049', '02', 'SSOMA', 'JSSOMA', 'GP', 'C_NC_NA',
     'Realizar esta inspección cada vez que se utilice un equipo de protección contra caídas, para mantener apropiadamente la vida y desempeño. Marque según corresponda el estado del equipo: C = Cumple / buen estado, NC = No cumple / mal estado, NA = No aplica.',
     'trabajador', TRUE, TRUE, FALSE, TRUE, TRUE)
ON CONFLICT (codigo_formato) DO NOTHING;

UPDATE ss_insp_equipo_tipo SET fecha_version = DATE '2026-12-01'
WHERE codigo_formato = 'SSO-FO-049' AND fecha_version IS NULL;  -- el formato dice "Fecha: 01/12/2026"

INSERT INTO ss_insp_equipo_tipo_variante (tipo_id, nombre, orden)
SELECT t.id, v.nombre, v.orden
FROM ss_insp_equipo_tipo t
CROSS JOIN (VALUES
    ('Arnés de seguridad', 1),
    ('Línea de anclaje doble', 2),
    ('Línea de restricción', 3),
    ('Línea de posicionamiento', 4),
    ('Tambor retráctil', 5)
) AS v(nombre, orden)
WHERE t.codigo_formato = 'SSO-FO-049'
ON CONFLICT (tipo_id, nombre) DO NOTHING;

-- Ítems específicos por equipo (numeración correlativa propia de cada imagen).
INSERT INTO ss_insp_plantilla_item (tipo_id, variante_id, orden, grupo, nivel_riesgo, descripcion, es_critico)
SELECT t.id, v.id, i.orden, 'CAÍDA A DISTINTO NIVEL', 'STOP', i.descripcion, TRUE
FROM ss_insp_equipo_tipo t
JOIN ss_insp_equipo_tipo_variante v ON v.tipo_id = t.id
JOIN (VALUES
    ('Arnés de seguridad', 1, 'Cintas y bandas sin hebras descocidas'),
    ('Arnés de seguridad', 2, 'Anillos en D y anillos no doblados sin fisuras'),
    ('Arnés de seguridad', 3, 'Hebillas no dobladas sin fisuras'),
    ('Línea de anclaje doble', 1, 'Ganchos de anclaje o seguridad no doblados sin fisuras'),
    ('Línea de anclaje doble', 2, 'Absorbedor de impacto no roto'),
    ('Línea de anclaje doble', 3, 'Línea de seguridad sin hebras descocidas o alambres rotos'),
    ('Línea de restricción', 1, 'Hebillas no dobladas sin fisuras'),
    ('Línea de restricción', 2, 'Línea de seguridad sin hebras descocidas o alambres rotos'),
    ('Línea de restricción', 3, 'Ganchos de anclaje o seguridad no doblados sin fisuras'),
    ('Línea de posicionamiento', 1, 'Ganchos de anclaje o seguridad no doblados sin fisuras'),
    ('Línea de posicionamiento', 2, 'Línea de seguridad sin hebras descocidas o alambres rotos'),
    ('Tambor retráctil', 1, 'Manija de anclaje no doblada sin fisuras'),
    ('Tambor retráctil', 2, 'Línea de seguridad sin hebras descocidas o alambres rotos'),
    ('Tambor retráctil', 3, 'Ganchos de anclaje o seguridad no doblados sin fisuras'),
    ('Tambor retráctil', 4, 'Carcaza sin fisuras')
) AS i(variante, orden, descripcion) ON i.variante = v.nombre
WHERE t.codigo_formato = 'SSO-FO-049'
  AND NOT EXISTS (SELECT 1 FROM ss_insp_plantilla_item x WHERE x.tipo_id = t.id);

-- Ítems comunes (GLOBAL, variante_id NULL): se agregan al final de cada equipo.
INSERT INTO ss_insp_plantilla_item (tipo_id, variante_id, orden, grupo, nivel_riesgo, descripcion, es_critico)
SELECT t.id, NULL, i.orden, 'GLOBAL', 'M', i.descripcion, FALSE
FROM ss_insp_equipo_tipo t
CROSS JOIN (VALUES
    (100, 'Código de identificación visible y legible'),
    (101, 'Equipo de sistema anticaídas protegido de condiciones dañinas'),
    (102, 'Cinta de inspección del mes correspondiente')
) AS i(orden, descripcion)
WHERE t.codigo_formato = 'SSO-FO-049'
  AND NOT EXISTS (SELECT 1 FROM ss_insp_plantilla_item x WHERE x.tipo_id = t.id AND x.variante_id IS NULL);

-- 6.2 Minicargador
INSERT INTO ss_insp_equipo_tipo
    (nombre, codigo_formato, version, fecha_version, elaboracion, revision, aprobacion, modo_respuesta,
     registra_correccion, requiere_planificada, requiere_empresa_tipo, req_firma_capataz, req_firma_residente, req_firma_ssoma)
VALUES
    ('Minicargador', 'SSO-FO-056', '01', DATE '2023-12-01', 'SSOMA', 'JSSOMA', 'GP', 'SI_NO_NA',
     TRUE, TRUE, TRUE, FALSE, TRUE, TRUE)
ON CONFLICT (codigo_formato) DO NOTHING;

INSERT INTO ss_insp_plantilla_item (tipo_id, orden, descripcion, es_critico, invertido)
SELECT t.id, i.orden, i.descripcion, i.critico, i.inv
FROM ss_insp_equipo_tipo t
CROSS JOIN (VALUES
    (1,  '¿Existen fugas de combustible?', TRUE, TRUE),
    (2,  '¿La batería está limpia, cargada y en buen estado?', FALSE, FALSE),
    (3,  '¿Nivel de agua de la batería?', FALSE, FALSE),
    (4,  '¿Las cuchillas del minicargador están en buen estado?', FALSE, FALSE),
    (5,  '¿El mecanismo elevador con o sin carga está en buen estado?', TRUE, FALSE),
    (6,  '¿La dirección funciona correctamente?', TRUE, FALSE),
    (7,  '¿Los frenos se encuentran en buen estado?', TRUE, FALSE),
    (8,  '¿Nivel del líquido de frenos está lleno?', TRUE, FALSE),
    (9,  '¿Nivel del aceite hidráulico lleno?', FALSE, FALSE),
    (10, '¿Al verificar, todas las luces funcionan adecuadamente?', FALSE, FALSE),
    (11, '¿Cuenta con extintor tipo PQS mínimo de 4 kg?', FALSE, FALSE),
    (12, '¿Tiene seguro y carga vigente el extintor?', FALSE, FALSE),
    (13, '¿Las luces intermitentes y la torreta funcionan adecuadamente?', FALSE, FALSE),
    (14, '¿Cuenta con espejo retrovisor en buen estado?', FALSE, FALSE),
    (15, '¿Las llantas están en buen estado y no presentan desgaste?', FALSE, FALSE),
    (16, '¿El asiento del operador está en buen estado?', FALSE, FALSE),
    (17, '¿Cuenta con cinturón o sistema de seguridad para el operador?', TRUE, FALSE),
    (18, '¿Funciona el claxon?', FALSE, FALSE),
    (19, '¿El pitillo de alarma de reversa funciona adecuadamente?', TRUE, FALSE),
    (20, '¿La rejilla de protección está en buen estado?', FALSE, FALSE),
    (21, '¿Presenta golpes o abolladuras que presenten dificultades al operar?', FALSE, TRUE),
    (22, '¿El minicargador emana demasiado humo o monóxido en el área de trabajo?', FALSE, TRUE),
    (23, 'Otros (especificar en observación)', FALSE, FALSE)
) AS i(orden, descripcion, critico, inv)
WHERE t.codigo_formato = 'SSO-FO-056'
  AND NOT EXISTS (SELECT 1 FROM ss_insp_plantilla_item x WHERE x.tipo_id = t.id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 6.3 Tipos de notificación (campanita). Sin estas filas los avisos no se crean.
--     Mismo patrón que ATS_PENDIENTE_FIRMA.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO notificacion_tipo (codigo, nombre, orden, created_date_time, active, state)
VALUES ('INSPECCION_EQUIPO_PENDIENTE_FIRMA', 'Check list de equipo pendiente de tu firma', 52, now(), true, true)
ON CONFLICT (codigo) WHERE (state = true)
DO UPDATE SET nombre = EXCLUDED.nombre, orden = EXCLUDED.orden, active = true, updated_date_time = now();

INSERT INTO notificacion_tipo (codigo, nombre, orden, created_date_time, active, state)
VALUES ('INSPECCION_EQUIPO_BLOQUEADO', 'Equipo fuera de servicio por inspección', 53, now(), true, true)
ON CONFLICT (codigo) WHERE (state = true)
DO UPDATE SET nombre = EXCLUDED.nombre, orden = EXCLUDED.orden, active = true, updated_date_time = now();

-- ─────────────────────────────────────────────────────────────────────────────
-- 7. FEATURE (acceso a la gestión: catálogo, equipos, listado, liberaciones).
--    Las rutas públicas por QR serán [AllowAnonymous] y no necesitan permiso.
--    ADMINISTRADOR DEL SISTEMA (1), JEFE SSOMA (9), COORDINADOR SSOMA (70).
--    Después: cerrar sesión y volver a entrar (allowed_features).
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.inspeccion-equipos', m.module_id
FROM module m
WHERE m.module_name = 'SSOMA'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.inspeccion-equipos');

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE f.feature_key = 'ssoma.gestion.inspeccion-equipos'
  AND r.role_id::text IN ('1', '9', '70')
  AND NOT EXISTS (
    SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

-- Residentes, capataces, prevencionistas… firman la cadena: reciben la feature todos los roles
-- que ya tienen ATS (misma población). Las acciones de administración (catálogo, equipos,
-- liberar, baja, anular) las valida el backend por rol: solo 1, 9, 70 y 72.
INSERT INTO role_feature (role_id, feature_id)
SELECT DISTINCT rf_ats.role_id, f_new.feature_id
FROM role_feature rf_ats
JOIN feature f_ats ON f_ats.feature_id = rf_ats.feature_id AND f_ats.feature_key = 'ssoma.gestion.ats'
CROSS JOIN feature f_new
WHERE f_new.feature_key = 'ssoma.gestion.inspeccion-equipos'
  AND NOT EXISTS (
    SELECT 1 FROM role_feature rf WHERE rf.role_id = rf_ats.role_id AND rf.feature_id = f_new.feature_id
  );

COMMIT;

-- ============================================================================
-- Verificación (solo lectura)
-- ============================================================================
-- SELECT t.codigo_formato, t.nombre, count(i.*) AS items
-- FROM ss_insp_equipo_tipo t LEFT JOIN ss_insp_plantilla_item i ON i.tipo_id = t.id
-- GROUP BY 1, 2;                                  -- esperado: 049 → 18 ítems (15 por equipo + 3 comunes), 056 → 23 ítems
-- SELECT * FROM ss_insp_equipo_tipo_variante;      -- esperado: 5 filas (arnés)
-- SELECT feature_key FROM feature WHERE feature_key = 'ssoma.gestion.inspeccion-equipos';
-- SELECT r.role_id, r.role_description FROM role_feature rf JOIN feature f ON f.feature_id = rf.feature_id JOIN role r ON r.role_id = rf.role_id WHERE f.feature_key = 'ssoma.gestion.inspeccion-equipos';  -- los roles de ATS + 1, 9, 70
-- SELECT codigo FROM notificacion_tipo WHERE codigo LIKE 'INSPECCION_EQUIPO%';   -- esperado: 2 filas
