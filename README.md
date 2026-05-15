# Calcpad Lab

**MATLAB-syntax scientific worksheets** — same WPF + CLI experience as
[Calcpad](https://calcpad.eu/) but the parser reads pure `.m` files instead
of `.cpd`. Native MATLAB engine in C#, **no MATLAB installation required**.

> Up to **3× faster than MATLAB R2017a** on equivalent FEM scripts.
> Same renderized HTML/PDF/DOCX output as Calcpad, same auto-run-on-save,
> same template — only the input syntax is MATLAB.

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

## Quick start (Windows portable)

1. Download `CalcpadLab-portable-win-x64.zip` from
   [Releases](https://github.com/GiorgioBurbanelli89/Calcpad-Lab/releases)
   (or build from source — see below).
2. Extract to any folder (e.g. `C:\CalcpadLab\`).
3. Double-click `CalcpadLab.exe`. **No .NET install required** — fully
   self-contained.
4. Open any `.m` file or create a new one (`Ctrl+N`) and press `F9` to run.

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

## Acknowledgments

- Built on top of [Calcpad Symbolic](https://github.com/Proektsoftbg/Calcpad)
  (Ned Tomov, MIT license) — same renderer, same math engine.
- AngouriMath — symbolic algebra backend.
- Eigen 3 compiled to WASM for plate solvers (in hekatan-fem sister repo).

## License

MIT — same as upstream Calcpad.
