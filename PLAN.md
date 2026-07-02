# Plan de desarrollo: ComicLayout Editor

Editor de escritorio para Windows que permite maquetar historietas: páginas A4,
viñetas dentro de las páginas, imágenes y bocadillos de texto dentro de las
viñetas. Guardar/recuperar proyectos e imprimir el resultado.

## Stack

- **Lenguaje/Framework:** C# .NET 8, WPF
- **Persistencia:** paquete `.comicproj` (ZIP) = `manifest.json` + carpeta `assets/`
- **Impresión/Exportación:** `System.Printing` / `FixedDocument` + exportación a PDF
- **Distribución:** `dotnet publish` self-contained, single-file, para `win-x64`

## Estructura de proyecto propuesta

```
ComicLayoutEditor/
  ComicLayoutEditor.sln
  src/
    ComicLayoutEditor.App/          # WPF UI
      Views/
      ViewModels/
      Controls/                     # PageCanvas, PanelControl, BalloonControl
      Converters/
    ComicLayoutEditor.Core/         # Modelo de datos, lógica de dominio
      Models/
      Serialization/
      Printing/
    ComicLayoutEditor.Tests/        # Tests unitarios
  README.md
```

## Modelo de datos (Core)

```
ComicDocument
  Title: string
  Pages: List<Page>

Page
  Id: Guid
  SizeMm: (210, 297)          # A4, orientación configurable
  Panels: List<Panel>

Panel
  Id: Guid
  Bounds: Rect                # posición/tamaño relativo a la página
  Rotation: double
  ZIndex: int
  Image: ImageRef?             # referencia a archivo en assets/
  ImageFit: enum (Cover, Contain, Stretch)
  Balloons: List<Balloon>

Balloon
  Id: Guid
  Bounds: Rect                 # posición/tamaño relativo al panel
  Shape: enum (Oval, Rounded, Rect, Thought, Shout)
  Text: string
  FontFamily, FontSize, TextAlign
  TailPoint: Point?             # punto del "piquito" del bocadillo

ImageRef
  RelativePath: string          # dentro de assets/
  OriginalFileName: string
```

Formato de archivo `.comicproj` (ZIP):
```
manifest.json     # serialización de ComicDocument (rutas relativas a assets/)
assets/
  img_0001.png
  img_0002.jpg
  ...
```

## Fases

### Fase 0 — Setup del proyecto
- [x] Crear solución .NET 8 con los 3 proyectos (App, Core, Tests)
- [x] Configurar WPF App con MVVM básico (sin librerías externas de entrada;
      usar `CommunityToolkit.Mvvm` para `ObservableObject`/`RelayCommand`)
- [x] Configurar `.gitignore`, `README.md` con instrucciones de build
- [x] Verificar que `dotnet build` y `dotnet run` funcionan con una ventana vacía

### Fase 1 — Modelo de dominio (Core)
- [x] Implementar clases `ComicDocument`, `Page`, `Panel`, `Balloon`, `ImageRef`
- [x] Implementar serialización a JSON (`System.Text.Json`) con `manifest.json`
- [x] Implementar guardado/carga del paquete ZIP (`.comicproj`):
  - Guardar: escribir manifest + copiar imágenes referenciadas a `assets/`
  - Cargar: extraer a carpeta temporal, deserializar, resolver rutas de imagen
- [x] Tests unitarios: round-trip de serialización (crear doc → guardar → cargar → comparar)

### Fase 2 — Lienzo de página y viñetas
- [x] Control `PageCanvas`: dibuja una página A4 a escala (con zoom; regla en mm
      pendiente para la Fase 7)
- [x] Control `PanelControl`: rectángulo redimensionable/movible (handles en
      esquinas y bordes, usando `Thumb` de WPF) que representa una viñeta
  - [x] Crear viñeta con arrastre del mouse (dibujar rectángulo nuevo)
  - [x] Mover/redimensionar viñeta existente (snap opcional a grid)
  - [x] Eliminar viñeta (tecla Supr / botón de barra) — menú contextual pendiente
  - [x] Reordenar Z-index (traer al frente / enviar atrás)
- [x] Selección múltiple y alineación básica (alinear bordes, distribuir)
- [x] Undo/Redo (pila de comandos simple sobre el ViewModel)

