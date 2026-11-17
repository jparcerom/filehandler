import os
import shutil
import hashlib
import time
from datetime import datetime

# ============================================================
# 🧠 Funciones auxiliares
# ============================================================

def calcular_hash(path, bloque=65536):
    """Calcula el hash SHA256 de un archivo."""
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while chunk := f.read(bloque):
            h.update(chunk)
    return h.hexdigest()

def generar_nombre_versionado(ruta):
    """Genera un nombre alternativo con sufijo incremental _1, _2, etc."""
    base, ext = os.path.splitext(ruta)
    n = 1
    nueva_ruta = f"{base}_{n}{ext}"
    while os.path.exists(nueva_ruta):
        n += 1
        nueva_ruta = f"{base}_{n}{ext}"
    return nueva_ruta

def escribir_log(path, texto, reintentos=3, espera=0.5):
    """Escribe en el log con reintentos en caso de bloqueo."""
    for i in range(reintentos):
        try:
            with open(path, "a", encoding="utf-8") as f:
                f.write(texto)
            break
        except PermissionError:
            if i < reintentos - 1:
                time.sleep(espera)
            else:
                raise

# ============================================================
# 🗂️ Función 1 - Organizar por fecha
# ============================================================

def organizar_por_fecha(origen, destino):
    meses = [
        "01-Enero", "02-Febrero", "03-Marzo", "04-Abril",
        "05-Mayo", "06-Junio", "07-Julio", "08-Agosto",
        "09-Septiembre", "10-Octubre", "11-Noviembre", "12-Diciembre"
    ]
    log_path = "filehandler.log"
    total_movidos = 0
    total_errores = 0

    escribir_log(log_path, f"\n[{datetime.now():%Y-%m-%d %H:%M:%S}] Organizando archivos por fecha...\n")

    for root, _, files in os.walk(origen):
        for nombre in files:
            try:
                archivo = os.path.join(root, nombre)
                timestamp = os.path.getmtime(archivo)
                fecha = datetime.fromtimestamp(timestamp)
                year = str(fecha.year)
                month_folder = meses[fecha.month - 1]
                dest_folder = os.path.join(destino, year, month_folder)
                os.makedirs(dest_folder, exist_ok=True)
                destino_final = os.path.join(dest_folder, nombre)
                if not os.path.exists(destino_final):
                    shutil.move(archivo, destino_final)
                    total_movidos += 1
                    escribir_log(log_path, f"[OK] {nombre} → {year}/{month_folder}\n")
                else:
                    total_errores += 1
                    escribir_log(log_path, f"[SKIP] Ya existe: {nombre}\n")
            except Exception as e:
                total_errores += 1
                escribir_log(log_path, f"[ERROR] {nombre}: {e}\n")

    escribir_log(log_path, f"Total movidos: {total_movidos} | Errores: {total_errores}\n")
    return f"Movidos: {total_movidos} | Errores: {total_errores}"

# ============================================================
# 🧩 Función 2 - Mover duplicados
# ============================================================

def mover_duplicados(origen, destino):
    log_path = "filehandler.log"
    total_movidos = 0
    total_omitidos = 0
    escribir_log(log_path, f"\n[{datetime.now():%Y-%m-%d %H:%M:%S}] Moviendo duplicados...\n")

    for root, _, files in os.walk(origen):
        for nombre in files:
            if "(" in nombre and ")" in nombre:
                archivo = os.path.join(root, nombre)
                os.makedirs(destino, exist_ok=True)
                try:
                    shutil.move(archivo, os.path.join(destino, nombre))
                    total_movidos += 1
                    escribir_log(log_path, f"[DUPLICADO] {nombre}\n")
                except Exception as e:
                    total_omitidos += 1
                    escribir_log(log_path, f"[ERROR] {nombre}: {e}\n")

    escribir_log(log_path, f"Duplicados movidos: {total_movidos} | Errores: {total_omitidos}\n")
    return f"Duplicados movidos: {total_movidos} | Errores: {total_omitidos}"

# ============================================================
# 🧬 Función 3 - Copiar archivos nuevos
# ============================================================

def copiar_archivos_nuevos(origen, destino):
    log_path = "filehandler.log"
    total_copiados = 0
    total_omitidos = 0
    total_versiones = 0
    escribir_log(log_path, f"\n[{datetime.now():%Y-%m-%d %H:%M:%S}] Copiando archivos nuevos o modificados...\n")

    for root, _, files in os.walk(origen):
        for nombre in files:
            origen_file = os.path.join(root, nombre)
            rel_path = os.path.relpath(origen_file, origen)
            destino_file = os.path.join(destino, rel_path)
            os.makedirs(os.path.dirname(destino_file), exist_ok=True)
            if not os.path.exists(destino_file):
                shutil.copy2(origen_file, destino_file)
                total_copiados += 1
                escribir_log(log_path, f"[NUEVO] {rel_path}\n")
            else:
                hash_origen = calcular_hash(origen_file)
                hash_destino = calcular_hash(destino_file)
                if hash_origen != hash_destino:
                    destino_versionado = generar_nombre_versionado(destino_file)
                    shutil.copy2(origen_file, destino_versionado)
                    total_versiones += 1
                    escribir_log(log_path, f"[VERSION] {rel_path} → {os.path.basename(destino_versionado)}\n")
                else:
                    total_omitidos += 1
                    escribir_log(log_path, f"[SIN CAMBIOS] {rel_path}\n")

    escribir_log(log_path, f"Nuevos: {total_copiados} | Versiones: {total_versiones} | Sin cambios: {total_omitidos}\n")
    return f"Nuevos: {total_copiados} | Versiones: {total_versiones} | Sin cambios: {total_omitidos}"

# ============================================================
# 🗃️ Función 4 - Mover desde subcarpetas
# ============================================================

def mover_desde_subcarpetas(origen, destino):
    log_path = "filehandler.log"
    total_movidos = 0
    total_errores = 0
    escribir_log(log_path, f"\n[{datetime.now():%Y-%m-%d %H:%M:%S}] Moviendo archivos desde subcarpetas...\n")

    for root, _, files in os.walk(origen):
        for nombre in files:
            archivo = os.path.join(root, nombre)
            os.makedirs(destino, exist_ok=True)
            try:
                shutil.move(archivo, os.path.join(destino, nombre))
                total_movidos += 1
                escribir_log(log_path, f"[MOVIDO] {nombre}\n")
            except Exception as e:
                total_errores += 1
                escribir_log(log_path, f"[ERROR] {nombre}: {e}\n")

    escribir_log(log_path, f"Total movidos: {total_movidos} | Errores: {total_errores}\n")
    return f"Movidos: {total_movidos} | Errores: {total_errores}"

# ============================================================
# 🧹 Función 5 - Limpiar logs
# ============================================================

def limpiar_logs():
    try:
        with open("filehandler.log", "w", encoding="utf-8") as f:
            f.write("")
        return "Logs limpiados correctamente."
    except Exception as e:
        return f"Error al limpiar logs: {e}"
