-- ============================================================================
-- INDUCCIÓN DIGITAL — Fase 2: candados en la base de datos.
--
-- ⚠ CORRER SOLO CUANDO TERMINEN LAS PRUEBAS. Esta base es única (prueba y producción) y, una vez puestos los
--   candados, las sesiones cerradas, el log de auditoría, las versiones de documentos y los sellos NO se pueden
--   editar ni borrar — ni siquiera con un DELETE a mano. Si hay sesiones de prueba, bórralas ANTES (el orden está
--   en la sección «Limpieza de pruebas» al final) o quedarán para siempre.
--
-- Qué protege (la regla de oro: lo firmado queda congelado):
--   ss_ind_audit_log          solo INSERT                       (cadena de hash que no se puede reescribir)
--   ss_ind_documento_version  no DELETE; solo puede cambiar «vigente»
--   ss_ind_evaluacion_version no DELETE; solo puede cambiar «vigente»
--   ss_ind_sesion             no DELETE; Cerrada solo puede pasar a Anulada; Anulada no cambia
--   hijas de una sesión       solo se escriben mientras la sesión está Abierta
--   ss_ind_sello              no DELETE; un sello «Sellado» no cambia
--   ss_ind_respaldo           no DELETE
--
-- Idempotente. Rollback al final (comentado).
-- ============================================================================

BEGIN;

-- ── 1) Auditoría: solo inserción ────────────────────────────────────────────
CREATE OR REPLACE FUNCTION fn_ss_ind_solo_insercion() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION '% es de solo inserción (no se permite % )', TG_TABLE_NAME, TG_OP USING ERRCODE = 'integrity_constraint_violation';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tg_ss_ind_audit_log_inmutable ON ss_ind_audit_log;
CREATE TRIGGER tg_ss_ind_audit_log_inmutable
    BEFORE UPDATE OR DELETE ON ss_ind_audit_log
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_solo_insercion();

-- ── 2) Nada se borra ────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION fn_ss_ind_no_borrar() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION '% no admite borrado: se anula o se versiona, nunca se elimina', TG_TABLE_NAME USING ERRCODE = 'integrity_constraint_violation';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tg_ss_ind_documento_version_no_borrar ON ss_ind_documento_version;
CREATE TRIGGER tg_ss_ind_documento_version_no_borrar BEFORE DELETE ON ss_ind_documento_version
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_no_borrar();

DROP TRIGGER IF EXISTS tg_ss_ind_evaluacion_version_no_borrar ON ss_ind_evaluacion_version;
CREATE TRIGGER tg_ss_ind_evaluacion_version_no_borrar BEFORE DELETE ON ss_ind_evaluacion_version
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_no_borrar();

DROP TRIGGER IF EXISTS tg_ss_ind_sesion_no_borrar ON ss_ind_sesion;
CREATE TRIGGER tg_ss_ind_sesion_no_borrar BEFORE DELETE ON ss_ind_sesion
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_no_borrar();

DROP TRIGGER IF EXISTS tg_ss_ind_sello_no_borrar ON ss_ind_sello;
CREATE TRIGGER tg_ss_ind_sello_no_borrar BEFORE DELETE ON ss_ind_sello
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_no_borrar();

DROP TRIGGER IF EXISTS tg_ss_ind_respaldo_no_borrar ON ss_ind_respaldo;
CREATE TRIGGER tg_ss_ind_respaldo_no_borrar BEFORE DELETE ON ss_ind_respaldo
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_no_borrar();

-- ── 3) Versiones: el contenido publicado no cambia (solo la marca «vigente») ─
CREATE OR REPLACE FUNCTION fn_ss_ind_documento_version_inmutable() RETURNS trigger AS $$
BEGIN
    IF NEW.documento_id <> OLD.documento_id OR NEW.version <> OLD.version
       OR NEW.archivo_url <> OLD.archivo_url OR NEW.archivo_hash <> OLD.archivo_hash
       OR NEW.es_cambio_material <> OLD.es_cambio_material
       OR NEW.nota_cambio IS DISTINCT FROM OLD.nota_cambio
       OR NEW.publicado_en <> OLD.publicado_en THEN
        RAISE EXCEPTION 'Una versión de documento publicada no se modifica: sube una versión nueva' USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tg_ss_ind_documento_version_inmutable ON ss_ind_documento_version;
CREATE TRIGGER tg_ss_ind_documento_version_inmutable BEFORE UPDATE ON ss_ind_documento_version
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_documento_version_inmutable();

CREATE OR REPLACE FUNCTION fn_ss_ind_evaluacion_version_inmutable() RETURNS trigger AS $$
BEGIN
    IF NEW.version <> OLD.version OR NEW.nota_minima <> OLD.nota_minima OR NEW.nota_maxima <> OLD.nota_maxima
       OR NEW.duracion_min <> OLD.duracion_min OR NEW.contenido_json::text <> OLD.contenido_json::text
       OR NEW.contenido_hash <> OLD.contenido_hash OR NEW.publicado_en <> OLD.publicado_en THEN
        RAISE EXCEPTION 'Una evaluación publicada no se modifica: publica una versión nueva' USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tg_ss_ind_evaluacion_version_inmutable ON ss_ind_evaluacion_version;
