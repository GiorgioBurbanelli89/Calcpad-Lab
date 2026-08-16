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
| b1_densa  | 0.024 | 0.020 | Lab 1.2× más lento |
| b2_sparse | 0.022 | 0.055 | Lab 2.5× más rápido |
| b3_bucle  | 0.010 | 0.159 | **Lab 16× más rápido** |
| b4_vector | 0.070 | 0.093 | Lab 1.3× más rápido *(era 1.6× más lento)* |
| b5_fem_q4 | 0.019 | 0.078 | **Lab 4× más rápido** |
| b6_fft    | 0.058 | 0.074 | Lab 1.3× más rápido |

Lectura: Lab gana en **bucle escalar** (su JIT) y en **FEM** (ensamblado + solver
disperso). Lo único que sigue por detrás es el **denso** (b1), que en los dos motores es
librería nativa (LAPACK).

### El elementwise (b4): de 1.6× más lento a la par

`prof4.m` y `prof5.m` son los perfiles que se usaron para encontrarlo — cronometran cada
primitiva por separado, **usando el resultado** para que nadie pueda borrarla por "código
muerto" y regalar un tiempo falso. Sobre 4e6 elementos (mejor de 7):

| primitiva | Lab antes | Lab ahora | MATLAB |
|---|---|---|---|
| `sum(v)`   | 10.4 ms | **1.9 ms** | 1.8 ms |
| `v.*v`     | 7.1 ms  | **4.2 ms** | 3.3 ms |
| `v+v`      | 7.1 ms  | **3.4 ms** | 3.5 ms |
| `v*2`      | 13.5 ms | **3.5 ms** | 3.6 ms |
| `sin(v)`   | 2.7 ms  | 2.7 ms | 16.7 ms |
| `exp(v)`   | 2.8 ms  | 2.8 ms | 12.2 ms |

Tres causas, ninguna adivinada — todas medidas:

1. **`sum` llamaba un delegado por elemento** (`ReduceNumDim` con `Func<double,double,double>`):
   4 millones de llamadas que .NET no puede meter inline ni vectorizar. Iba 7× por debajo
   del ancho de banda de memoria, o sea el cuello era el delegado, no el bus. Ahora hay
   camino SIMD para el caso denso real (`SumFastDense`); sparse/complejo/3D siguen por el
   general. La reducción por columna acumula fila a fila, así que da **bit a bit lo mismo**
   que antes.
2. **Cada resultado se ponía a cero antes de escribirlo.** .NET inicializa todo arreglo
   nuevo; como el SIMD lo sobrescribe entero, eran 32 MB de escritura tirados por
   operación. Ahora esos buffers se piden sin inicializar (`GC.AllocateUninitializedArray`).
3. **`A*escalar` iba por `MatMul`** en vez del camino element-wise SIMD, y tardaba el doble
   que `A.*escalar`. Los sparse siguen por `MatMul` a propósito: el camino genérico
   densifica, y densificar una K de FEM se come la RAM.

Y al revés: `sin`/`exp` de Lab ya eran **6× y 4× más rápidos** que los de MATLAB R2017a.

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
