# Bitácora — Funciones como matemática de libro + jacobian + fixes

## Objetivo
Regla de Jorge: "si MATLAB lo hace, Hekatan lo hace" y **ninguna función en texto plano**
—todo como expresión matemática de libro—. Además, responder a un comentario de LinkedIn
("¿deduce? los polinomios los ingresó el usuario") con un ejemplo que **DEDUCE** las
funciones de forma en vez de teclearlas.

## Render de funciones como matemática (no "texto plano" morado de código)
`SymFunc.ToHtml` reescrito: toda función simbólica sale como matemática de libro.
- `abs→|x|`, `norm→‖x‖`, `log→ln`, `log2→log₂`, `log10→log₁₀`, `sign→sgn`,
  `sqrt→√`, `exp→e^x`, `heaviside→H`, `dirac→δ`; trig/hiperbólicas en **romano serif**
  (Cambria Math), NO en el sans-serif morado de código. Verificado con estilos computados
  (font Cambria Math, italic normal, color negro).
- Igual criterio en la ruta NO simbólica del writer (eco de `y=sin(x)` numérico).

### Bug de motor (grave) cazado y arreglado
`MapSymUnary` **adivinaba** el nombre de la función evaluando el delegado en x=0,1 (heurística
frágil). Fallaba: `abs`/`sign` → `√` (colisión con sqrt); `log2`/`log10` → `log` (perdían la
base); funciones sin patrón → `f(...)`. Arreglo: **pasar el nombre EXPLÍCITO** por
`MapUnary`/`MapUnaryVml` (regla "no adivinar, extraer"). El nombre ya se conocía al registrar
el builtin y se estaba tirando.

## jacobian — MATLAB lo tiene (simbólico), Hekatan también, y ahora se RENDERIZA como cálculo
- `jacobian(f, v)` → `∂/∂(v) f` (notación de cálculo, no "jacobian(...)" en texto). También
  `hessian→∂²/∂v²`, `curl→∇×`, `divergence→∇·`, `laplacian→∇²`. Registrados en
  `IsSymbolicFunctionCall` para que se echen como `fórmula = resultado`.
- Verificado simbólico Y numérico: `J = jacobian([x^2*y; 5x+sin y],[x y])` = `[2xy x²; 5 cos y]`;
  numérico correcto vía `double(subs(...))`.

### Bug de motor: `double()` de matriz simbólica TRANSPONÍA
`_builtins["double"]` escribía `data[c*rr+r]` (columna-mayor) pero `MValue.Data` es
**fila-mayor** (`Data[r*Cols+c]`). `double(subs([1 2;3 x],x,9))` daba `[[1,3],[2,9]]`.
Arreglo: `data[r*cc+c]`. Afecta a cualquier matriz simbólica→numérica (incl. jacobian numérico).

## Branding: el reporte del CLI decía "Calcpad Lab"
`Symbolic.Cli/doc/template{,.bg,.zh}.html` decían `<title>Created with Calcpad Lab</title>`
→ **Hekatan Lab** (el WPF ya estaba bien).

## Ejemplos (deducción simbólica genuina — responden al comentario de LinkedIn)
- **`Examples-Lab/06 Calculo Simbolico/Funciones_de_Forma_Deducidas.m`** (NUEVO): SOLO la
  deducción simbólica. De una cúbica genérica `w=c₀+c₁x+c₂x²+c₃x³` + 4 condiciones nodales
  (`subs`), arma `C=jacobian(u,c)`, invierte, y las funciones de forma de Hermite **salen**
  de `mon·C⁻¹`. Nadie teclea los polinomios.
- **`Examples-Lab/18 FEA Slab/viga_simbolico_a_numerico.m`** (MODIFICADO): mismo planteamiento
  deducido → K simbólica → K numérica → deflexión `−121.909638 mm` = teórica (exacto).

Verificado renderizando con el CLI y mirando (texto + estilos computados + PNG parcial).
Pendiente: republicar WPF + regenerar instalador con estos arreglos.
