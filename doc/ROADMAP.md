# Roadmap de FileHandler 2.0

Este plan convierte los requisitos de [FileHandler.md](./FileHandler.md) en entregas pequeñas y comprobables. Cada hito se cerrará con compilación y pruebas pertinentes; informaré del avance y crearé un commit cuando el cambio forme una unidad coherente.

## Hitos

### 0. Base técnica y primera operación — completado
- Aplicación de escritorio WPF en C#/.NET 8.
- Primera versión funcional de "Organizar por fecha", con selección de carpetas, progreso, registro de actividad y protección frente a sobrescrituras.
- Pruebas iniciales de fechas, colisiones y rutas.
- Verificación: compilación Release y 4 pruebas superadas.

### 1. Navegación y experiencia común — completado
- Añadida una pantalla principal para elegir una operación y abrir su flujo correspondiente.
- La selección de origen y destino permanece dentro del flujo de "Organizar por fecha".
- La operación mantiene el progreso, la salida y los errores visibles sin bloquear la interfaz.
- Las tres operaciones pendientes se identifican claramente y no se pueden iniciar.

Verificación: compilación Release y 4 pruebas superadas.

### 2. Escaneo y copia de multimedia — completado
- Escaneo recursivo con recuento separado de imágenes, vídeos y subcarpetas; extensiones acordadas comparadas sin distinguir mayúsculas.
- Selección independiente de tipos y destinos, con confirmación antes de copiar.
- Comparación SHA-256 ante colisiones; el archivo entrante recibe un sufijo incremental tanto si el contenido es igual como si es distinto.
- El origen se conserva; la eliminación se reserva para el siguiente hito.

Verificación: compilación Release y 8 pruebas superadas.

### 3. Limpieza opcional del origen tras la copia — completado
- Permite elegir qué tipos de originales borrar y confirma los archivos y el posible borrado de subcarpetas.
- Solo se eliminan fuentes cuya copia terminó correctamente y cuyos hashes SHA-256 siguen coincidiendo.
- Los archivos fallidos, ausentes o modificados se conservan; las carpetas no vacías nunca se eliminan.
- Las carpetas vacías se procesan de abajo arriba y solo cuando se seleccionan todos los tipos presentes, según la regla definida.

Verificación: compilación Release y 11 pruebas superadas.

### 4. Búsqueda y revisión de duplicados multimedia — completado
- Escanea imágenes y vídeos reconocidos y encuentra contenido idéntico por tamaño y SHA-256, aunque los nombres sean distintos.
- Muestra grupos revisables con rutas, tipo y miniaturas de imágenes o vídeos cuando Windows las proporciona.
- Presenta todos los archivos en una tabla única, ordenados consecutivamente por grupo y con el resumen del grupo en cada fila; marca automáticamente para la Papelera todas las copias salvo la primera ruta y permite desmarcar archivos que se quieran conservar.
- Requiere confirmación; conserva al menos una copia por grupo y vuelve a verificar antes de procesar.
- Si se marca todo un grupo, bloquea el envío y resalta en rojo el aviso y las filas afectadas.
- Registra errores y cambios detectados desde el escaneo; tras procesar una selección, vuelve a buscar automáticamente para mostrar los duplicados restantes.

Verificación: compilación Release y 26 pruebas superadas; revisión manual con datos de prueba (3 imágenes, 2 vídeos, 2 grupos idénticos y 1 archivo no multimedia ignorado).

### 5. Extracción de archivos de subcarpetas — completado
- Escanea recursivamente y mueve a la raíz del destino solo los archivos que provienen de subcarpetas; los que están en la raíz del origen no se alteran.
- Si un archivo ya está directamente en la raíz del destino, lo conserva allí; no elimina carpetas que queden vacías.
- Ante cualquier colisión, conserva el archivo existente y renombra el entrante con un sufijo incremental, tanto si el contenido coincide como si difiere.
- Incluye selección de origen/destino, confirmación, progreso, actividad y resumen de movimientos, colisiones y errores.

Verificación: compilación Release y 19 pruebas superadas.

### 6. Unificación de la organización multimedia — completado
- Sustituye las opciones separadas de organizar por fecha y copiar multimedia por una única operación.
- Usa un origen común; permite seleccionar imágenes, vídeos o ambos, con un destino por tipo que puede coincidir.
- Mueve los tipos seleccionados a carpetas `Año/NN-Mes`; los archivos no multimedia permanecen en origen.
- Renombra colisiones sin sobrescribir. Entre volúmenes o ante un movimiento directo fallido, verifica por SHA-256 la copia antes de borrar el origen.
- Mantiene progreso, confirmación, registro de actividad y resumen por tipo.

Verificación: compilación Release y 26 pruebas superadas.

### Mejora visual previa a la distribución: splash screen — completada
- Muestra FileHandler, la versión leída del ensamblado y el copyright © jpmDevelope al iniciar.
- Se mantiene 2,5 segundos con una barra de progreso simulada y después da paso a la ventana principal.

### Mejora visual de la pantalla principal — completada
- Sustituye la etiqueta «DISPONIBLE» por un icono vectorial en cada operación.
- Explica con más detalle la organización multimedia y el movimiento de duplicados verificados; conserva la descripción de extracción de subcarpetas.
- Añade una barra inferior fija con la fecha, el nombre de la aplicación y un enlace de contacto a jpm.develope@gmail.com.

Verificación de las mejoras visuales: compilación Release sin advertencias ni errores, 26 pruebas superadas y revisión manual del splash y la pantalla principal en Windows.

### 7. Validación y distribución — en curso
- Batería completa Release superada (32 pruebas) y pantallas de las tres operaciones revisadas manualmente en Windows.
- La búsqueda de duplicados se comprobó en Windows con archivos de prueba: los archivos multimedia reconocidos se agrupan por contenido idéntico, independientemente de sus nombres.
- La batería cubre rutas de origen superiores a 260 caracteres, archivos bloqueados al organizar/copiar/extraer, destinos sin permiso de escritura y colisiones de nombres.
- Preparar ambas entregas: una versión portable autocontenida y un instalador de Windows.
- Generar la versión final tras completar las validaciones y revisar ambas entregas.

## Criterios comunes de cierre

- Los procesos largos informan de su avance y dejan un registro legible.
- Los errores se muestran; no se convierten en resultados aparentemente exitosos.
- No se sobrescriben archivos ni se borran datos sin una política explícita y confirmación cuando corresponda.
- Cada operación cuenta con pruebas de éxito y de sus casos de riesgo más relevantes.
- Una entrega no se considera terminada hasta que sus pruebas y compilación pasan.

## Seguimiento

En cada actualización indicaré el hito en curso, lo completado, la validación realizada y el siguiente paso. Agruparé en un commit los cambios de cada entrega coherente, incluyendo las pruebas y la documentación directamente relacionadas.
