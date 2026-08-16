# 2026-08-16 — Banco Hekatan Lab vs MATLAB R2017a con tic/toc

**Pedido:** unos tests contra el MATLAB 2017a instalado, con tic/toc.

**Dónde quedó:** `tests/bench_ml/` (6 casos `.m` + `run_bench.py` + `README.md`).
Se corre con `python tests/bench_ml/run_bench.py`.

---

## ✅ Funcionó

- **Un solo `.m` para los dos motores.** El mismo archivo corre en MATLAB y en Lab; cada
  uno imprime `CHECK <nombre> <valor>` y su `t_seg`. Nada de `rand`: las matrices salen de
  una fórmula, así los dos motores ven los mismos números y se comparan dígito a dígito.
- **MATLAB arranca UNA vez** (`ml_driver.m` corre los 6 casos seguidos): arrancar MATLAB
  cuesta ~10 s, más que todos los cómputos juntos. El `tic/toc` va dentro del script, así
  que mide el motor, no el arranque.
- **Mejor de 3 repeticiones** en ambos → motor caliente contra motor caliente.
- **6/6 casos dan los mismos números que MATLAB R2017a** (tolerancia relativa 1e-12).
- **Tiempos (mejor de 3):** b3 bucle escalar **Lab 17× más rápido**, b5 FEM Q4 **3.7×**,
  b2 disperso 1.6×; b1 denso y b6 FFT empatados; b4 vectorizado 4e6 Lab 1.6× más lento.
- **Salida `.txt` del CLI** (`CalcpadLabCli.exe caso.m caso.txt`) en vez del HTML: el
  reporte HTML pesa MBs de JS y hay que rasparlo con regex; el txt es texto plano.
- **`--solo-lab`** guarda/lee `ref_matlab.json`, así el banco sirve en una máquina sin
  licencia de MATLAB.

## ❌ No funcionó (y por qué) — los dos bugs que destapó el banco

1. **`t_seg = 0` en el primer caso.** No era el bench: **`tic` sin paréntesis dentro de un
   bucle compilado por el JIT** se tomaba como *variable live-in* → su slot arrancaba en 0,
   la función `tic` nunca se llamaba, el cronómetro global nunca arrancaba y `toc` devolvía
   **0**. Un benchmark metido en un `for` medía cero y parecía infinitamente rápido, sin
   error ni warning. Es el MISMO pozo del bug histórico de `pi=0` (el que motivó
   `tests/numeric`), pero con funciones-sin-argumentos en vez de constantes.
   → Arreglo: bail-out al intérprete en `MatlabJit.cs` `InferKind` cuando el identificador
   no está en el scope y sí es una función conocida. Cubre también `rand`, `cputime`, etc.
   *Cómo se cazó:* imprimir el handle (`t0` valía 0) — no adivinando.

2. **`round` del intérprete redondeaba "al par"** (bancario, el default de .NET):
   `round(6.5)=6`, `round(0.5)=0`, `round(-2.5)=-2`. MATLAB **siempre se aleja del cero**:
   7, 1, -3. El JIT ya lo hacía bien (`JRound` usa `AwayFromZero`) → el motor se contradecía
   a sí mismo según compilara o interpretara el mismo bucle.
   *Cómo apareció:* el FEM Q4 (b5) se desviaba de MATLAB en el 4º dígito (1.27069 vs
   1.27037). No era el solver: `round(nny/2)` daba 6 en vez de 7 y **la carga caía en otro
   nodo**. Tras el arreglo, b5 casa con MATLAB al último dígito.
   → Arreglado en `MatlabEvaluator.cs`; de paso `round(x,n)`, que antes ignoraba el 2º
   argumento en silencio.

3. Intentos fallidos de aislamiento antes de dar con el 1: culpé a `min(Inf,·)`, al nombre
   de la variable `tmin` y al `toc` anidado dentro de `min(...)`. Los tres eran falsos: el
   patrón real es "identificador de función sin paréntesis dentro de bucle JIT".

## Verificación de que no rompí nada

- `tests/numeric` (t1–t4, refs MATLAB 2017a + Abaqus): **24/24 OK**.
- `tests/jit_coverage` (42 casos): **valor a valor idénticos** a la referencia de MATLAB
  (solo cambian etiquetas cosméticas del log).
- Velocidad: el CLI con los arreglos NO es más lento — t3 (FEM) 1.57 s con los arreglos vs
  1.97 s con el binario anterior.

## ⏳ Falta

- **b4 (vectorizado 4e6) va 1.6× más lento que MATLAB.** Es el único punto flojo: hay
  `sin/exp/sqrt` elementwise + `cumsum` + `sort`. Vale medir cuál de los tres pesa.
- Ampliar el banco: `interp1`, `eig`/`svd`, cell/struct y strings.
- Enganchar `run_bench.py` al pre-commit junto con `tests/numeric/run_tests.py`.
