# Publicar FileHandler para Windows

Desde la raíz del repositorio, ejecuta:

```powershell
.\packaging\publish.ps1
```

El script lee la versión desde `src\FileHandler\FileHandler.csproj` y publica para `win-x64` como autocontenido; no requiere que el equipo de destino tenga .NET instalado. Genera:

- `artifacts\FileHandler-<versión>-win-x64-portable.zip`: carpeta portable autocontenida.
- `artifacts\FileHandler-<versión>-Setup-win-x64.exe`: instalador Inno Setup per-user, con acceso directo al menú Inicio, acceso directo opcional en el escritorio y desinstalador.

Para generar únicamente el portable:

```powershell
.\packaging\publish.ps1 -PortableOnly
```

Se necesita el SDK de .NET 8 y, para el instalador, Inno Setup 6 (`ISCC.exe`). El ejecutable del instalador no está firmado digitalmente. Los registros de la aplicación siguen guardándose en `%LOCALAPPDATA%\FileHandler\logs`.
