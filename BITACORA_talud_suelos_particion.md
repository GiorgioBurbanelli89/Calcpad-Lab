# Bitácora — Talud interactivo + suelos por partición, botón PNG, @{} en Markdown

## Talud (canvas `ginput`, `MatlabEvaluator.cs` → `HktDrawJs`)

- **Ctrl+Z real:** el foco de teclado nunca queda en el WebView2 (se queda en el editor). Se resolvió con un manejador a **nivel de ventana** en `MainWindow.xaml.cs` (`AddHandler(PreviewKeyDownEvent, …, handledEventsToo)`) que llama a `window.__hktUndo()`. Snapshot/restore de todo el estado (pts, cur, soils, base) para deshacer paso a paso.
- **Flujo en 2 fases:** Fase 1 traza el **borde** del talud (solo la línea); al pulsar "✔ OK: generar área" se **cierra y genera el área total** (relleno). Fase 2 divide en suelos.
- **Suelos por PARTICIÓN (no estratos arriba/abajo):** cada línea de contacto va **de un punto del borde a otro** (extremos enganchados al borde con `snapToPoly`), y **parte el dominio en regiones cerradas** (algoritmo `computeFaces`/`splitFace` sobre el polígono del dominio). Cada región = un suelo. Sin vacíos, colores distintos, etiqueta en el centroide de cada cara.
- **Snap al borde con resaltado:** al acercar el cursor a la superficie/lados/base, engancha exactamente sobre el borde (anillo naranja + "(en el borde)").
- **Etiqueta flotante en el cursor** (coordenadas + longitud), cotas, orto, clic derecho = Terminar.
- Suite de regresión del canvas: **17/17** (scratchpad `test_suite_talud.js`, Playwright headless extrayendo el JS real).

## Otros

- **Botón PNG** en la barra del Output (`MainWindow.xaml` + `ExportPngButton_Click`): exporta el render del WebView2 a PNG con diálogo. Reusa la captura de `--shot` (Page.captureScreenshot) sin cerrar la app.
- **`@{expr}` dentro de bloques Markdown** `% #md … % #endmd` (`MatlabPipeline.cs` → `SubstituteMdVarsPlain`): permite tablas/listas Markdown con valores en vivo, sin HTML crudo. Usado en el ejemplo de benchmark.
- **Ejemplo `matmul_benchmark.m`** (`Examples-Lab/00 MATLAB Patrones Basicos/`): réplica del test de matmul de Ganchovski, corre en el motor MATLAB (Intel MKL), tabla Markdown limpia.
- **`MatlabPlots.cs`:** cuadro de ejes 2D (showline/mirror/ticks) estilo MATLAB.

## Hallazgo importante — NO subir MKL a 2026.1

Se probó actualizar oneMKL 2024.2 → **2026.1** (descarga oficial, reemplazo de DLLs, recompilación). Resultado medido: **matmul igual**, pero el **reuso de factorización 4× MÁS LENTO** (t4 de `tests/numeric`: decomp 0.0121s→0.0484s, speedup 74×→17×). Los solvers FEM/estructurales viven de ese reuso → 2026.1 EMPEORA lo importante. **Revertido a 2024.2** (suite numérica 4/4 OK). Ver `reference_matlab_numeric_engine_libs` en memoria.
