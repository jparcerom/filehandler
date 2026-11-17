# -*- coding: utf-8 -*-
import customtkinter as ctk
from tkinter import filedialog, messagebox, scrolledtext, Tk, Toplevel, Label, Frame
import threading
import os
import time
import shutil
import hashlib
from datetime import datetime
from pathlib import Path
import logging

ctk.set_appearance_mode("system")
ctk.set_default_color_theme("blue")

# ============================================================
# SPLASH SCREEN
# ============================================================
class SplashScreen:
    def __init__(self):
        self.splash = Tk()
        self.splash.title("FileHandler")
        self.splash.geometry("400x300")
        self.splash.resizable(False, False)
        
        # Centrar en pantalla
        self.center_window()
        
        # Configurar para que no tenga bordes de ventana
        self.splash.overrideredirect(True)
        
        # Frame principal
        main_frame = Frame(self.splash, bg="white")
        main_frame.pack(expand=True, fill="both", padx=20, pady=20)
        
        # Logo/Icono
        logo_label = Label(
            main_frame,
            text="📁",
            font=("Segoe UI", 48),
            fg="#2E86AB",
            bg="white"
        )
        logo_label.pack(pady=(20, 10))
        
        # Nombre de la aplicación
        title_label = Label(
            main_frame,
            text="FileHandler",
            font=("Segoe UI", 28, "bold"),
            fg="#2E86AB",
            bg="white"
        )
        title_label.pack(pady=(0, 5))
        
        # Versión
        version_label = Label(
            main_frame,
            text="v1.0.0",
            font=("Segoe UI", 14),
            fg="#666666",
            bg="white"
        )
        version_label.pack(pady=(0, 30))
        
        # Barra de progreso (simulada con texto)
        self.progress_label = Label(
            main_frame,
            text="█" * 0 + "░" * 20,
            font=("Consolas", 12),
            fg="#2E86AB",
            bg="white"
        )
        self.progress_label.pack(pady=(0, 20))
        
        # Texto de estado
        self.status_label = Label(
            main_frame,
            text="Iniciando aplicación...",
            font=("Segoe UI", 12),
            fg="#666666",
            bg="white"
        )
        self.status_label.pack()
        
        # Iniciar animación en hilo separado
        self.start_loading()
    
    def center_window(self):
        """Centra la ventana en la pantalla"""
        self.splash.update_idletasks()
        width = self.splash.winfo_width()
        height = self.splash.winfo_height()
        x = (self.splash.winfo_screenwidth() // 2) - (width // 2)
        y = (self.splash.winfo_screenheight() // 2) - (height // 2)
        self.splash.geometry(f"{width}x{height}+{x}+{y}")
    
    def start_loading(self):
        """Inicia la animación de carga en hilo separado"""
        def loading_thread():
            self.progress = 0
            self.status_messages = [
                "Iniciando aplicación...",
                "Cargando módulos...",
                "Preparando interfaz...",
                "Listo para usar"
            ]
            self.current_status = 0
            
            for i in range(50):  # 50 pasos = 3 segundos aprox
                if not self.splash.winfo_exists():
                    return
                    
                # Actualizar progreso
                self.progress = i / 50.0
                progress_bars = int(self.progress * 20)
                progress_text = "█" * progress_bars + "░" * (20 - progress_bars)
                
                # Actualizar UI en hilo principal
                self.splash.after(0, lambda p=progress_text: self.progress_label.configure(text=p))
                
                # Cambiar mensaje de estado
                if self.progress >= 0.25 and self.current_status == 0:
                    self.current_status = 1
                    self.splash.after(0, lambda: self.status_label.configure(text=self.status_messages[1]))
                elif self.progress >= 0.5 and self.current_status == 1:
                    self.current_status = 2
                    self.splash.after(0, lambda: self.status_label.configure(text=self.status_messages[2]))
                elif self.progress >= 0.75 and self.current_status == 2:
                    self.current_status = 3
                    self.splash.after(0, lambda: self.status_label.configure(text=self.status_messages[3]))
                
                time.sleep(0.06)  # 60ms entre actualizaciones
            
            # Completar carga
            self.splash.after(0, lambda: self.progress_label.configure(text="█" * 20))
            self.splash.after(0, lambda: self.status_label.configure(text="¡Bienvenido!"))
            
            # Cerrar splash después de 500ms
            time.sleep(0.5)
            self.splash.after(0, self.close_splash)
        
        # Iniciar hilo de carga
        thread = threading.Thread(target=loading_thread, daemon=True)
        thread.start()
    
    def close_splash(self):
        """Cierra el splash y abre la aplicación principal"""
        try:
            self.splash.destroy()
        except Exception:
            pass
            
        # Crear y mostrar la aplicación principal
        try:
            app = FileHandlerApp()
            app.mainloop()
        except Exception as e:
            print(f"Error al abrir aplicación principal: {e}")
    
    def show(self):
        """Muestra el splash screen"""
        self.splash.mainloop()

# ============================================================
# TOOLTIP
# ============================================================
class ToolTip:
    def __init__(self, widget, text):
        self.widget = widget
        self.text = text
        self.tip_window = None
        self._show_after_id = None
        self._hide_after_id = None
        widget.bind("<Enter>", self._schedule_show)
        widget.bind("<Leave>", self._schedule_hide)
        widget.bind("<Motion>", self._maybe_reposition)
        widget.bind("<Destroy>", lambda e: self._cleanup())

    def _cleanup(self):
        self._cancel_show()
        self._cancel_hide()
        if self.tip_window:
            try:
                self.tip_window.destroy()
            except Exception:
                pass
            self.tip_window = None

    def _schedule_show(self, event=None):
        self._cancel_hide()
        self._cancel_show()
        # Aparición ágil pero evitando parpadeo
        self._show_after_id = self.widget.after(150, lambda: self._show_tip(event))

    def _cancel_show(self):
        if self._show_after_id:
            try:
                self.widget.after_cancel(self._show_after_id)
            except Exception:
                pass
            self._show_after_id = None

    def _schedule_hide(self, event=None):
        self._cancel_show()
        self._cancel_hide()
        # Ocultación rápida para que no se "queden pegados"
        self._hide_after_id = self.widget.after(100, self._hide_tip)

    def _cancel_hide(self):
        if self._hide_after_id:
            try:
                self.widget.after_cancel(self._hide_after_id)
            except Exception:
                pass
            self._hide_after_id = None

    def _show_tip(self, event=None):
        if self.tip_window or not self.text:
            return
        # Posición inicial cerca del cursor si existe evento
        if event is not None and hasattr(event, "x_root") and hasattr(event, "y_root"):
            x = event.x_root + 12
            y = event.y_root + 12
        else:
            x = self.widget.winfo_rootx() + 40
            y = self.widget.winfo_rooty() + 20

        self.tip_window = tw = ctk.CTkToplevel(self.widget)
        tw.wm_overrideredirect(True)
        try:
            tw.attributes("-topmost", True)
        except Exception:
            pass

        label = ctk.CTkLabel(
            tw,
            text=self.text,
            justify="left",
            font=("Segoe UI", 12),
            corner_radius=8,
            fg_color="#FFFFFF",  # blanco
            text_color="#000000",  # negro
            padx=10,
            pady=6
        )
        label.pack()

        # Calcular tamaño y reubicar si se corta
        tw.update_idletasks()
        width = tw.winfo_width()
        height = tw.winfo_height()
        screen_w = tw.winfo_screenwidth()
        screen_h = tw.winfo_screenheight()

        if x + width > screen_w - 4:
            x = max(0, screen_w - width - 8)
        if y + height > screen_h - 4:
            # Mostrar por encima si no cabe abajo
            y = max(0, y - height - 8)

        tw.wm_geometry(f"+{x}+{y}")

    def _hide_tip(self):
        if self.tip_window:
            self.tip_window.destroy()
            self.tip_window = None

    def _maybe_reposition(self, event=None):
        # Reposicionar en vivo si el mouse se acerca a los bordes
        if not self.tip_window:
            return
        try:
            x = event.x_root + 12 if event and hasattr(event, "x_root") else self.tip_window.winfo_x()
            y = event.y_root + 12 if event and hasattr(event, "y_root") else self.tip_window.winfo_y()
        except Exception:
            return

        tw = self.tip_window
        tw.update_idletasks()
        width = tw.winfo_width()
        height = tw.winfo_height()
        screen_w = tw.winfo_screenwidth()
        screen_h = tw.winfo_screenheight()

        if x + width > screen_w - 4:
            x = max(0, screen_w - width - 8)
        if y + height > screen_h - 4:
            y = max(0, y - height - 8)

        tw.wm_geometry(f"+{x}+{y}")

# ============================================================
# VISOR DE LOGS EN VIVO SIN BLOQUEO
# ============================================================
def center_on_parent(window, parent):
    """Centra una ventana con respecto a su ventana padre"""
    parent.update_idletasks()
    width = window.winfo_width()
    height = window.winfo_height()
    parent_x = parent.winfo_x()
    parent_y = parent.winfo_y()
    parent_width = parent.winfo_width()
    parent_height = parent.winfo_height()
    
    x = parent_x + (parent_width // 2) - (width // 2)
    y = parent_y + (parent_height // 2) - (height // 2)
    
    window.geometry(f"{width}x{height}+{x}+{y}")

class LogViewer(ctk.CTkToplevel):
    def __init__(self, master):
        super().__init__(master)
        self.title("Logs en vivo")
        self.geometry("600x400")
        self.resizable(False, False)

        # Centrar en la ventana padre y hacer modal
        self.update_idletasks()
        center_on_parent(self, master)
        self.lift()  # Asegurar que aparece encima
        self.focus_force()  # Forzar el foco
        
        # Hacer la ventana modal
        self.transient(master)
        self.grab_set()

        self.text_area = scrolledtext.ScrolledText(self, wrap="word", font=("Consolas", 11))
        self.text_area.pack(padx=10, pady=(10, 5), fill="both", expand=True)

        bottom_frame = ctk.CTkFrame(self)
        bottom_frame.pack(fill="x", padx=10, pady=(0, 10))

        self.status_label = ctk.CTkLabel(bottom_frame, text="", font=("Segoe UI", 10))
        self.status_label.pack(side="left", padx=10)

        ctk.CTkButton(bottom_frame, text="Actualizar ahora", width=130, command=self.update_log).pack(side="right", padx=10)

        self.after(500, self.update_log)

    def update_log(self):
        try:
            if os.path.exists("filehandler.log"):
                with open("filehandler.log", "r", encoding="utf-8", errors="ignore") as f:
                    contenido = f.read()
                self.text_area.delete("1.0", "end")
                self.text_area.insert("1.0", contenido)
                self.text_area.see("end")
            else:
                self.text_area.delete("1.0", "end")
                self.text_area.insert("1.0", "No hay logs disponibles todavía.")

            # Indicador de actualización breve
            self.status_label.configure(text="🟢 Actualizando...")
            self.after(600, lambda: self.status_label.configure(text=""))
        except Exception as e:
            self.text_area.insert("end", f"\n[ERROR al leer log]: {e}\n")

        self.after(3000, self.update_log)

# ============================================================
# FUNCIONES CORE
# ============================================================
def configurar_logging():
    """Configura el sistema de logging"""
    logging.basicConfig(
        filename='filehandler.log',
        level=logging.INFO,
        format='%(asctime)s - %(levelname)s - %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )

def limpiar_logs():
    """Limpia el archivo de logs"""
    try:
        if os.path.exists("filehandler.log"):
            os.remove("filehandler.log")
            logging.info("Archivo de logs limpiado")
            return "Logs limpiados correctamente."
        return "No hay logs para limpiar."
    except Exception as e:
        return f"Error al limpiar logs: {e}"

def get_file_hash(filepath):
    """Calcula el hash MD5 de un archivo"""
    hash_md5 = hashlib.md5()
    with open(filepath, "rb") as f:
        for chunk in iter(lambda: f.read(4096), b""):
            hash_md5.update(chunk)
    return hash_md5.hexdigest()

def organizar_por_fecha(origen, destino, extensions=None):
    """Organiza archivos por fecha de creación"""
    configurar_logging()
    archivos_movidos = 0
    
    try:
        for archivo in os.listdir(origen):
            if extensions and not any(archivo.lower().endswith(ext) for ext in extensions):
                continue
                
            ruta_completa = os.path.join(origen, archivo)
            if os.path.isfile(ruta_completa):
                tiempo = os.path.getctime(ruta_completa)
                fecha = datetime.fromtimestamp(tiempo)
                
                año_dir = os.path.join(destino, str(fecha.year))
                mes_dir = os.path.join(año_dir, f"{fecha.month:02d}")
                
                os.makedirs(mes_dir, exist_ok=True)
                
                nuevo_destino = os.path.join(mes_dir, archivo)
                shutil.move(ruta_completa, nuevo_destino)
                archivos_movidos += 1
                logging.info(f"Archivo movido: {archivo} -> {nuevo_destino}")
        
        return f"Se organizaron {archivos_movidos} archivos por fecha."
    except Exception as e:
        logging.error(f"Error al organizar archivos: {e}")
        raise Exception(f"Error al organizar archivos: {e}")

def mover_duplicados(origen, destino, extensions=None):
    """Mueve archivos duplicados a carpeta específica"""
    configurar_logging()
    archivos_movidos = 0
    
    try:
        os.makedirs(destino, exist_ok=True)
        
        for archivo in os.listdir(origen):
            if extensions and not any(archivo.lower().endswith(ext) for ext in extensions):
                continue
                
            if "(" in archivo and ")" in archivo:
                ruta_origen = os.path.join(origen, archivo)
                if os.path.isfile(ruta_origen):
                    ruta_destino = os.path.join(destino, archivo)
                    shutil.move(ruta_origen, ruta_destino)
                    archivos_movidos += 1
                    logging.info(f"Duplicado movido: {archivo}")
        
        return f"Se movieron {archivos_movidos} archivos duplicados."
    except Exception as e:
        logging.error(f"Error al mover duplicados: {e}")
        raise Exception(f"Error al mover duplicados: {e}")

def copiar_archivos_nuevos(origen, destino, extensions=None):
    """Copia archivos nuevos evitando duplicados por contenido"""
    configurar_logging()
    archivos_copiados = 0
    
    try:
        os.makedirs(destino, exist_ok=True)
        hashes_destino = {}
        
        # Primero recopilamos los hashes de los archivos en destino
        for archivo in os.listdir(destino):
            ruta_completa = os.path.join(destino, archivo)
            if os.path.isfile(ruta_completa):
                hashes_destino[get_file_hash(ruta_completa)] = archivo
        
        # Procesamos los archivos de origen
        for archivo in os.listdir(origen):
            if extensions and not any(archivo.lower().endswith(ext) for ext in extensions):
                continue
                
            ruta_origen = os.path.join(origen, archivo)
            if os.path.isfile(ruta_origen):
                hash_archivo = get_file_hash(ruta_origen)
                
                if hash_archivo not in hashes_destino:
                    nombre_base, extension = os.path.splitext(archivo)
                    nuevo_nombre = archivo
                    contador = 1
                    
                    # Si el archivo ya existe, agregar número
                    while os.path.exists(os.path.join(destino, nuevo_nombre)):
                        nuevo_nombre = f"{nombre_base}_{contador}{extension}"
                        contador += 1
                    
                    shutil.copy2(ruta_origen, os.path.join(destino, nuevo_nombre))
                    archivos_copiados += 1
                    logging.info(f"Archivo copiado: {archivo} -> {nuevo_nombre}")
        
        return f"Se copiaron {archivos_copiados} archivos nuevos."
    except Exception as e:
        logging.error(f"Error al copiar archivos: {e}")
        raise Exception(f"Error al copiar archivos: {e}")

def mover_desde_subcarpetas(origen, destino):
    """Mueve archivos desde subcarpetas a una carpeta común"""
    configurar_logging()
    archivos_movidos = 0
    
    try:
        os.makedirs(destino, exist_ok=True)
        
        for carpeta_actual, subcarpetas, archivos in os.walk(origen):
            for archivo in archivos:
                ruta_origen = os.path.join(carpeta_actual, archivo)
                
                # Si el archivo ya existe en destino, agregar número
                nombre_base, extension = os.path.splitext(archivo)
                nuevo_nombre = archivo
                contador = 1
                
                while os.path.exists(os.path.join(destino, nuevo_nombre)):
                    nuevo_nombre = f"{nombre_base}_{contador}{extension}"
                    contador += 1
                
                shutil.move(ruta_origen, os.path.join(destino, nuevo_nombre))
                archivos_movidos += 1
                logging.info(f"Archivo movido desde subcarpeta: {ruta_origen} -> {nuevo_nombre}")
        
        # Eliminar carpetas vacías
        for carpeta_actual, subcarpetas, archivos in os.walk(origen, topdown=False):
            try:
                os.rmdir(carpeta_actual)
            except OSError:
                pass
        
        return f"Se movieron {archivos_movidos} archivos desde las subcarpetas."
    except Exception as e:
        logging.error(f"Error al mover desde subcarpetas: {e}")
        raise Exception(f"Error al mover desde subcarpetas: {e}")

# ============================================================
# SELECTOR DE EXTENSIONES
# ============================================================
class ExtensionSelector(ctk.CTkToplevel):
    def __init__(self, master, current_extensions=None):
        super().__init__(master)
        self.title("Seleccionar Extensiones")
        self.geometry("400x500")
        self.resizable(False, False)
        
        # Centrar en la ventana padre
        self.update_idletasks()
        center_on_parent(self, master)
        
        self.result = set(current_extensions) if current_extensions else set()
        
        # Frame superior para entrada manual
        input_frame = ctk.CTkFrame(self)
        input_frame.pack(fill="x", padx=10, pady=10)
        
        self.extension_var = ctk.StringVar()
        entry = ctk.CTkEntry(input_frame, textvariable=self.extension_var, placeholder_text=".ext")
        entry.pack(side="left", padx=5, expand=True, fill="x")
        
        add_btn = ctk.CTkButton(input_frame, text="Añadir", width=80, command=self.add_extension)
        add_btn.pack(side="right", padx=5)
        
        # Lista de extensiones comunes
        common_frame = ctk.CTkFrame(self)
        common_frame.pack(fill="x", padx=10, pady=(0,10))
        
        common_exts = [".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", 
                      ".xls", ".xlsx", ".txt", ".zip", ".rar", ".mp3", ".mp4"]
        
        row = 0
        col = 0
        for ext in common_exts:
            btn = ctk.CTkButton(common_frame, text=ext, width=70,
                              command=lambda e=ext: self.toggle_extension(e))
            btn.grid(row=row, column=col, padx=2, pady=2)
            col += 1
            if col > 3:
                col = 0
                row += 1
        
        # Lista de extensiones seleccionadas
        ctk.CTkLabel(self, text="Extensiones seleccionadas:").pack(padx=10, pady=(10,0))
        
        self.list_frame = ctk.CTkFrame(self)
        self.list_frame.pack(fill="both", expand=True, padx=10, pady=10)
        
        # Botones de acción
        btn_frame = ctk.CTkFrame(self)
        btn_frame.pack(fill="x", padx=10, pady=(0,10))
        
        ctk.CTkButton(btn_frame, text="Aceptar", command=self.confirm).pack(side="left", padx=5, expand=True)
        ctk.CTkButton(btn_frame, text="Cancelar", command=self.cancel).pack(side="right", padx=5, expand=True)
        
        self.update_list()
        
        # Hacer modal
        self.transient(master)
        self.grab_set()
        self.wait_window()
    
    def add_extension(self):
        ext = self.extension_var.get().strip()
        if not ext: return
        if not ext.startswith("."): ext = "." + ext
        self.result.add(ext.lower())
        self.extension_var.set("")
        self.update_list()
    
    def toggle_extension(self, ext):
        if ext.lower() in self.result:
            self.result.remove(ext.lower())
        else:
            self.result.add(ext.lower())
        self.update_list()
    
    def update_list(self):
        for widget in self.list_frame.winfo_children():
            widget.destroy()
        
        row = 0
        col = 0
        for ext in sorted(self.result):
            frame = ctk.CTkFrame(self.list_frame)
            frame.grid(row=row, column=col, padx=2, pady=2)
            
            ctk.CTkLabel(frame, text=ext).pack(side="left", padx=5)
            ctk.CTkButton(frame, text="×", width=20, 
                         command=lambda e=ext: self.toggle_extension(e)).pack(side="right", padx=2)
            
            col += 1
            if col > 2:
                col = 0
                row += 1
    
    def confirm(self):
        self.destroy()
    
    def cancel(self):
        self.result = None
        self.destroy()

# ============================================================
# APLICACIÓN PRINCIPAL
# ============================================================
class FileHandlerApp(ctk.CTk):
    def __init__(self):
        super().__init__()
        self.title("FileHandler")
        self.geometry("600x500")  # Aumentado de 450 a 500 para dar espacio al botón
        self.resizable(False, False)
        
        # Centrar ventana en pantalla
        self.center_window()

        self.origen = ctk.StringVar()
        self.destino = ctk.StringVar()

        title = ctk.CTkLabel(self, text="FileHandler", font=("Segoe UI", 26, "bold"))
        title.pack(pady=(20, 10))

        paths_frame = ctk.CTkFrame(self)
        paths_frame.pack(pady=10, padx=20, fill="x")

        origin_row = ctk.CTkFrame(paths_frame)
        origin_row.pack(fill="x", pady=(5, 10))
        ctk.CTkLabel(origin_row, text="Ruta de origen:", width=120).pack(side="left", padx=5)
        ctk.CTkEntry(origin_row, textvariable=self.origen, width=380).pack(side="left", padx=5)
        ctk.CTkButton(origin_row, text="📁", width=40, command=self.select_origen).pack(side="left", padx=5)

        dest_row = ctk.CTkFrame(paths_frame)
        dest_row.pack(fill="x", pady=(5, 10))
        ctk.CTkLabel(dest_row, text="Ruta de destino:", width=120).pack(side="left", padx=5)
        ctk.CTkEntry(dest_row, textvariable=self.destino, width=380).pack(side="left", padx=5)
        ctk.CTkButton(dest_row, text="📁", width=40, command=self.select_destino).pack(side="left", padx=5)

        # Frame para extensiones
        ext_frame = ctk.CTkFrame(paths_frame)
        ext_frame.pack(fill="x", pady=(5, 10))
        
        ctk.CTkLabel(ext_frame, text="Extensiones:", width=120).pack(side="left", padx=5)
        self.ext_label = ctk.CTkLabel(ext_frame, text="Todas", width=380)
        self.ext_label.pack(side="left", padx=5)
        self.extensions = None
        
        ext_btn = ctk.CTkButton(ext_frame, text="...", width=40, command=self.select_extensions)
        ext_btn.pack(side="left", padx=5)
        ToolTip(ext_btn, "Seleccionar extensiones de archivo a procesar")

        btn_frame = ctk.CTkFrame(self)
        btn_frame.pack(pady=15, padx=20, fill="both")
        btn_frame.grid_columnconfigure((0,1), weight=1)

        # Botones principales
        btn_organizar = ctk.CTkButton(
            btn_frame, 
            text="Organizar por fecha",
            command=self.run_organizar,
            font=("Segoe UI", 14, "bold"),
            height=45
        )
        btn_organizar.grid(row=0, column=0, padx=10, pady=5, sticky="ew")
        ToolTip(btn_organizar, "Mueve todos los archivos creando carpetas año y mes de creación del archivo para su organización.")

        btn_duplicados = ctk.CTkButton(
            btn_frame,
            text="Mover duplicados",
            command=self.run_duplicados,
            font=("Segoe UI", 14, "bold"),
            height=45
        )
        btn_duplicados.grid(row=0, column=1, padx=10, pady=5, sticky="ew")
        ToolTip(btn_duplicados, "Mueve todos los archivos que en el nombre muestren (n), donde n es el número de archivo duplicado.")

        btn_copiar = ctk.CTkButton(
            btn_frame,
            text="Copiar archivos nuevos",
            command=self.run_copiar,
            font=("Segoe UI", 14, "bold"),
            height=45
        )
        btn_copiar.grid(row=1, column=0, padx=10, pady=5, sticky="ew")
        ToolTip(btn_copiar, "Hace una copia de todos los archivos sin duplicados salvo que su contenido no sea igual, en este caso le da otro nombre.")

        btn_subcarpetas = ctk.CTkButton(
            btn_frame,
            text="Mover desde subcarpetas",
            command=self.run_subcarpetas,
            font=("Segoe UI", 14, "bold"),
            height=45
        )
        btn_subcarpetas.grid(row=1, column=1, padx=10, pady=5, sticky="ew")
        ToolTip(btn_subcarpetas, "Saca todos los archivos de un directorio y sus subcarpetas a una carpeta común.")

        # Botones de logs
        btn_logs = ctk.CTkButton(
            btn_frame,
            text="Limpiar logs",
            command=self.clean_logs,
            font=("Segoe UI", 14, "bold"),
            height=45,
            fg_color="gray"
        )
        btn_logs.grid(row=2, column=0, padx=10, pady=5, sticky="ew")
        ToolTip(btn_logs, "Elimina el archivo de registro de operaciones")

        btn_ver_logs = ctk.CTkButton(
            btn_frame,
            text="Ver logs en vivo",
            command=self.open_live_logs,
            font=("Segoe UI", 14, "bold"),
            height=45,
            fg_color="gray"
        )
        btn_ver_logs.grid(row=2, column=1, padx=10, pady=5, sticky="ew")
        ToolTip(btn_ver_logs, "Muestra la ventana de seguimiento de operaciones en tiempo real")

        self.progress = ctk.CTkProgressBar(self, mode="indeterminate")
        self.progress.pack(pady=(10,5), padx=20, fill="x")
        self.progress.stop()
        self.progress.pack_forget()

        self.status = ctk.CTkLabel(self, text="Listo.", font=("Segoe UI", 12))
        self.status.pack(pady=(5,10))

        # Botón de salir
        btn_salir = ctk.CTkButton(
            self,
            text="Salir",
            command=self.destroy,
            font=("Segoe UI", 14, "bold"),
            height=45,
            fg_color="#d9534f",
            hover_color="#c9302c"
        )
        btn_salir.pack(pady=(0,15))
        ToolTip(btn_salir, "Cierra la aplicación")

    def center_window(self):
        """Centra la ventana en la pantalla"""
        self.update_idletasks()
        width = self.winfo_width()
        height = self.winfo_height()
        x = (self.winfo_screenwidth() // 2) - (width // 2)
        y = (self.winfo_screenheight() // 2) - (height // 2)
        self.geometry(f"{width}x{height}+{x}+{y}")

    # === Funciones GUI ===
    def select_origen(self):
        # Actualizar la geometría actual antes de mostrar el diálogo
        self.update_idletasks()
        x = self.winfo_x() + self.winfo_width()/2
        y = self.winfo_y() + self.winfo_height()/2
        path = filedialog.askdirectory(title="Selecciona carpeta de origen")
        if path: 
            self.origen.set(path)
            # Volver a centrar la ventana principal
            self.lift()
            self.focus_force()

    def select_destino(self):
        # Actualizar la geometría actual antes de mostrar el diálogo
        self.update_idletasks()
        x = self.winfo_x() + self.winfo_width()/2
        y = self.winfo_y() + self.winfo_height()/2
        path = filedialog.askdirectory(title="Selecciona carpeta de destino")
        if path: 
            self.destino.set(path)
            # Volver a centrar la ventana principal
            self.lift()
            self.focus_force()

    def select_extensions(self):
        selector = ExtensionSelector(self, self.extensions)
        if selector.result is not None:
            self.extensions = selector.result
            if self.extensions:
                self.ext_label.configure(text=", ".join(sorted(self.extensions)))
            else:
                self.ext_label.configure(text="Todas")

    def check_paths(self):
        if not self.origen.get() or not self.destino.get():
            messagebox.showwarning("Atención","Debes seleccionar carpetas de origen y destino.")
            return False
        return True

    def threaded(self, func, *args):
        threading.Thread(target=func, args=args, daemon=True).start()

    def start_progress(self):
        self.progress.pack(pady=(10,5), padx=20, fill="x")
        self.progress.start()

    def stop_progress(self):
        self.progress.stop()
        self.progress.pack_forget()

    # === Acciones ===
    def run_organizar(self): 
        if not self.check_paths(): return
        self.status.configure(text="Organizando por fecha...")
        self.start_progress()
        self.threaded(self.execute_task, organizar_por_fecha)

    def run_duplicados(self):
        if not self.check_paths(): return
        self.status.configure(text="Buscando duplicados...")
        self.start_progress()
        self.threaded(self.execute_task, mover_duplicados)

    def run_copiar(self):
        if not self.check_paths(): return
        self.status.configure(text="Copiando archivos nuevos...")
        self.start_progress()
        self.threaded(self.execute_task, copiar_archivos_nuevos)

    def run_subcarpetas(self):
        if not self.check_paths(): return
        self.status.configure(text="Moviendo desde subcarpetas...")
        self.start_progress()
        self.threaded(self.execute_task, mover_desde_subcarpetas)

    def execute_task(self, func):
        try:
            resultado = func(self.origen.get(), self.destino.get(), self.extensions)
            self.after(0, self.stop_progress)
            self.after(0, lambda: self.status.configure(text=f"✅ {resultado}"))
            self.after(0, lambda: messagebox.showinfo("Completado", f"Proceso finalizado.\n\n{resultado}"))
        except Exception as e:
            self.after(0, self.stop_progress)
            self.after(0, lambda: self.status.configure(text=f"❌ Error: {e}"))
            self.after(0, lambda: messagebox.showerror("Error", str(e)))
        finally:
            self.after(0, self.stop_progress)

    def clean_logs(self):
        msg = limpiar_logs()
        self.status.configure(text=msg)
        messagebox.showinfo("Logs", msg)

    def open_live_logs(self):
        LogViewer(self)

if __name__=="__main__":
    # Mostrar splash screen primero
    splash = SplashScreen()
    splash.show()
