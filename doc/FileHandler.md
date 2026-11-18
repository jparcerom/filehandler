# FileHandler v2.0.0

La aplicación de escritorio se desarrolla en C# con .NET 8 y WPF para Windows. Cada operación permite seleccionar sus propias carpetas de origen y destino, y muestra progreso y actividad.

## Organizar archivos multimedia

Esta única opción combina la organización por fecha con la selección de tipos multimedia. Se elige una carpeta de origen y se selecciona organizar imágenes, vídeos o ambos. Cada tipo tiene un destino independiente; pueden seleccionarse destinos diferentes o elegir la misma carpeta para organizarlos juntos.

El escaneo recursivo reconoce, sin distinguir mayúsculas, imágenes `.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`, `.tif`, `.tiff`, `.webp`, `.heic`, `.heif` y `.raw`; y vídeos `.mp4`, `.mov`, `.mkv`, `.avi`, `.wmv`, `.flv`, `.webm`, `.m4v`, `.mpg`, `.mpeg` y `.3gp`. Los archivos no multimedia no se modifican.

Los archivos seleccionados se mueven a carpetas `Año/NN-Mes`, usando la fecha más antigua entre creación y modificación. Si ambos tipos comparten destino, se organizan en la misma estructura; si tienen destinos distintos, cada uno recibe la misma estructura en su propia raíz.

Ante una colisión se conserva el archivo existente y el entrante recibe un sufijo incremental, tanto si el contenido coincide como si difiere. El movimiento normal se usa cuando es posible; si hay que cruzar volúmenes o el movimiento directo falla, se copia y se comprueba por SHA-256 antes de eliminar el origen. Si la copia, la verificación o el borrado fallan, se conserva el origen y se informa del error. Las carpetas que queden vacías no se eliminan.

## Mover duplicados nativos de Windows

Windows suele añadir ` (n)` al nombre de los duplicados. Se buscan recursivamente solo archivos cuyo nombre termine así antes de la extensión. Se verifica su contenido mediante SHA-256 contra el archivo original sin sufijo; si no existe, se conserva como referencia el archivo con el sufijo numérico más bajo.

Solo los duplicados coincidentes se mueven al destino confirmado. Los archivos diferentes, no verificables y las referencias se conservan en origen. El destino no puede estar dentro del origen. Ante una colisión se conserva el archivo existente y se añade un sufijo incremental al entrante.

## Sacar archivos de subcarpetas

Se mueven recursivamente a la raíz del destino los archivos encontrados en subcarpetas. Los archivos que ya estén en la raíz del origen no se modifican, las carpetas que queden vacías no se eliminan y los archivos que ya estén en la raíz del destino se conservan allí. Ante cualquier colisión se mantienen ambos archivos y el entrante recibe un sufijo incremental, incluso si su contenido es idéntico.

## Procesos

Cualquier proceso debe mostrar y registrar su actividad, así como una barra de progreso o un conteo de archivos procesados. Los errores se informan explícitamente.

Los registros se guardan en `%LOCALAPPDATA%\FileHandler\logs\filehandler.log`.

## Compilación y pruebas

Se necesita el SDK de .NET 8. Desde la raíz del repositorio:

```powershell
dotnet build .\src\FileHandler\FileHandler.csproj
dotnet test .\tests\FileHandler.Tests\FileHandler.Tests.csproj
```

El plan y estado del desarrollo se mantienen en [ROADMAP.md](./ROADMAP.md).
