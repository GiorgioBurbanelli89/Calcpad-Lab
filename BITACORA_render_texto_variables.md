# Bitácora — Render de texto + variables al estilo Calcpad (Hekatan Lab)

Frente: hacer que Hekatan Lab renderice el reporte igual que Calcpad (texto antes/después de
las variables, tipografía matemática, sustitución de valores, export LaTeX), respetando la
**regla MATLAB**: en el código la variable va primero; las reglas de colocación del texto viven
en el comentario `%` (que MATLAB 2017a ignora) → el `.m` corre idéntico en MATLAB.

Archivos del motor tocados: `Symbolic.Core/Matlab/MatlabPipeline.cs`,
`MatlabHtmlWriter.cs`, `MatlabEvaluator.cs`, `MatlabPlots.cs`, `MatlabLatexWriter.cs` (nuevo).

## Avances (qué quedó funcionando, verificado por --shot PNG)

1. **Placeholder `@` en comentario inline** (`a = 6  %' Dimensiones: @ m,`): marca DÓNDE va la
   ecuación dentro del texto → texto ANTES, DESPUÉS o mezclado con la variable. Implementado en
   `MatlabPipeline.cs` rama `isInlineComment`: parte el texto en `@` e inserta pre/post alrededor
   del `<p class="line">` de la ecuación.
2. **`@nombre` / `@{expr}` en líneas `%'` independientes**: referencia variables ya definidas y
   renderiza «nombre = valor». Varias en una misma línea (`%' Slab - @a m, @b m`) → una fila.
   `RenderInlineVarRefs` + `RenderVarRefEq` (lookup `Globals.Vars`; `Eval` para `@{expr}`).
3. **Pre-cálculo** (`BuildPreviewVars` + `IsSafeScalarRhs`): una pre-pasada SEGURA evalúa solo
   asignaciones escalares de aritmética pura → `@name` funciona AUNQUE la línea de texto vaya
   ANTES de definir la variable. No ejecuta gráficas/fprintf/FEM (cero efectos, no dobla tiempos).
4. **Título `%"` negro centrado negrita** (antes verde `#1a7a4c` = off-brand). Acepta prefijos
   `%"<` / `%">` / `%"|` para subtítulos alineados. Marca Hekatan: paraguas oro, Lab rojo.
5. **Auto-tipografía en prosa** (`ConvertPrimes`, `ConvertSubscripts`): `N''`→N″, `N_1`→N₁,
   `J_2`→J₂, `K_e`→Kₑ dentro del TEXTO, no solo en ecuaciones. Con lookahead para no romper
   `Poisson's`, transpuesta ni `snake_case`/`E_suelo1`.
6. **Separador `|` entre elementos de vector** cuando hay varias columnas y son largos, para
   distinguir dónde empieza y termina cada elemento (pedido de Jorge).
7. **Paso de sustitución** (`SubstituteValues` en `MatlabHtmlWriter`): `n_e = n_a·n_b = 6·4 = 24`.
8. **Export LaTeX** `--tex` (`MatlabLatexWriter.cs`): ecuaciones + valores a `.tex`.
9. **Funciones de texto MATLAB añadidas** (`MatlabEvaluator.cs`): `str2double`, `int2str`,
   `erase`, `deblank`, `blanks`, `pad`.
10. **Ejemplos convertidos a estilo nativo** (sin disp/fprintf como 1ª opción):
    `Examples-Lab/18 FEA Slab/rectangular_slab_bfs.m`, `viga_simbolico_a_numerico.m`,
    `talud_t6_academico.m`.

## Complicaciones (callejones ya medidos — no repetir)

- **`#cols` con celdas `'texto`**: char-array sin cerrar → tumba el parseo de TODO el archivo.
  Medida: usar encabezados en la directiva (`% #cols Largo a | Ancho b`) y celdas de solo
  variables/números.
- **`#noc $Integral{...@ A}`** renderiza literal sin límites de área. Medida: para el ∫ de área
  usar HTML crudo `<div>K<sub>e</sub> = &#8747;<sub>A</sub> …</div>`. Las fórmulas algebraicas
  (`K_0=1-sin(phi)`, `f=sqrt(J_2)+alpha*I_1-k`) sí van bien con `% #noc`.
- **Primes literales**: `N''` mostraba apóstrofes. Causa: en el camino `disp` la variable se
  tipografía primero y los apóstrofes quedan tras las etiquetas. Medida: aplicar `ConvertPrimes`
  ANTES del typeset y en ambos puntos de flush.
- **`dNdxi`, `Npp` no renderizaban**: nombres sin tokens. Medida: renombrar a `N_xi`/`N_eta`
  (subíndice+griega) y `Npprime` (→ N″). La variable DEBE usar tokens para verse bien.
- **`struct('pf',…)` crash en `--shot`**: no eran cells; la causa era `talud_update(pf,[])`
  manual donde `gcf`/ancestro no resuelven la figura en headless. Medida: pintar el estado inicial
  inline (sin invocar el callback) + fallback `fig=gcbf`.

## Corroboración GEO5 Demo04 (talud) — cerrada 2026-08-11

Reporte GEO5 `Document.txt` vs Hekatan Lab vs Abaqus (malla T6 idéntica, 628 nodos):

| Herramienta | Método | FS |
|---|---|---|
| GEO5-FEM (ref) | DP + SRM c-φ, tangente analítica | **1.69** (etapa 2 c/sobrecarga = 1.48) |
| Abaqus/Standard | Drucker-Prager + SRM | ≈**1.68** (converge 1.675, diverge >1.70) |
| Hekatan Lab | MC-Clausen return-map + SRM | **1.63** (converge 1.60, diverge 1.62) |

Los tres dentro de ~4 %. Ajustes del solver GEO5 (Newton-Raphson, rigidez cada iteración,
reduce c-φ, tolerancias 0.01, return-map 0.001) coinciden con los de Hekatan. Desplazamiento:
GEO5 usa EDEF≈130 MPa (no el E=21/300, que es solo para el FS) — etapa 1 clava RMSE 0.061 mm.
Abaqus(21/300)==Hekatan(21/300) bit a bit valida la fórmula FEM. Abierto: la versión académica
`talud_t6_completo.m` usa SRM simplificado y toca techo FS=2.0; el motor bueno es `demo04_mc_srm.m`.

## Estado
- Motor compilado y publicado; instalador **Hekatan-Lab-Setup-1.1.1.exe**.
- Ejemplos verificados por `--shot`.
