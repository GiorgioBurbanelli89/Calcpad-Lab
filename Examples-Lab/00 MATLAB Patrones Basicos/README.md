# Ejemplos MATLAB — Calcpad-Lab

Scripts `.m` que sirven como guía de inicio del modo MATLAB de Calcpad-Lab.
Funcionan idénticos en MATLAB R2017a y en `CalcpadLabCli.exe`.

## Cómo ejecutarlos

**En MATLAB R2017a:**
- Abrir el archivo en el editor → F5 (Run).

**En Calcpad-Lab CLI:**
```
CalcpadLabCli.exe 01_Patrones_Multilinea.m
```
Genera un `.html` con el render (fracciones apiladas, variables en azul,
unidades en verde, etc.) y lo abre en el navegador.

## Contenido

| Archivo | Tema | Qué demuestra |
|---|---|---|
| `01_Patrones_Multilinea.m` | Texto multilínea | Las 10 formas de imprimir texto con saltos de línea en MATLAB (no hay heredoc — hay que conocer los idioms) |
| `02_Operaciones_Simbolicas.m` | Algebra simbólica | `syms`, `diff`, `int`, `solve`, `simplify`, `subs` combinados con texto en `fprintf('%s', char(expr))` |
| `03_Viga_Memoria_Calculo.m` | Memoria estructural | Ejemplo completo: viga simplemente apoyada con prosa fluida + ecuaciones + caso numérico, estilo libro de mecánica |

## Diferencias entre MATLAB pleno y Calcpad-Lab MVP

El motor simbólico de Calcpad-Lab es un MVP. Algunas construcciones de
MATLAB que **no funcionan**:

| Construcción MATLAB | Workaround Calcpad-Lab |
|---|---|
| `solve(expr == 0, x)` | `solve(expr, x)` (asume `= 0`) |
| `solve([eq1, eq2], [x, y])` (sistemas) | Resolver manualmente o usar fórmula conocida |
| `subs(expr, {a,b}, {1,2})` (cells) | `e = subs(expr, a, 1); e = subs(e, b, 2);` (encadenado) |
| `simplify` agresiva agrupando polinomios | Hacer la simplificación a mano |

Estas limitaciones están señaladas como **MVP** en el código fuente; se
relajarán en futuras versiones del motor.

## Convención de display tipo Calcpad

Para que las expresiones simbólicas se rendereen con el CSS Calcpad
(fracciones apiladas, colores), usar siempre `char(expr)` dentro de
`fprintf('%s', ...)`:

```matlab
syms q L
M_max = q*L^2/8;
fprintf('Momento maximo: M_max = %s\n', char(M_max));
```

Lo que sale renderizado:
- `q`, `L` → variables en azul (`<var>`)
- `L^2` → exponente con superíndice
- `q·L²/8` → fracción apilada con barra horizontal
- `q*L` → middle dot `·` en vez de asterisco

## Escape de comilla simple

MATLAB usa `''` (dos comillas consecutivas) dentro de un string para
representar una comilla literal. Ejemplo:

```matlab
fprintf('don''t worry\n');          % imprime: don't worry
fprintf('y''(x) = M(x)\n');         % imprime: y'(x) = M(x)
fprintf('E*I*y''''(x) = M(x)\n');   % imprime: E*I*y''(x) = M(x)
```

El tokenizer de Calcpad-Lab respeta el escape MATLAB estándar.
