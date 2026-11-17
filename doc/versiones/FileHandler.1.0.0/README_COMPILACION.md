# 🚀 Compilación de FileHandler a EXE

## 📋 Requisitos previos

1. **Python 3.8+** instalado
2. **Pip** actualizado
3. **Archivos del proyecto**:
   - `main.py`
   - `filehandler_core.py`
   - `requirements.txt`

## 🛠️ Método 1: Script automático (Recomendado)

### Opción A: Script Python completo
```bash
python build_exe.py
```

### Opción B: Script batch simple
```bash
build_simple.bat
```

## 🛠️ Método 2: Compilación manual

### 1. Instalar dependencias
```bash
pip install -r requirements.txt
```

### 2. Compilar con PyInstaller
```bash
pyinstaller --onefile --windowed --name=FileHandler main.py
```

### 3. Opciones avanzadas
```bash
pyinstaller --onefile --windowed --name=FileHandler --icon=icon.ico --clean main.py
```

## 📁 Resultado

- **Ejecutable**: `dist/FileHandler.exe`
- **Tamaño**: ~15-25 MB
- **Tipo**: Portable (no requiere instalación)

## ⚙️ Opciones de PyInstaller

| Opción | Descripción |
|--------|-------------|
| `--onefile` | Un solo archivo ejecutable |
| `--windowed` | Sin ventana de consola |
| `--windowed` | Sin ventana de consola |
| `--name=FileHandler` | Nombre del ejecutable |
| `--icon=icon.ico` | Icono personalizado |
| `--clean` | Limpiar cache |

## 🔧 Solución de problemas

### Error: "No module named 'customtkinter'"
```bash
pip install customtkinter
```

### Error: "PyInstaller not found"
```bash
pip install pyinstaller
```

### Ejecutable muy grande
```bash
pyinstaller --onefile --windowed --exclude-module=matplotlib --exclude-module=numpy main.py
```

### Error de importación en runtime
Agregar al comando:
```bash
--hidden-import=customtkinter
--hidden-import=tkinter
```

## 📦 Distribución

Para distribuir la aplicación:

1. **Copia** `FileHandler.exe` a la carpeta deseada
2. **Incluye** `filehandler_core.py` en la misma carpeta
3. **Opcional**: Crea un acceso directo al .exe

## 🎯 Optimizaciones

### Reducir tamaño del ejecutable:
```bash
pyinstaller --onefile --windowed --exclude-module=tkinter.test --exclude-module=test main.py
```

### Incluir archivos adicionales:
```bash
pyinstaller --onefile --windowed --add-data="config.ini;." main.py
```

### Compilar con icono:
```bash
pyinstaller --onefile --windowed --icon=icon.ico main.py
```

## ✅ Verificación

Después de la compilación:

1. **Probar** el ejecutable en `dist/FileHandler.exe`
2. **Verificar** que todas las funciones trabajen
3. **Comprobar** que no aparezcan errores de importación
4. **Testear** en una máquina sin Python instalado

## 📞 Soporte

Si encuentras problemas:
1. Verifica que todas las dependencias estén instaladas
2. Revisa que los archivos fuente estén completos
3. Prueba la compilación paso a paso
4. Consulta la documentación de PyInstaller

