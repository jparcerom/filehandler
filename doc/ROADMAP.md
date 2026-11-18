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

### 3. Limpieza opcional del origen tras la copia — siguiente
- Permitir elegir qué tipos de origen borrar después de la copia.
- Mostrar una confirmación explícita que indique los tipos y si se intentarán eliminar subcarpetas.
- Eliminar únicamente archivos seleccionados y carpetas vacías, recorriendo estas últimas de abajo arriba.
- Probar que una copia fallida o incompleta no provoque un borrado accidental.

### 4. Detección y movimiento de duplicados de Windows
- Identificar nombres con el sufijo de duplicado de Windows y localizar el archivo original correspondiente.
- Verificar que original y duplicado tienen el mismo contenido antes de mover.
- Registrar los casos no verificados y resolver colisiones en destino sin sobrescrituras.

### 5. Extracción de archivos de subcarpetas
- Mover archivos de subcarpetas a la raíz del destino.
- Antes de implementar, fijar el comportamiento ante nombres iguales con contenido igual o distinto.
- Informar de movimientos, omisiones y errores; no modificar archivos sin una política de colisión definida.

### 6. Validación y distribución
- Ejecutar la batería completa de pruebas y una revisión manual de los flujos en Windows.
- Verificar el comportamiento con rutas largas, permisos insuficientes, archivos bloqueados y destinos con colisiones.
- Elegir y documentar el formato de entrega (ejecutable portable o instalador) antes de generar la versión final.

## Criterios comunes de cierre

- Los procesos largos informan de su avance y dejan un registro legible.
- Los errores se muestran; no se convierten en resultados aparentemente exitosos.
- No se sobrescriben archivos ni se borran datos sin una política explícita y confirmación cuando corresponda.
- Cada operación cuenta con pruebas de éxito y de sus casos de riesgo más relevantes.
- Una entrega no se considera terminada hasta que sus pruebas y compilación pasan.

## Seguimiento

En cada actualización indicaré el hito en curso, lo completado, la validación realizada y el siguiente paso. Agruparé en un commit los cambios de cada entrega coherente, incluyendo las pruebas y la documentación directamente relacionadas.
