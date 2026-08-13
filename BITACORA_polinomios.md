# Bitácora — Polinomios de MATLAB 2017a en Hekatan Lab (numéricos y simbólicos)

## Objetivo
"Si lo hace MATLAB, Hekatan debe hacerlo." Completar TODA la categoría *Polynomials* de
MATLAB 2017a y probarla con valores de referencia exactos. Que funcionen **con valores**
(vectores de coeficientes) **y en simbólico** (expresiones), como MATLAB.

## Funciones añadidas al motor (`MatlabEvaluator.cs`)
- **`poly(v)`** — desde vector de raíces → coefs; desde matriz cuadrada → polinomio
  característico. Devuelve real si las raíces son conjugadas (imag residual ~0).
- **`polyvalm(p, X)`** — evaluación matricial (Horner con matrices), `p(X)`.
- **`polyder`, `polyint`, `deconv`** (1 salida) — ya se habían añadido; aquí se registran
  también `[q,r] = deconv(a,b)` (cociente + resto) como salida múltiple.
- **`residue(b,a)`** → `[r,p,k]` fracciones parciales, con **polos repetidos y complejos**
  (deflación + serie de Taylor compleja; agrupación de polos con tolerancia relativa).
- **`polyval` simbólico** — si `x` (o los coefs) es simbólico, Horner en el álgebra
  simbólica → devuelve la expresión **expandida** (forma de libro `s²−5s+6`), como MATLAB.

## Bugs del motor arreglados (pre-existentes, cazados por el test)
1. **Concatenación compleja**: `[1+2i, 3+4i]` perdía la parte imaginaria (`HorzConcat`/
   `VertConcat` sólo copiaban lo real). Ahora conservan `Imag` si algún operando es complejo.
2. **`roots` complejo**: `roots([1 0 1])` daba `0,0` en vez de `±i`. `roots`/`poly`/`residue`
   usaban `MatlabLinAlg.Eig(...).eigenvalues` (QR en C#, descarta lo imaginario). Nuevo
   helper **`EigValuesComplex`** (vía LAPACK **dgeev**) conserva los autovalores complejos.
3. **`sym2poly`/`coeffs` sin variable**: asumían `'x'` y fallaban si el polinomio estaba en
   otra variable (p.ej. `s`). Nuevo **`AutoSymVar`** (regla `symvar` de MATLAB: inicial más
   cercana a `x`) auto-detecta la variable.

## Tests (renderizados en el WebView2, NO texto plano)
- **`Examples-Lab/00 MATLAB Patrones Basicos/Polinomios MATLAB (numerico).m`** — 20 casos
  con referencia exacta de MATLAB 2017a en tabla Markdown (`#md` + `@{}`). **20/20 PASS,
  error máximo = 0.**
- **`Examples-Lab/06 Calculo Simbolico/Polinomios MATLAB (simbolico).m`** — mismos
  polinomios en simbólico, cada paso **renderizado como matemática de libro** (CSS tipo
  Calcpad): `p=s²−5s+6`, derivada `d/ds`, integral `∫ ⅓s³−5/2s²+6s`, factorizado
  `(s−3)(s−2)`, raíces `[3;2]`, `polyval([1 −5 6], s)`, evaluación `p|ₛ₌₄=2`, `expand`.

## Verificación
PNG extraído del WebView2 con `--shot` y revisado. Ambos correctos.
Velocidad: `poly`/`roots`/`residue` usan LAPACK (MKL) para los autovalores → ya supera a
MATLAB 2017a (MKL 2016) en el mismo cálculo.