CREATE TRIGGER tg_ss_ind_evaluacion_version_inmutable BEFORE UPDATE ON ss_ind_evaluacion_version
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_evaluacion_version_inmutable();

-- ── 4) Sesión: Cerrada solo puede pasar a Anulada; Anulada no cambia ────────
CREATE OR REPLACE FUNCTION fn_ss_ind_sesion_inmutable() RETURNS trigger AS $$
BEGIN
    IF OLD.estado = 'Anulada' THEN
        RAISE EXCEPTION 'La sesión % está anulada y no se puede modificar', OLD.id USING ERRCODE = 'integrity_constraint_violation';
    END IF;

    IF OLD.estado = 'Cerrada' THEN
        IF NEW.estado <> 'Anulada'
           OR NEW.proyecto_id <> OLD.proyecto_id OR NEW.fecha <> OLD.fecha
           OR NEW.codigo IS DISTINCT FROM OLD.codigo
           OR NEW.evaluacion_version_id <> OLD.evaluacion_version_id
           OR NEW.instructor_firma_hash IS DISTINCT FROM OLD.instructor_firma_hash
           OR NEW.instructor_firma_url IS DISTINCT FROM OLD.instructor_firma_url
           OR NEW.cerrada_en IS DISTINCT FROM OLD.cerrada_en
           OR NEW.acta_pdf_url IS DISTINCT FROM OLD.acta_pdf_url
           OR NEW.acta_pdf_hash IS DISTINCT FROM OLD.acta_pdf_hash
           OR NEW.acta_pdf_sha256 IS DISTINCT FROM OLD.acta_pdf_sha256 THEN
            RAISE EXCEPTION 'La sesión % ya está cerrada: lo único que admite es anularse (con motivo)', OLD.id USING ERRCODE = 'integrity_constraint_violation';
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tg_ss_ind_sesion_inmutable ON ss_ind_sesion;
CREATE TRIGGER tg_ss_ind_sesion_inmutable BEFORE UPDATE ON ss_ind_sesion
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_sesion_inmutable();

-- ── 5) Hijas de la sesión: solo se escriben mientras está Abierta ───────────
CREATE OR REPLACE FUNCTION fn_ss_ind_hija_solo_abierta() RETURNS trigger AS $$
DECLARE
    v_sesion_id int;
    v_estado    text;
BEGIN
    IF TG_TABLE_NAME IN ('ss_ind_sesion_documento', 'ss_ind_sesion_tema', 'ss_ind_asistencia') THEN
        v_sesion_id := CASE WHEN TG_OP = 'DELETE' THEN OLD.sesion_id ELSE NEW.sesion_id END;
    ELSE  -- ss_ind_asistencia_documento, ss_ind_intento: cuelgan de la asistencia
        SELECT a.sesion_id INTO v_sesion_id FROM ss_ind_asistencia a
        WHERE a.id = CASE WHEN TG_OP = 'DELETE' THEN OLD.asistencia_id ELSE NEW.asistencia_id END;
    END IF;

    SELECT estado INTO v_estado FROM ss_ind_sesion WHERE id = v_sesion_id;
    IF v_estado IS DISTINCT FROM 'Abierta' THEN
        RAISE EXCEPTION '% no se puede modificar: la sesión % está %', TG_TABLE_NAME, v_sesion_id, COALESCE(v_estado, 'inexistente')
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tg_ss_ind_sesion_documento_abierta ON ss_ind_sesion_documento;
CREATE TRIGGER tg_ss_ind_sesion_documento_abierta BEFORE INSERT OR UPDATE OR DELETE ON ss_ind_sesion_documento
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_hija_solo_abierta();

DROP TRIGGER IF EXISTS tg_ss_ind_sesion_tema_abierta ON ss_ind_sesion_tema;
CREATE TRIGGER tg_ss_ind_sesion_tema_abierta BEFORE INSERT OR UPDATE OR DELETE ON ss_ind_sesion_tema
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_hija_solo_abierta();

DROP TRIGGER IF EXISTS tg_ss_ind_asistencia_abierta ON ss_ind_asistencia;
CREATE TRIGGER tg_ss_ind_asistencia_abierta BEFORE INSERT OR UPDATE OR DELETE ON ss_ind_asistencia
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_hija_solo_abierta();

DROP TRIGGER IF EXISTS tg_ss_ind_asistencia_documento_abierta ON ss_ind_asistencia_documento;
CREATE TRIGGER tg_ss_ind_asistencia_documento_abierta BEFORE INSERT OR UPDATE OR DELETE ON ss_ind_asistencia_documento
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_hija_solo_abierta();

