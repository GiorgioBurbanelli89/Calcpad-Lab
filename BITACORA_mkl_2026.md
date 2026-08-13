# Bitácora — Migración a Intel oneMKL 2026.1 (todo el motor)

## Objetivo
Jorge: "todo con MKL 2026". Llevar Hekatan Lab de oneMKL 2024.2 (`mkl_rt.2.dll`) a
**2026.1** (`mkl_rt.3.dll`) en TODO el motor, sin romper correctitud, y regenerar
el instalador una sola vez con 2026 + el ejemplo simbólico corregido.

## Qué se hizo
- **Swap de DLLs** en `Symbolic.Core/Native/mkl/` a la serie **2026 (`.3`)**. Respaldo
  de 2024.2 en `Symbolic.Core/Native/mkl_2024.2_backup/` (`.2`) para rollback.
  Ambas carpetas están en `.gitignore` (binarios ~388 MB, no se versionan).
- **Sin cambios en `BlasInterop.cs`**: el `.csproj` incluye MKL con glob
  `Native\mkl\*.dll` y el cargador busca `mkl_rt*.dll`, así que 2026 entra solo.
- **Republish LIMPIO del WPF**: la primera publicación dejó los DLLs MEZCLADOS
  (14 `.2` + 16 `.3`). Como el cargador toma `GetFiles("mkl_rt*.dll")[0]`
  (orden alfabético → `.2` primero), habría cargado 2024.2 en silencio. Se borró la
  carpeta de publicación y se republicó → **solo 2026** (0 `.2`, 16 `.3`).

## Verificación numérica (suite `tests/numeric/`, CLI Release con 2026)
**24/24 OK — cero desviación vs MATLAB 2017a / Abaqus.**

| Test | Valores | Velocidad (2026) |
|---|---|---|
| t1_builtin       | 6/6  OK | — |
| t2_arrays3d      | 5/5  OK | — |
| t3_fem_srm       | 10/10 OK | 2.09 s vs MATLAB 1.35 s (~1.5×; es fint_c, no MKL) |
| t4_decomposition | 3/3  OK | **speedup 21×** · Lab-decomp 0.043 s vs MATLAB-backslash 0.486 s = **11×** |

## Regresión de reutilización de factorización — estado honesto
2024.2 daba ~74× de speedup en el micro t4; **2026 da 21×** (más lento en absoluto),
pero **pasa el guardián de 20×** y **sigue ganándole 11×** a MATLAB 2017a en el mismo
patrón multi-solve. No se aplicó ningún arreglo de threading (el agente que lo
investigaba se detuvo sin dejar fix ni mediciones). Palanca pendiente si se quiere
recuperar el absoluto: `MKL_THREADING_LAYER` / nº de hilos para el path de sustitución
(`HEKATAN_MKL_THREADS`), a evaluar aparte.

## Instalador
`Installer/Hekatan-Lab-Setup-1.1.3.exe` (180.8 MB) regenerado con ISCC → empaqueta
**MKL 2026 + el ejemplo simbólico no-repetido**. Publicación limpia verificada (solo `.3`).
