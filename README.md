# Calcpad Lab

**MATLAB-syntax scientific worksheets** — same WPF + CLI experience as
[Calcpad](https://calcpad.eu/) but the parser reads pure `.m` files instead
of `.cpd`. Native MATLAB engine in C#, **no MATLAB installation required**.

> Up to **3× faster than MATLAB R2017a** on equivalent FEM scripts.
> Same renderized HTML/PDF/DOCX output as Calcpad, same auto-run-on-save,
> same template — only the input syntax is MATLAB.

📥 **Download v1.0.5:** [Calcpad-Lab-Setup-1.0.5.exe](https://github.com/GiorgioBurbanelli89/Calcpad-Lab/releases/download/v1.0.5/Calcpad-Lab-Setup-1.0.5.exe) (68 MB, self-contained, no .NET required)
🎬 **Video demo:** https://youtu.be/-Xcyc2SsG7s
📁 **107 ejemplos `.m`** en 18 categorías bundleadas con el installer.

---

## Why Calcpad Lab?

Calcpad oficial is excellent for engineering math with its native equation
rendering, but its `.cpd` syntax has a steep learning curve for engineers
coming from MATLAB / Python / Julia. **Calcpad Lab keeps all the visual
strengths of Calcpad (rendered formulas, auto-run, PDF/Word export, plots
inline)** and replaces the input syntax with **standard MATLAB**.

You write:

```matlab
%% Datos
a = 6
b = 4
t = 0.1
E = 35e6
nu = 0.15

%% FEM
D11 = E*t^3/(12*(1-nu^2))
D = D11 * [1, nu, 0; nu, 1, 0; 0, 0, (1-nu)/2]
```

And you get the same beautifully-rendered HTML/PDF as Calcpad.

---

## Highlights

- **Native MATLAB parser** in C# — no transpiler, no `octave-cli`, no MATLAB
  subprocess. Open `.m` files directly.
- **12,000+ lines of code** in `Symbolic.Core/Matlab/` — pure tokenizer +
  parser + evaluator + HTML writer.
- **500+ MATLAB builtins**: `zeros`, `eye`, `inv`, `solve` (`A\b`), `det`,
  `transpose`, `eig`, `sin/cos/exp/log`, `min/max/sum/prod`, `plot`, `surf`,
  `patch`, `trisurf`, `mesh`, `imagesc`, `contour`, …
- **Full control flow**: `for`/`while`/`if-elseif-else`/`switch`/`break`/
  `continue`/`function ... end`.
- **OOP**: `classdef` with properties, methods, constructors, multiple
  return values.
- **Symbolic algebra** via AngouriMath: `syms`, `simplify`, `expand`,
  `solve`, `diff`, `int`, `subs`, `dsolve` (ODEs).
- **Cell arrays + string arrays + structs**.
- **Inline plotting** with MATLAB-style `figure` / `plot` / `surf` that
  saves PNG/SVG to the auto-rendered HTML.
- **Auto-run on save** — like Calcpad, HTML updates instantly as you type.

---

## Quick start (Windows installer)

1. Descargar **[Calcpad-Lab-Setup-1.0.5.exe](https://github.com/GiorgioBurbanelli89/Calcpad-Lab/releases/download/v1.0.5/Calcpad-Lab-Setup-1.0.5.exe)** desde
   [Releases](https://github.com/GiorgioBurbanelli89/Calcpad-Lab/releases).
2. Doble-click → aceptar UAC → seguir el wizard (acepta asociación `.m` para abrir scripts con doble-click).
3. Al primer arranque, los **107 ejemplos** se copian a `Documents\Calcpad-Lab\Examples\`.
4. Abrir cualquier `.m` (`Ctrl+O`) o crear uno nuevo (`Ctrl+N`) y `F9` para ejecutar.

**No requiere .NET Desktop Runtime** — el runtime .NET 10 viaja dentro del installer (self-contained).

CLI usage:

```bash
CalcpadLabCli.exe my_script.m html -s   # generate HTML output
CalcpadLabCli.exe my_script.m pdf       # generate PDF
```

## Build from source

Requires **.NET 10 SDK**.

```bash
git clone https://github.com/GiorgioBurbanelli89/Calcpad-Lab.git
cd Calcpad-Lab
dotnet build Symbolic.Wpf/Symbolic.Wpf.csproj -c Release
dotnet build Symbolic.Cli/Symbolic.Cli.csproj -c Release
```

Self-contained portable (Windows x64):

```bash
dotnet publish Symbolic.Wpf/Symbolic.Wpf.csproj \
  -c Release -r win-x64 --self-contained true \
  -o ./publish/CalcpadLab
```

---

## Repository structure

```
Symbolic.Core/
├── Matlab/              ← native MATLAB parser + evaluator (12 kLoC)
│   ├── MatlabTokenizer.cs
│   ├── MatlabParser.cs
│   ├── MatlabEvaluator.cs
│   ├── MatlabHtmlWriter.cs
│   └── MatlabPipeline.cs
└── ...                  ← Calcpad-Symbolic core (math + plotting)

Symbolic.Wpf/            ← WPF GUI (WebView2 hot reload)
Symbolic.Cli/            ← command-line interface
Symbolic.Api/PyCalcpad/  ← Python bindings (optional)

Examples/
├── Algebra Lineal/      ← vectors, matrices, eigenvalues, SVD
├── FEM/                 ← Kirchhoff Q4-BFS, MITC4, DSE, Batoz DKQ
├── Cálculo/             ← derivatives, integrals, ODEs
└── Demos/               ← OOP, dynamic systems, control
```

---

## FEM benchmarks

Calcpad Lab is the validation engine for
[Hekatan Struct](https://github.com/GiorgioBurbanelli89/hekatan-struct), a
browser-based structural analysis platform. Cross-validated against
SAP 2000 v24 via OAPI:

| Element | vs SAP 2000 |
|---|---|
| **Batoz DKQ** vs Plate-Thin | **0.00 % exact match** |
| **MITC4** (Dvorkin-Bathe 1985) vs Plate-Thick | -0.56 % deflection, +2.6 % Mxy |
| **BFS Q4** (16-DOF Bogner-Fox-Schmit 1965) | matches analytical Navier within 0.1 % |

See [hekatan-struct/validacion](https://github.com/GiorgioBurbanelli89/hekatan-struct/tree/main/validacion)
for the full cross-language benchmark (Python / Julia / C++ WASM / SAP API).

---

## ¿Por qué Calcpad-Lab para validar Hekatan Struct?

Hekatan Struct es la plataforma de análisis estructural en navegador. Su
validación numérica se hace contra **cuatro lenguajes en paralelo** — cada
uno entendido nativamente por ingenieros y por modelos de IA:

| Lenguaje | Rol en la validación |
|---|---|
| **MATLAB** (Calcpad-Lab) | Memoria de cálculo legible, render simbólico, comparación celda-por-celda |
| **Python** (NumPy / SciPy) | Scripts batch, integración con notebooks Jupyter |
| **Julia** | Solver rápido para FEM no lineal, tipos paramétricos |
| **C++ / WASM** (Eigen 3) | Solver de producción que corre en el browser |

La idea: si el mismo benchmark da el mismo resultado en los cuatro
lenguajes, la implementación es correcta. **La IA entiende cada uno de
estos lenguajes con fluidez**, lo que permite generar, revisar y debuggear
validaciones cruzadas mucho más rápido que con DSLs propietarios.

Calcpad-Lab es la pieza que cierra el ciclo MATLAB: te permite escribir
una memoria de cálculo legible (con prosa intercalada con ecuaciones
simbólicas renderizadas como en Calcpad) que sirve **al mismo tiempo como
documento técnico publicable y como caso de validación numérico**.

---

## What's new in v1.0.5

- **Render simbólico estilo Calcpad** — `char(M_max)` dentro de `fprintf`
  ahora sale con fracciones apiladas, variables en azul, subíndices
  (`R_A`, `sigma_adm`), superíndices (`x²`, `L⁴`) y unidades en verde
  (`kN·m`, `MPa`, `cm³`). Todo HTML+CSS puro, sin MathJax/KaTeX.
- **Texto plano se beautifica solo** — el `fprintf` con texto descriptivo
  como `'M_max = q*L^2/8 kN*m'` detecta variables, unidades y operadores
  matemáticos automáticamente.
- **Escape `''` arreglado** — `fprintf('don''t worry')`, `fprintf('y''(x)')`,
  todo el escape estándar MATLAB ahora funciona.
- **`fprintf(['a' 'b'])`** — concatenación de single-quoted strings ya no
  crashea (era IndexOutOfRange).
- **Self-contained installer** — no requiere .NET Desktop Runtime preinstalado.
- **107 ejemplos `.m`** en 18 categorías (cálculo simbólico, mecánica
  estructural, FEM, álgebra lineal, visualización, diseño de acero,
  ETABS, sismo).

---

## Acknowledgments

- Built on top of [Calcpad Symbolic](https://github.com/Proektsoftbg/Calcpad)
  (Ned Tomov, MIT license) — same renderer, same math engine.
- AngouriMath — symbolic algebra backend.
- Eigen 3 compiled to WASM for plate solvers (in hekatan-fem sister repo).

## License

MIT — same as upstream Calcpad.
