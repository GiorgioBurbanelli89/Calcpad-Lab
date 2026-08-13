# Hekatan Symbolic — CLI

**Hekatan Symbolic** es el motor de **cálculo simbólico y numérico** de Hekatan (el mismo
que impulsa la app **Hekatan Lab**). Su CLI *headless* toma un archivo `.m` de **MATLAB puro**
y genera un **reporte HTML tipo libro** (memoria de cálculo), exportable a **PDF** y **Word
(.docx)**. El motor —parser MATLAB, evaluador, JIT, álgebra numérica sobre **oneMKL** y
**álgebra simbólica**— está escrito en C# y corre *in-process*: **no necesitás tener MATLAB
instalado**.

> El **mismo archivo `.m` corre idéntico en Hekatan Lab y en MATLAB 2017a**. Todo el formato
> del reporte vive en comentarios `%`, que MATLAB ignora — así nunca se rompe la compatibilidad.

---

## Uso

```bash
# Genera el reporte HTML a partir de un archivo .m
CalcpadLabCli.exe mi_calculo.m salida.html
```

El primer argumento es el `.m` de entrada; el segundo, el `.html` de salida. La app de
escritorio (WPF) exporta además a **PDF** y **Word**.

---

## El archivo `.m` (MATLAB)

Todo lo que va **fuera de `%`** se **ejecuta y se renderiza** como matemática de libro; todo
lo que va en `%` es comentario (MATLAB lo ignora) y en Hekatan Lab **da formato al reporte**.

### 1) Código real (se ejecuta y se renderiza)

```matlab
x = 2 + 3                     % x = 5
syms x
f  = (x + 1)^2 * (x - 2)      % f = (x+1)²·(x−2)
fe = expand(f)                % x³ − 3·x − 2
df = diff(f, x)               % d/dx f
F  = int(f, x, 0, 2)          % ∫₀² f dx  (integral definida)
r  = solve(x^2 - 5*x + 6, x)  % x = [2; 3]
```

| Escribís | Se ve |
|---|---|
| `sqrt(b^2 - 4*a*c)` | √(b²−4·a·c) |
| `exp(-3*t)` | e⁻³ᵗ |
| `abs(x)` · `log(x)` · `sign(x)` | \|x\| · ln(x) · sgn(x) |
| `diff(f, x)` · `int(f, x)` | d/dx f · ∫ f dx |
| `jacobian(u, [c_0; c_1])` | ∂/∂(c₀, c₁) u |
| `K\F` · `inv(K)*F` | K⁻¹·F |

### 2) Comentarios de formato (`%`)

| Escribís | Efecto |
|---|---|
| `%'texto` | texto visible (prosa del reporte) |
| `%"Título` | título |
| `%#md … %#endmd` | bloque Markdown |
| `%#deq sigma = P/A @@(2.1)` | ecuación numerada |
| `%#cen` · `%#col` · `%#pgb` | centrado · columnas · salto de página |

La referencia completa de directivas de formato está en el
[README principal](../README.md#el-lenguaje).

---

## Cálculo simbólico (MATLAB Symbolic Toolbox)

Exacto y renderizado como libro:

- **Cálculo**: `diff`, `int` (indefinida y definida), `limit`, `taylor`, `symsum`/`symprod`.
- **Álgebra**: `simplify`, `expand`, `factor`, `collect`, `subs`, `solve`, `dsolve`.
- **Matricial**: `jacobian`, `hessian`, `det`, `inv`, `poly2sym`/`sym2poly`.
- **Transformadas**: `laplace`/`ilaplace`, `fourier`/`ifourier`, `ztrans`/`iztrans`.

Las funciones se muestran como **notación matemática** (`abs→|x|`, `log→ln`, `log2→log₂`,
`sign→sgn`, `sqrt→√`, `exp→eˣ`, `jacobian→∂/∂v`), nunca como texto plano.

---

## Ejemplo

```matlab
%% Deducción simbólica de las funciones de forma de una viga
%' <h3>Las funciones de forma se CALCULAN, no se teclean</h3>
syms x L c_0 c_1 c_2 c_3 real
w  = c_0 + c_1*x + c_2*x^2 + c_3*x^3          % viga cúbica genérica
dw = diff(w, x)                               % giro
u  = [subs(w,x,0); subs(dw,x,0); subs(w,x,L); subs(dw,x,L)]  % 4 condiciones nodales
C  = jacobian(u, [c_0; c_1; c_2; c_3])        % matriz del sistema (deducida)
N  = simplify([1, x, x^2, x^3] * inv(C))      % funciones de forma de Hermite
```

Genera el reporte con `CalcpadLabCli.exe viga.m viga.html` y ábrelo en el navegador.

---

## Créditos

Hekatan Lab hereda la **base de render e interfaz** (plantilla HTML del reporte, panel WPF +
WebView2, estilos de math) del proyecto **[Calcpad](https://codeberg.org/proektsoft/Calcpad)**
de **Nedelcho Ganchovski / PROEKTSOFT EOOD** (licencia MIT) — el crédito de esa base es suyo.

El **intérprete de MATLAB** (tokenizer, parser, evaluador), el **JIT**, el **álgebra numérica
sobre oneMKL** y el **motor de cálculo simbólico (Hekatan Symbolic)** son desarrollo propio de
**Hekatan Engineers**. *Hekatan Symbolic* seguirá creciendo — se le dará continuidad a su tiempo.

---

## Licencia

Distribuido bajo licencia **MIT**. Ver `../LICENSE`. El crédito de la base de render/UI
corresponde a PROEKTSOFT EOOD® (Calcpad, MIT).
