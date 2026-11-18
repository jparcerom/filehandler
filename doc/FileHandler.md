# FileHandler v2.0.0
La aplicación original desarrollada en python, ubicada en la carpeta /doc/versiones, se permitía seleccionar un origen y un destino para las siguientes acciones:

### Mejoras
En vez de seleccionar la carpeta de origen y de destino antes de seleccionar la acción a realizar dentro de las opciones, hacerlo dentro de cada opción.

Una vez seleccionada la opción dentro de la pantalla principal, aparecerá el campo de ruta de origen para poder seleccionarla y a continuación seleccionar ruta de destino.

## Organizar por fecha
De una carpeta de origen se recogían todos los archivos contenidos y se movían a una ubicación de destino organizándolos por año y mes. Se creaba en el destino una carpeta año si no existía ya y dentro la carpeta mes si no existía con el siguiente formato ej. "01-Enero". El número se corresponde con el orden del mes que van desde 01-Enero hasta 12-Diciembre.

La fecha se recogía de la información del archivo, si la fecha de creación era inferior a la de la modificación se usaba ésta, si por el contrario la fecha de moficación era inferior a la de creación se usaba la de modificación.

### Mejoras
Al seleccionar esta opción y seleccionar origen, saldrá una barra de progreso en la ventana para mostrar el número de archivos de video y el número de archivos de imagen de la carpeta seleccionada y todas las subcarpetas. En una línea informativa debajo de las anteriores mostrará el número de subcarpetas. Ej.:
X - 25 archivos de video - DESTINO
X - 32 archivos de imagen - DESTINO
(en 10 subcarpetas)

Una vez hecho esto permitirá seleccionar que tipo de archivos queremos copiar (video y/o imagen) y un destino para cada tipo de archivo seleccionado que puede ser el mismo si se desea.

Cuando se inicie la copia o antes, si es posible, se verificará que no haya duplicados. Si se encuentra un archivo que tenga el nombre duplicado se hará una segunda verificación para determinar si es el mismo archivo, si es así se le dará otro nombre añadiendo un "-1" por ejemplo.

Finalizado este procedimiento, se podrá iniciar la copia confirmándolo con un botón.

En vez de mover los archivos en este proceso usar la copia, al finalizar mostrará al igual que al iniciar el número de archivos de video y el número de archivos de imagen copiados y dará la opción de borrar los archivos de origen seleccionando los archivos de video y/o de imagen. Si sólo hubiese archivos de video o de imagen y se seleccionan para borrar se eliminarán también las subcarpetas, en el caso de haber ambos tipos si se selecciona solo un tipo de archivo no se borrarán las subcarpetas, si se seleccionan ambos sí y siempre confirmando la acción con un botón, que pedirá confirmación y advirtiendo si es el caso que se eliminarán las subcarpetas.

* Nota importante, las subcarpetas será lo último que se elimine siempre y si están vacías, las subcarpetas que no estén vacías no se eliminarán. El procedimiento para eliminar subcarpetas se hará en modo árbol, se eliminarán siempre primero las subcarpetas que estén en subcarpetas para que no se detecten que no están vacías por tener una dentro vacía. 

## Mover duplicados nativos de windows
Cuando windows crea duplicados suele añadir al nombre " (n)" dónde n es un número que representará el número de veces que ese archivo está duplicado. Esto suele pasar cuando se hacen importaciones desde dispositivos móviles varias veces de los mismos archivos.

Al seleccionar esta opción seguirá el mismo procedimiento que la opción anterior para seleccionar la carpeta de origen y destino.

Con esta opción buscaremos en la carpeta de origen y todas sus subcarpetas únicamente archivos cuyo nombre termine en " (n)" antes de la extensión. Verificaremos su contenido mediante SHA-256 contra el archivo original, que tendrá el mismo nombre sin " (n)". Si no existe el original, se conservará como referencia el archivo con el sufijo numérico más bajo y se compararán con él los demás archivos del grupo.

Solo los duplicados que coincidan se moverán a la carpeta seleccionada como destino; los archivos diferentes, no verificables y las referencias se conservarán en origen. El destino no puede estar dentro del origen. Ante una colisión de nombres en destino se conservará el archivo existente y se añadirá un sufijo incremental al entrante. El movimiento requiere confirmación y vuelve a comprobar los hashes antes de mover.

En este caso sí se moverán los archivos en vez de copiarlos, una vez movidos se revisarán manualmente para verificar que efectivamente siguen este formato en la carpeta destino.

## Sacar archivos de subcarpetas
Otra de las opciones es la de sacar todos los archivos de las subcarpetas de una carpeta origen a la raiz de una destino.

# Procesos
Cualquier proceso que se ejecute debe mostrar la salida y guardarlo en logs además de mostrar mientras se ejecuta una barra de progreso o un conteo de archivos procesados.

# Desarrollo
La aplicación de escritorio se desarrolla en C# con .NET 8 y WPF para Windows.

### Decisión para la versión 2.0
La primera operación implementada es "Organizar por fecha": recorre la carpeta de origen y sus subcarpetas, elige la fecha más antigua entre creación y modificación, y mueve los archivos a carpetas `Año/NN-Mes`. Si ya existe un elemento con el mismo nombre en el destino, el archivo se omite y se registra para evitar sobrescrituras.

La operación "Copiar multimedia" cuenta recursivamente imágenes y vídeos. Por defecto reconoce, sin distinguir mayúsculas, imágenes `.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`, `.tif`, `.tiff`, `.webp`, `.heic`, `.heif` y `.raw`; y vídeos `.mp4`, `.mov`, `.mkv`, `.avi`, `.wmv`, `.flv`, `.webm`, `.m4v`, `.mpg`, `.mpeg` y `.3gp`.

La copia compara mediante SHA-256 cuando ya existe un archivo con el mismo nombre. Tanto si el contenido coincide como si difiere, conserva el archivo existente y guarda el entrante con un sufijo incremental (`-1`, `-2`, etc.); nunca sobrescribe. La copia no elimina por sí sola los archivos de origen; la limpieza es un paso posterior y confirmado.

La limpieza opcional del origen solo elimina archivos cuya copia terminó correctamente y cuyo hash SHA-256 sigue coincidiendo con el destino; los archivos fallidos o modificados se conservan. Las subcarpetas solo se intentan eliminar cuando están vacías y después de los archivos, de abajo arriba. Si el origen contiene ambos tipos multimedia, solo se intentan borrar subcarpetas al seleccionar ambos tipos; si contiene uno solo, al seleccionarlo.

La operación de duplicados de Windows reconoce únicamente el sufijo " (n)" antes de la extensión. Compara los candidatos con el original sin número o, si este no existe, con el candidato de número más bajo. Solo mueve las coincidencias SHA-256 tras confirmación y una nueva verificación; conserva el archivo de referencia y los candidatos no coincidentes o no verificables. El destino debe estar fuera del origen, y las colisiones reciben un sufijo incremental sin sobrescribir.

Los registros de actividad se guardan en `%LOCALAPPDATA%\FileHandler\logs\filehandler.log`. Las operaciones restantes están detalladas en el [roadmap](./ROADMAP.md).

Para compilar y ejecutar las pruebas desde la raíz del repositorio se necesita el SDK de .NET 8:

```powershell
dotnet build .\src\FileHandler\FileHandler.csproj
dotnet test .\tests\FileHandler.Tests\FileHandler.Tests.csproj
```

El plan de entregas y su estado se mantiene en [ROADMAP.md](./ROADMAP.md).