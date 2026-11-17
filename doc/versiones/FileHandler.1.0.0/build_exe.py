#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Script para compilar FileHandler a ejecutable usando PyInstaller
"""

import os
import sys
import subprocess
import shutil
from pathlib import Path

def check_pyinstaller():
    """Verifica si PyInstaller está instalado"""
    try:
        import PyInstaller
        print("✅ PyInstaller encontrado")
        return True
    except ImportError:
        print("❌ PyInstaller no encontrado. Instalando...")
        try:
            subprocess.check_call([sys.executable, "-m", "pip", "install", "pyinstaller"])
            print("✅ PyInstaller instalado correctamente")
            return True
        except subprocess.CalledProcessError:
            print("❌ Error al instalar PyInstaller")
            return False

def clean_build():
    """Limpia archivos de build anteriores"""
    dirs_to_clean = ["build", "dist", "__pycache__"]
    files_to_clean = ["*.spec"]
    
    for dir_name in dirs_to_clean:
        if os.path.exists(dir_name):
            print(f"🧹 Limpiando {dir_name}...")
            shutil.rmtree(dir_name)
    
    # Limpiar archivos .spec
    for spec_file in Path(".").glob("*.spec"):
        spec_file.unlink()
        print(f"🧹 Eliminando {spec_file}")

def build_executable():
    """Compila la aplicación a ejecutable"""
    print("🔨 Iniciando compilación...")
    
    # Comando de PyInstaller optimizado
    cmd = [
        "pyinstaller",
        "--onefile",                    # Un solo archivo ejecutable
        "--windowed",                   # Sin consola (GUI)
        "--name=FileHandler",          # Nombre del ejecutable
        "--icon=📁",                    # Icono (emoji como placeholder)
        "--add-data=filehandler_core.py;.",  # Incluir módulo core
        "--hidden-import=customtkinter",
        "--hidden-import=tkinter",
        "--hidden-import=tkinter.filedialog",
        "--hidden-import=tkinter.messagebox",
        "--hidden-import=tkinter.scrolledtext",
        "--hidden-import=threading",
        "--hidden-import=os",
        "--hidden-import=time",
        "--hidden-import=hashlib",
        "--hidden-import=shutil",
        "--hidden-import=datetime",
        "--clean",                      # Limpiar cache
        "main.py"                       # Archivo principal
    ]
    
    try:
        print("⚙️ Ejecutando PyInstaller...")
        result = subprocess.run(cmd, check=True, capture_output=True, text=True)
        print("✅ Compilación exitosa!")
        return True
    except subprocess.CalledProcessError as e:
        print(f"❌ Error en la compilación:")
        print(f"STDOUT: {e.stdout}")
        print(f"STDERR: {e.stderr}")
        return False

def create_launcher():
    """Crea un script launcher opcional"""
    launcher_content = '''@echo off
echo Iniciando FileHandler...
FileHandler.exe
pause
'''
    with open("launch_FileHandler.bat", "w", encoding="utf-8") as f:
        f.write(launcher_content)
    print("📄 Creado launcher: launch_FileHandler.bat")

def main():
    """Función principal"""
    print("🚀 FileHandler - Compilador a EXE")
    print("=" * 40)
    
    # Verificar PyInstaller
    if not check_pyinstaller():
        return False
    
    # Limpiar builds anteriores
    clean_build()
    
    # Compilar
    if build_executable():
        print("\n🎉 ¡Compilación completada!")
        print("📁 El ejecutable está en: dist/FileHandler.exe")
        print("📏 Tamaño aproximado: 15-25 MB")
        
        # Crear launcher opcional
        create_launcher()
        
        print("\n📋 Instrucciones:")
        print("1. El ejecutable está en la carpeta 'dist/'")
        print("2. Puedes copiar FileHandler.exe a cualquier carpeta")
        print("3. No necesita instalación, es portable")
        print("4. Asegúrate de que el archivo filehandler_core.py esté en la misma carpeta")
        
        return True
    else:
        print("\n❌ La compilación falló. Revisa los errores arriba.")
        return False

if __name__ == "__main__":
    success = main()
    input("\nPresiona Enter para salir...")
    sys.exit(0 if success else 1)

