# Calcpad Lab

**MATLAB-syntax scientific worksheets** with the same WPF + CLI experience as
Calcpad Symbolic, but the parser reads pure `.m` files instead of `.cpd`.

Fork of [Calcpad Symbolic](../Calcpad-Symbolic/) — same template, same CSS-pure
math rendering, same auto-run, same WPF/CLI/PDF/DOCX pipeline. The only change
is the **source syntax**: MATLAB instead of Calcpad worksheet markup.

## Estado actual — v1 (Syntactic complete)

✅ **Parser MATLAB-aware nativo** (sin transpiler externo, sin MATLAB subprocess):

| MATLAB construct | Calcpad Lab support |
|---|---|
| `for i = 1:n ... end` | ✅ bare keyword, stack contextual `end` |
| `if cond ... elseif ... else ... end` | ✅ |
| `while cond ... end` | ✅ |
| `function out = f(args) ... end` | ✅ (registro + dispatch) |
| `break`, `continue` | ✅ |
| `% comment` | ✅ full-line + inline → text token |
| `%% Section Header` | ✅ → `<h2>...</h2>` |
| `;` suppression | ✅ statement no se muestra |
| `a = 1; b = 2; c = 3;` | ✅ split a 3 líneas |
| `...` line continuation | ✅ une líneas |
| `f(a, b, c)` | ✅ → `f(a; b; c)` (sep Calcpad) |
| `[1, 2, 3]` | ✅ → `[1; 2; 3]` (vector Calcpad) |
| `[a, b; c, d]` | ✅ → `[a; b\|c; d]` (matriz 2D Calcpad) |
| `eye(n)`, `inv(A)`, `transpose(A)` | ✅ aliases → identity/inverse/transp |
| `zeros(m, n)` | ✅ → matrix(m; n) |
| `zeros(1, n)`, `zeros(n, 1)` | ✅ → vector(n) (1D verdadero) |
| `clear`, `clc` | ✅ no-op (comment) |
| `disp(x)`, `fprintf(...)` | ✅ shims (text output) |
| `sqrt`, `abs`, `sin`, `cos`, `log`, `exp`, `min`, `max` | ✅ ya en Calcpad |
| `M(i, j) = expr` | ✅ sintaxis → `M.(i; j) = expr` |
| `M(i, j)` (lectura) | ✅ → `M.(i; j)` para reads |
| File `.m` | ✅ Open dialog en WPF + CLI |
| Autorun | ✅ heredado de Calcpad Symbolic |
| HTML output con template Calcpad | ✅ idéntico, sólo title cambia a "Calcpad Lab" |

✅ **Template HTML**: idéntico a Calcpad Symbolic — CSS puro, sin KaTeX/MathJax.

✅ **Autorun**: al abrir un `.m` en la WPF, se ejecuta automáticamente.
En CLI sin flag `-s` abre el HTML en el navegador.

## Limitación conocida — v1

⚠️ **Calcpad es declarativo (worksheet), no imperativo**. Las asignaciones
indexadas a matrices `M(i, j) = x` se **RENDERIZAN** con notación matemática
hermosa (`M₁,₁ = 10`) pero **no persisten** valor (Calcpad evalúa cada línea
como una ecuación independiente, no como un estado mutable).

**Impacto**: scripts MATLAB que hacen FEM manual (ensamblaje de K acumulativo)
no completan el cálculo numérico. La solución arquitectónica está en la v2.

## Roadmap — v2

### Fase 2: JIT loops + Math.NET Numerics (~1 semana)
- Compilar loop bodies a `Expression<Func<...>>` → IL nativo
- Delegar matriz×matriz, inv, eig a Math.NET (LAPACK/BLAS nativo)

### Fase 3: C++ kernel (Eigen / MFEM) vía P/Invoke (~2 semanas)
- Resuelve la limitación declarativa (mutación imperativa en C++)
- `fem.plateQ4Solve(...)`, `fem.modalAnalysis(...)`, etc. como intrínsecas
- Reutiliza solvers ya validados de hekatan-fem
- Speed C++ nativa (igual a MATLAB)

## Demo que funciona en v1

`Examples/Calcpad-Lab/plate_thin_demo.m` — Plate thin con solución analítica
Navier + Reissner. Renderiza con notación matemática profesional (fracciones,
exponentes, subíndices) sin errores.

## Comparación con Calcpad Symbolic

| Aspecto | Calcpad Symbolic | Calcpad Lab |
|---|---|---|
| Sintaxis input | `.cpd` | `.m` (MATLAB) |
| Headings | `'***` | `%%` |
| Comments | `'comment` | `% comment` |
| Suppression | (siempre muestra) | `;` al final oculta |
| For loop | `#for i = 1:n ... #loop` | `for i = 1:n ... end` |
| If | `#if cond ... #end if` | `if cond ... end` |
| Args fn | `f(a; b)` | `f(a, b)` (auto-traduce) |
| Matrix | `[a; b\|c; d]` | `[a, b; c, d]` (auto-traduce) |
| Indexing | `M.(i; j)` | `M(i, j)` (auto-traduce) |
| Template HTML | mismo | mismo (title "Calcpad Lab") |
| WPF / CLI | mismos | mismos |
| Autorun | sí | sí |

## Cómo correr

```bash
Cli.exe "script.m output.html"      # → HTML + abre browser
Cli.exe "script.m output.html -s"   # silent
Cli.exe "script.m output.pdf"       # PDF
Cli.exe "script.m output.docx"      # Word

# WPF: File → Open → script.m
```

## Arquitectura

```
.m file
  ↓
MatlabPreprocessor (Symbolic.Core/Parsers/)
  ├── MergeMatlabContinuations    ('...')
  ├── TransformShims              (clear/clc/disp/fprintf)
  ├── TransformZerosOnesToVector  (zeros(1,n) → vector(n))
  ├── TransformDelimiters         ('(a, b)' → '(a; b)';  '[a; b]' → '[a|b]')
  ├── TransformIndexedAssignment  (M(i,j) = x → M.(i; j) = x)
  └── TransformMatrixReads        (M(i,j) inline → M.(i; j))
  ↓
ExpressionParser (Calcpad, modificado)
  ├── GetMatlabBareKeyword        (for/if/while/end → Keyword.For/...)
  ├── % comments en GetTokens
  ├── ; suppression en Parse
  └── %% Markdown headings
  ↓
HTML con template.html (CSS puro)
  ↓
Converter → file + UseShellExecute → browser
```

## Para más detalle ver `validacion/` en hekatan-struct
