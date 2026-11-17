@echo off
echo Compilando FileHandler a EXE...
echo.

REM Instalar PyInstaller si no está instalado
pip install pyinstaller

REM Limpiar builds anteriores
if exist build rmdir /s /q build
if exist dist rmdir /s /q dist
if exist *.spec del *.spec

REM Compilar con PyInstaller
pyinstaller --onefile --windowed --name=FileHandler --clean main.py

echo.
echo Compilacion completada!
echo El ejecutable esta en: dist\FileHandler.exe
pause