### Fase 3 — Imágenes dentro de viñetas
- [x] Importar imagen (drag&drop desde explorador + diálogo "Abrir archivo")
- [x] Copiar la imagen importada a la carpeta de trabajo del proyecto (assets/)
- [x] Ajuste de imagen dentro del panel: Cover / Contain / Stretch
- [x] Reposicionar/zoom de la imagen dentro del marco de la viñeta (pan/zoom interno: Alt+arrastrar / Alt+rueda)
- [x] Reemplazar o quitar imagen de una viñeta

### Fase 4 — Bocadillos de texto
- [x] Control `BalloonControl`: forma editable (óvalo, rectángulo redondeado,
      rectángulo, nube de pensamiento + grito) dibujada en un `Path`
- [x] Edición de texto inline (`TextBox` superpuesto, doble clic para editar)
- [x] Control de fuente, tamaño, alineación desde un panel de propiedades
- [x] Mover/redimensionar el bocadillo igual que una viñeta
- [x] "Piquito" del bocadillo: punto ajustable que apunta hacia el personaje

### Fase 5 — Gestión de páginas y documento
- [x] Panel lateral con miniaturas de páginas (añadir, duplicar, eliminar, reordenar)
- [x] Barra de propiedades contextual (según se seleccione página/viñeta/bocadillo)
- [x] Menú principal: Nuevo, Abrir, Guardar, Guardar como, Exportar, Imprimir
      (Exportar/Imprimir cablean su comando; implementación real en la Fase 6)
- [ ] Autoguardado / recuperación ante cierre inesperado (opcional, backlog)

### Fase 6 — Impresión y exportación
- [x] Implementar impresión con `PrintDialog` + `FixedDocument`, una página
      física por cada `Page` del documento, a tamaño A4 real (mm → DIU)
- [x] Exportar a PDF (rasterizado a alta resolución con `PDFsharp`, una hoja por
      página a su tamaño real en mm)
- [x] Exportar página(s) como imagen PNG de alta resolución (opcional)
- [x] Vista previa de impresión (`DocumentViewer`)

### Fase 7 — Pulido de UX
- [x] Atajos de teclado (Ctrl+S, Ctrl+Z/Y, Supr, flechas para mover con precisión;
      Shift+flechas = pasos de 10 px; Ctrl +/-/0 y Ctrl+P)
- [x] Zoom del lienzo (rueda del mouse + Ctrl, acercar/alejar, ajustar a ventana)
- [x] Guías/reglas en mm, rejilla visible y snapping con tamaño configurable
- [x] Manejo de errores: imagen no encontrada al abrir proyecto, archivo corrupto, etc.
- [x] Icono de la app y diálogo "Acerca de"

### Fase 8 — Empaquetado y distribución
- [x] Configurar `dotnet publish` self-contained, single-file, `win-x64`
      (ajustes en el `.csproj`, condicionados a que haya RuntimeIdentifier; basta
      `dotnet publish -c Release -r win-x64`). Resultado: un único `.exe` (~70 MB).
- [x] Probar el .exe generado: el ejecutable self-contained arranca usando su
      runtime embebido (no depende del .NET instalado). *Pendiente de tu parte:
      probarlo en una máquina realmente limpia sin .NET.*
- [x] (Opcional) Instalador con Inno Setup: script en `installer/ComicLayoutEditor.iss`
      (requiere Inno Setup para compilarlo; no imprescindible, el `.exe` ya es autónomo).
- [x] Documentar en README cómo compilar y generar el .exe

## Criterios de aceptación (MVP)

1. Crear un documento nuevo con al menos una página A4.
2. Añadir, mover, redimensionar y eliminar viñetas dentro de la página.
3. Insertar una imagen dentro de una viñeta y ajustarla (cover/contain).
4. Insertar un bocadillo de texto editable dentro de una viñeta, con forma y fuente configurables.
5. Guardar el proyecto en un archivo `.comicproj` y volver a abrirlo sin pérdida de datos.
6. Imprimir el documento y que cada página salga a tamaño A4 correcto.
7. Generar un `.exe` distribuible que corra en un Windows limpio sin instalar nada extra.

## Notas para Claude Code

- Priorizar que cada fase termine en un estado compilable y ejecutable
  (commits pequeños, verificables con `dotnet build`).
- Los controles de viñeta y bocadillo comparten mucha lógica de "elemento
  redimensionable/movible": considerar extraer una clase base o `Behavior`
  reutilizable (`ResizableThumbBehavior`) en vez de duplicar código.
- Mantener el Core (`ComicLayoutEditor.Core`) libre de referencias a WPF para
  que el modelo y la serialización sean testeables sin UI.
- Usar `CommunityToolkit.Mvvm` (NuGet) para reducir boilerplate de MVVM.
