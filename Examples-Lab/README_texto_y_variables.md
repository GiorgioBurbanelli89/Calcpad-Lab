# Texto y variables en Hekatan Lab (estilo Calcpad)

Guía de **todas** las formas de mezclar texto y variables en el render de Hekatan Lab, para
armar reportes que se lean **como un libro**. Todo esto vive en los comentarios (`%`), así que
el **mismo `.m` corre igual en MATLAB 2017a** — MATLAB los ignora, Hekatan Lab los renderiza.

---

## Regla de oro (MATLAB-safe)

> En el **código**, la variable va **primero**: `a = 6`. Las reglas de **cómo se ve** el texto
> viven dentro del `%`. Nunca se comenta el código ejecutable.

Por eso `%'comentario% a = 2` (comentario que "cierra" y deja código después) **no** se usa:
en MATLAB `%` comenta hasta el fin de línea y el `.m` dejaría de correr.

---

## 1. Comentarios: ocultos vs visibles

| Escribes | Qué hace |
|---|---|
| `a = 6  % nota privada` | comentario **OCULTO** (como MATLAB, no se renderiza) |
| `a = 6  %' texto` | comentario **VISIBLE** (el `'` = marcador de texto, estilo Calcpad) |
| `%' texto` (línea propia) | línea de **texto** visible |
| `%" Título` | **título** visible (ver §5) |
| `%%` | encabezado de sección (como MATLAB) |

---

## 2. El placeholder `@` — texto + variable en la MISMA línea de código

En el comentario visible de una línea de código, `@` marca **dónde cae la ecuación** dentro del texto.

| Escribes | Se renderiza |
|---|---|
| `a = 6      %' Lado de la losa: @ m` | Lado de la losa:  *a = 6*  m |
| `b = 4      %' El ancho @` | El ancho  *b = 4*   ← **texto antes** |
| `n_e = n_a*n_b  %' Total: @` | Total:  *n_e = n_a·n_b = 24* |
| `d = 2      %' @ es el espesor` | *d = 2*  es el espesor |
| `f = 35000  %' texto sin arroba` | *f = 35000*   texto   ← sin `@` = después |

---

## 3. `@nombre` y `@{expr}` — llamar variables dentro del texto

En una **línea `%'` (o `%"`) independiente**, `@nombre` inserta «nombre = valor» y `@{expr}`
evalúa una expresión. Sirve para **combinar** variables (definidas en líneas separadas, con
comentario oculto) en **una sola línea** visible.

```matlab
a = 6;   % oculto
b = 4;   % oculto
%' Slab dimensions - @a m, @b m
%' Área = @{a*b} m²
```
Se renderiza:
- Slab dimensions -  *a = 6*  m,  *b = 4*  m
- Área =  *a·b = 24*  m²

`@{expr}` acepta cualquier expresión escalar: `@{E*t^3/(12*(1-nu^2))*1000}` sale como **fracción**.

---

## 4. Pre-cálculo — usar la variable ANTES de definirla

La línea `%'` con `@nombre`/`@{expr}` puede ir **arriba**, aunque la variable se defina más abajo:

```matlab
%' Analizamos una losa de @a m por @b m, área @{a*b} m².
a = 6;
b = 4;
```
Funciona porque Hekatan Lab hace una **pre-pasada segura** que evalúa solo las asignaciones
**escalares de aritmética pura** (datos de entrada). **No** re-ejecuta gráficas ni el FEM (no dobla
el tiempo). Límite: si la variable no viene de una asignación escalar simple (p. ej. `a = f(...)`
o una matriz), no se puede adivinar antes de definirla; y si se reasigna, se usa el último valor.

---

## 5. Títulos y subtítulos (`%"`)

| Escribes | Resultado |
|---|---|
| `%" Título` | **título negro, centrado, negrita** (por defecto) |
| `%"< Subtítulo` | subtítulo a la **izquierda** (negrita/negro, sin centrar) |
| `%"> Subtítulo` | subtítulo a la **derecha** |
| `%"\| Título` | centrado explícito |
| `%"/< Subtítulo` | izquierda + itálica (combinables) |
| `%"{negro,izquierda,grande} …` | override fino (color/alineación/tamaño) |

