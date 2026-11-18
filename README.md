# FileHandler 2.0

[Español](#español) | [English](#english)

## Español

FileHandler es una aplicación de escritorio para Windows que ayuda a organizar archivos multimedia, revisar duplicados y extraer archivos de subcarpetas.

### Funciones

- **Organizar archivos multimedia:** selecciona imágenes, vídeos o ambos y ordénalos en carpetas por año y mes. Puedes usar destinos separados para imágenes y vídeos o reunirlos en el mismo destino.
- **Buscar duplicados multimedia:** encuentra imágenes y vídeos con contenido idéntico mediante tamaño y hash SHA-256, aunque tengan nombres distintos. Revisa la selección automática, conserva al menos una copia de cada grupo y confirma antes de enviar archivos a la Papelera.
- **Sacar archivos de subcarpetas:** mueve a la raíz del destino los archivos encontrados en las subcarpetas del origen.

La aplicación muestra el progreso y la actividad de cada proceso. Los registros se guardan en `%LOCALAPPDATA%\FileHandler\logs\filehandler.log`.

### Descargar e instalar

Descarga el instalador o el ZIP portable desde [GitHub Releases](https://github.com/jparcerom/filehandler/releases).

- **Instalador:** permite elegir español o inglés y guarda la selección para los siguientes inicios. Para cambiar el idioma más adelante, vuelve a ejecutar el instalador y elige el otro idioma.
- **Versión portable:** no requiere instalar .NET en el equipo de destino. Usa español si el idioma de Windows es español; para cualquier otro idioma, usa inglés.

Las carpetas de organización conservan el prefijo numérico del mes y muestran su nombre en el idioma seleccionado, por ejemplo, `02-Febrero` o `02-February`. Las carpetas existentes no se renombran.

### Compilar y probar

Se necesita Windows y el SDK de .NET 8. Desde la raíz del repositorio, ejecuta en PowerShell:

```powershell
dotnet build .\src\FileHandler\FileHandler.csproj
dotnet test .\tests\FileHandler.Tests\FileHandler.Tests.csproj
```

Para generar los paquetes de Windows, se necesita además Inno Setup 6 para crear el instalador:

```powershell
.\packaging\publish.ps1
```

Consulta [la documentación de la aplicación](./doc/FileHandler.md) y [las instrucciones de publicación](./doc/RELEASE.md) para más detalles.

## English

FileHandler is a Windows desktop application for organizing media files, reviewing duplicates, and moving files out of subfolders.

### Features

- **Organize media files:** choose images, videos, or both, and organize them into year and month folders. Use separate destinations for images and videos or combine them in one destination.
- **Find media duplicates:** identifies images and videos with identical content using file size and SHA-256 hashes, even when their names differ. Review the automatic selection, keep at least one copy in each group, and confirm before sending files to the Recycle Bin.
- **Move files out of subfolders:** moves files found in the source's subfolders into the destination root.

The application displays progress and activity for each operation. Logs are saved to `%LOCALAPPDATA%\FileHandler\logs\filehandler.log`.

### Download and install

Download the installer or portable ZIP from [GitHub Releases](https://github.com/jparcerom/filehandler/releases).

- **Installer:** lets you choose English or Spanish and saves the selection for future launches. To change the language later, run the installer again and choose the other language.
- **Portable version:** does not require .NET to be installed on the target computer. It uses Spanish when the Windows language is Spanish; for any other language, it uses English.

Organized folders keep the numeric month prefix and use the selected language, for example, `02-February` or `02-Febrero`. Existing folders are not renamed.

### Build and test

Windows and the .NET 8 SDK are required. From the repository root, run in PowerShell:

```powershell
dotnet build .\src\FileHandler\FileHandler.csproj
dotnet test .\tests\FileHandler.Tests\FileHandler.Tests.csproj
```

To create the Windows packages, Inno Setup 6 is also required to build the installer:

```powershell
.\packaging\publish.ps1
```

See the [application documentation](./doc/FileHandler.md) and [release instructions](./doc/RELEASE.md) for more details.
