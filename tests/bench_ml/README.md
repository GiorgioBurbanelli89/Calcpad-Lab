# Banco cara a cara: Hekatan Lab vs MATLAB R2017a (con tic/toc)

```
python run_bench.py                 # corre los dos motores
python run_bench.py --solo-lab      # sin MATLAB (compara contra ref_matlab.json)
```

## Por qué existe (y en qué se diferencia de `tests/numeric`)

En `tests/numeric` los números y los tiempos de MATLAB están **escritos a mano** en el
runner: son referencias congeladas. Sirven de canario, pero no vuelven a preguntarle a
MATLAB. Aquí se corre **MATLAB de verdad en cada pasada**, con el **mismo `.m`**, y se
comparan dos cosas:

1. **los `CHECK`** → los dos motores tienen que dar el mismo número (tolerancia relativa
   `1e-12`; los `*_res` son residuos, que solo se exige que sean ~0 en ambos).
2. **el `t_seg`** → el `tic/toc` va **dentro** del script, así que mide el motor y no el
   arranque del proceso. Cada caso repite el cómputo 3 veces y se queda con el mínimo:
   motor caliente contra motor caliente.

## Los 6 casos

| caso | qué ejercita |
|---|---|
| `b1_densa`   | matmul + Cholesky + backslash densos (LAPACK/BLAS) |
| `b2_sparse`  | ensamblado `sparse(i,j,v)` + solver disperso (Poisson 5 puntos) |
| `b3_bucle`   | bucle escalar puro: Newton 1e6 × 5 (JIT contra JIT) |
| `b4_vector`  | 4e6 elementos: elementwise, reducciones, `cumsum`, `sort` |
| `b5_fem_q4`  | FEM de verdad: voladizo Q4, Gauss 2×2, scatter-add y solve |
| `b6_fft`     | FFT de 2^20 + Parseval + ida y vuelta |

Ninguno usa `rand`: las matrices salen de una fórmula, así los dos motores ven
exactamente los mismos números y los `CHECK` se pueden comparar dígito a dígito.

## Resultado (2026-08-16, esta máquina)

Los 6 casos dan **los mismos números** que MATLAB R2017a. Tiempos (mejor de 3):

| caso | Lab (s) | MATLAB (s) | |
|---|---|---|---|
| b1_densa  | 0.023 | 0.021 | Lab 1.1× más lento |
| b2_sparse | 0.033 | 0.054 | Lab 1.6× más rápido |
| b3_bucle  | 0.010 | 0.172 | **Lab 17× más rápido** |
| b4_vector | 0.108 | 0.067 | Lab 1.6× más lento |
| b5_fem_q4 | 0.019 | 0.072 | **Lab 3.7× más rápido** |
| b6_fft    | 0.067 | 0.067 | empate |

Lectura: donde Lab gana es en **bucle escalar** (su JIT) y en **FEM** (ensamblado +
solver disperso). Donde pierde es en **elementwise sobre vectores enormes** (b4) y va
parejo en denso y FFT, que en los dos motores son librería nativa.

## Dos bugs que destapó este banco

- **`tic` sin paréntesis dentro de un bucle JIT-eado daba `toc` = 0.** El JIT tomaba
  `tic` (identificador pelado, sin `()`) como *variable live-in*: su slot arrancaba en 0,
  la función nunca se llamaba y el cronómetro no arrancaba. O sea: **un benchmark dentro
  de un `for` medía cero y parecía infinitamente rápido**. Es el mismo pozo que el bug
  histórico de `pi=0`; el arreglo es el mismo: bail-out al intérprete
  (`MatlabJit.cs`, `InferKind`).
- **`round` del intérprete redondeaba "al par"** (bancario: `round(6.5)=6`, `round(0.5)=0`)
  en vez de alejarse del cero como MATLAB. El JIT ya lo hacía bien, así que el motor se
  contradecía según compilara o interpretara. Se colaba como error mudo: en `b5_fem_q4`,
  `round(nny/2)` daba 6 en vez de 7, la carga caía en **otro nodo** y el voladizo se
  desviaba de MATLAB en el 4º dígito. Arreglado en `MatlabEvaluator.cs` (y de paso
  `round(x,n)`, que antes ignoraba el 2º argumento en silencio).