Los títulos y subtítulos también aceptan `@nombre`/`@{expr}`.

---

## 6. Formato de texto (`%'`)

Prefijos combinables tras el `'` (o el `"`):

| Prefijo | Efecto |
|---|---|
| `%'<` | alinear a la **izquierda** |
| `%'>` | alinear a la **derecha** |
| `%'\|` | **centrar** |
| `%'*` | **negrita** |
| `%'/` | *itálica* |
| `%'_` | subrayado |
| `%'-----` | **línea divisoria** (solo guiones/iguales) |
| `%'\ ...` | ESCAPE: mostrar literal `< > \| * / _` o `-----` |

Combinables: `%'>*` = derecha + negrita.

---

## 7. Bloque de atributos `%'{...}` / `%"{...}`

"Código dentro del `%`" para pedir todo el formato de una vez:

```matlab
%'{rojo, centrado, negrita} Aviso importante
%"{negro, izquierda} Subtítulo
%'{#1f6feb, grande} Encabezado azul
```
Atributos: color (rojo/azul/verde/negro/gris/naranja/morado o `#hex`), alineación
(izquierda/centro/derecha), estilo (negrita/italica/subrayado), tamaño (grande/pequeno).

---

## 8. Columnas `#cols`

Fila de celdas iguales separadas por `;`. **Celdas MATLAB-válidas** (variables/números), los
encabezados van en la directiva:

```matlab
% #cols Largo a | Ancho b | Carga q
a ; b ; q
% #endcols
```
⚠️ Una celda `'texto` (comilla sin cerrar) es char-array inválido en MATLAB y rompe el parseo.

---

## 9. HTML crudo

Para tablas/figuras a medida (va dentro de comentario, MATLAB lo ignora):

```matlab
%' <table><tr><td>A</td><td>B</td></tr></table>
%' <svg width="60" height="20">...</svg>
```

---

## 10. Operaciones de texto (MATLAB) que soporta Hekatan Lab

`sprintf`, `num2str`, `str2num`, `str2double`, `int2str`, `strcat`, `[s1 s2]` (concatenar),
`strrep`, `erase`, `strsplit`, `strjoin`, `strtrim`, `deblank`, `blanks`, `pad`,
`upper`, `lower`, `strfind`, `strcmp`, `contains`, `startsWith`, `endsWith`,
`regexp`, `regexprep`, `mat2str`.

```matlab
nombre = sprintf('Losa %gx%g m', a, b)   %' El identificador es: @
```

---

## Ejemplo completo

Ver [`18 FEA Slab/test_libro_texto_variables.m`](18%20FEA%20Slab/test_libro_texto_variables.m):
un documento tipo libro que usa **todas** estas formas.

---

## Tabla resumen (todas las formas)

| Forma | Sintaxis | Para qué |
|---|---|---|
| Oculto | `a = 6  % nota` | comentario privado, no se ve |
| Visible | `a = 6  %' texto` | anotar la línea |
| Placeholder | `a = 6  %' Lado: @ m` | texto antes/después/mezclado con la variable |
| Referencia | `%' … @a … @b …` | combinar variables en una línea |
| Expresión | `%' … @{a*b} …` | evaluar e insertar |
| Pre-cálculo | `%' @a` arriba, `a=6` abajo | condicionar el texto arriba |
| Título | `%" Título` | negro, centrado, negrita |
| Subtítulo | `%"< Subtítulo` | izquierda (o `>` `\|`) |
| Formato | `%'* /  _ < > \|` | negrita/itálica/subrayado/alineación |
| Atributos | `%'{color,align,size}` | formato completo |
| Línea | `%'-----` | divisor |
| Columnas | `% #cols … % #endcols` | tabla de columnas |
| HTML | `%' <table>…` | tablas/figuras a medida |
| Texto (MATLAB) | `sprintf`, `strcat`, … | manipular cadenas |