DROP TRIGGER IF EXISTS tg_ss_ind_intento_abierta ON ss_ind_intento;
CREATE TRIGGER tg_ss_ind_intento_abierta BEFORE INSERT OR UPDATE OR DELETE ON ss_ind_intento
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_hija_solo_abierta();

-- ── 6) Sello: lo ya sellado no cambia ───────────────────────────────────────
CREATE OR REPLACE FUNCTION fn_ss_ind_sello_inmutable() RETURNS trigger AS $$
BEGIN
    IF OLD.estado = 'Sellado' THEN
        RAISE EXCEPTION 'El sello de tiempo de la sesión % ya está emitido y no se modifica', OLD.sesion_id USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    IF NEW.digest <> OLD.digest OR NEW.componentes <> OLD.componentes OR NEW.sesion_id <> OLD.sesion_id THEN
        RAISE EXCEPTION 'El digest a sellar no puede cambiar' USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tg_ss_ind_sello_inmutable ON ss_ind_sello;
CREATE TRIGGER tg_ss_ind_sello_inmutable BEFORE UPDATE ON ss_ind_sello
    FOR EACH ROW EXECUTE FUNCTION fn_ss_ind_sello_inmutable();

COMMIT;

-- ============================================================================
-- Verificación (solo lectura): deben salir 15 triggers.
-- ============================================================================
-- SELECT event_object_table, trigger_name FROM information_schema.triggers
-- WHERE trigger_name LIKE 'tg_ss_ind_%' GROUP BY event_object_table, trigger_name ORDER BY 1, 2;

-- ============================================================================
-- Limpieza de pruebas (CORRER ANTES de este script si hay sesiones de prueba; con los candados puestos ya no se puede).
-- Revisa con el SELECT qué vas a borrar. Orden: de las hijas hacia la sesión.
-- ============================================================================
-- SELECT id, codigo, estado, fecha FROM ss_ind_sesion ORDER BY id;
-- DELETE FROM ss_ind_audit_log WHERE sesion_id IN (<ids de prueba>);
-- DELETE FROM ss_ind_intento WHERE asistencia_id IN (SELECT id FROM ss_ind_asistencia WHERE sesion_id IN (<ids>));
-- DELETE FROM ss_ind_asistencia_documento WHERE asistencia_id IN (SELECT id FROM ss_ind_asistencia WHERE sesion_id IN (<ids>));
-- DELETE FROM ss_ind_asistencia WHERE sesion_id IN (<ids>);
-- DELETE FROM ss_ind_sesion_tema WHERE sesion_id IN (<ids>);
-- DELETE FROM ss_ind_sesion_documento WHERE sesion_id IN (<ids>);
-- DELETE FROM ss_ind_sello WHERE sesion_id IN (<ids>);
-- DELETE FROM ss_ind_respaldo WHERE sesion_id IN (<ids>);
-- DELETE FROM ss_ind_sesion WHERE id IN (<ids>);

-- ============================================================================
-- ROLLBACK (si algo bloquea un flujo legítimo; quita todos los candados):
-- ============================================================================
-- DROP TRIGGER IF EXISTS tg_ss_ind_audit_log_inmutable ON ss_ind_audit_log;
-- DROP TRIGGER IF EXISTS tg_ss_ind_documento_version_no_borrar ON ss_ind_documento_version;
-- DROP TRIGGER IF EXISTS tg_ss_ind_evaluacion_version_no_borrar ON ss_ind_evaluacion_version;
-- DROP TRIGGER IF EXISTS tg_ss_ind_sesion_no_borrar ON ss_ind_sesion;
-- DROP TRIGGER IF EXISTS tg_ss_ind_sello_no_borrar ON ss_ind_sello;
-- DROP TRIGGER IF EXISTS tg_ss_ind_respaldo_no_borrar ON ss_ind_respaldo;
-- DROP TRIGGER IF EXISTS tg_ss_ind_documento_version_inmutable ON ss_ind_documento_version;
-- DROP TRIGGER IF EXISTS tg_ss_ind_evaluacion_version_inmutable ON ss_ind_evaluacion_version;
-- DROP TRIGGER IF EXISTS tg_ss_ind_sesion_inmutable ON ss_ind_sesion;
-- DROP TRIGGER IF EXISTS tg_ss_ind_sesion_documento_abierta ON ss_ind_sesion_documento;
-- DROP TRIGGER IF EXISTS tg_ss_ind_sesion_tema_abierta ON ss_ind_sesion_tema;
-- DROP TRIGGER IF EXISTS tg_ss_ind_asistencia_abierta ON ss_ind_asistencia;
-- DROP TRIGGER IF EXISTS tg_ss_ind_asistencia_documento_abierta ON ss_ind_asistencia_documento;
-- DROP TRIGGER IF EXISTS tg_ss_ind_intento_abierta ON ss_ind_intento;
-- DROP TRIGGER IF EXISTS tg_ss_ind_sello_inmutable ON ss_ind_sello;
