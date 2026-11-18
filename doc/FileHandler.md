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

Con está opción buscaremos en la carpeta origen y todas sus subcarpetas estos archivos y verificaremos que realmente son copia del archivo original, que tendrá el mismo nombre sin " (n)".

Todos estos duplicados se moverán a la carpeta que se seleccione como destino.

En este caso sí se moverán los archivos en vez de copiarlos, una vez movidos se revisarán manualmente para verificar que efectivamente siguen este formato en la carpeta destino.

## Sacar archivos de subcarpetas
Otra de las opciones es la de sacar todos los archivos de las subcarpetas de una carpeta origen a la raiz de una destino.

# Procesos
Cualquier proceso que se ejecute debe mostrar la salida y guardarlo en logs además de mostrar mientras se ejecuta una barra de progreso o un conteo de archivos procesados.

# Desarrollo
La aplicación de escritorio se desarrolla en C# con .NET 8 y WPF para Windows.

### Decisión para la versión 2.0
La primera operación implementada es "Organizar por fecha": recorre la carpeta de origen y sus subcarpetas, elige la fecha más antigua entre creación y modificación, y mueve los archivos a carpetas `Año/NN-Mes`. Si ya existe un elemento con el mismo nombre en el destino, el archivo se omite y se registra para evitar sobrescrituras.

Los registros de actividad se guardan en `%LOCALAPPDATA%\FileHandler\logs\filehandler.log`. Las demás operaciones descritas en este documento quedan pendientes de implementación.

Para compilar y ejecutar las pruebas desde la raíz del repositorio se necesita el SDK de .NET 8:

```powershell
dotnet build .\src\FileHandler\FileHandler.csproj
dotnet test .\tests\FileHandler.Tests\FileHandler.Tests.csproj
```

El plan de entregas y su estado se mantiene en [ROADMAP.md](./ROADMAP.md).