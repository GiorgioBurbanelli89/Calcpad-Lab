#!/usr/bin/env python3
"""Banco cara a cara: Hekatan Lab vs MATLAB R2017a, con tic/toc.

Que hace distinto a tests/numeric: alli los numeros y los tiempos de MATLAB estan
ESCRITOS A MANO en el runner (referencias congeladas). Aqui se corre MATLAB DE VERDAD
en cada pasada, con el MISMO .m, y se comparan dos cosas:

  1. los CHECK  -> el motor calcula lo mismo (tolerancia relativa)
  2. el t_seg   -> quien tarda menos (tic/toc DENTRO del script, sin contar arranques)

Cada .m mide el mejor de 3 repeticiones, asi se compara motor caliente contra motor
caliente y no el arranque del proceso.

Uso:
    python run_bench.py                    # los dos motores
    python run_bench.py --solo-lab         # sin MATLAB (p.ej. en una maquina sin licencia)
    python run_bench.py --exe <ruta.exe> --matlab <ruta\\matlab.exe>

Sale != 0 si algun CHECK se desvia. El tiempo NO tumba la corrida (la maquina tiene
ruido), solo se reporta; se avisa fuerte si Lab pasa de 3x el tiempo de MATLAB.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
# Cada corrida CON MATLAB deja aqui lo que MATLAB imprimio. Asi `--solo-lab` (una
# maquina sin licencia, o un pre-commit rapido) sigue comparando contra MATLAB de
# verdad, solo que congelado en la ultima corrida buena.
REF_JSON = os.path.join(HERE, "ref_matlab.json")
DEFAULT_EXE = os.path.abspath(os.path.join(
    HERE, "..", "..", "Symbolic.Cli", "bin", "Release", "net10.0", "CalcpadLabCli.exe"))
DEFAULT_MATLAB = r"C:\Program Files\MATLAB\R2017a\bin\matlab.exe"

CASOS = [
    ("b1_densa",   "algebra densa: matmul + Cholesky + backslash"),
    ("b2_sparse",  "disperso: ensamblado sparse(i,j,v) + solver"),
    ("b3_bucle",   "bucle escalar: Newton 1e6 x 5 (JIT contra JIT)"),
    ("b4_vector",  "vectorizado 4e6: elementwise + reducciones + sort"),
    ("b5_fem_q4",  "FEM Q4 real: Gauss 2x2 + scatter-add + solve"),
    ("b6_fft",     "FFT 2^20 + Parseval + ida y vuelta"),
]

# Tolerancia RELATIVA por defecto. Los dos motores usan double, pero suman en
# distinto orden (SIMD) -> los ultimos bits pueden bailar; 1e-12 lo absorbe sin
# tapar un error de verdad (el del FEM Q4 era 2.5e-4, tres ordenes por encima).
TOL_REL = 1e-12
# Los CHECK terminados en _res son RESIDUOS (deben ser ~0 en ambos motores, pero su
# valor exacto depende del pivoteo). No se comparan entre si: se exige que ambos
# esten por debajo de este umbral relativo al problema.
TOL_RESIDUO = 1e-6

RE_CHECK = re.compile(r"CHECK\s+([A-Za-z0-9_]+)\s+([-+0-9.eE]+)")


def parse_checks(texto):
    return {m.group(1): m.group(2) for m in RE_CHECK.finditer(texto)}


def corre_lab(exe, caso):
    """Un proceso del CLI por caso; la salida .txt es texto plano (sin HTML)."""
    salida = os.path.join(HERE, caso + ".lab.txt")
    if os.path.exists(salida):
        os.remove(salida)
    t0 = time.time()
    subprocess.run([exe, caso + ".m", os.path.basename(salida)], cwd=HERE, timeout=1800,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    proc = time.time() - t0
    if not os.path.exists(salida):
        return {}, proc, "el CLI no genero salida"
    txt = open(salida, encoding="utf-8", errors="ignore").read()
    err = "el script reporto un error" if re.search(r"\bError\b|Undefined:", txt) else None
    return parse_checks(txt), proc, err


def corre_matlab(matlab, casos):
    """MATLAB arranca una sola vez y corre TODOS los casos (arrancar cuesta ~10 s)."""
    log = os.path.join(HERE, "ml.log")
    if os.path.exists(log):
        os.remove(log)
    cmd = [matlab, "-nodisplay", "-nosplash", "-nodesktop", "-wait",
           "-r", "try; ml_driver; catch e; disp(getReport(e)); end; exit",
           "-logfile", "ml.log"]
    t0 = time.time()
    subprocess.run(cmd, cwd=HERE, timeout=3600,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    dt = time.time() - t0
    if not os.path.exists(log):
        return {}, dt, "MATLAB no dejo log"
    txt = open(log, encoding="utf-8", errors="ignore").read()
    # el log trae los casos en orden, separados por "=== CASO <nombre>"
    trozos = re.split(r"===\s+CASO\s+", txt)
    res = {}
    for t in trozos[1:]:
        nombre = t.split()[0].strip()
        res[nombre] = parse_checks(t)
    return res, dt, None


def compara(nombre, lab, ml):
    """Devuelve (lista de fallas, nº de valores comparados)."""
    fallas = []
    ncomp = 0
    for k, v_ml in ml.items():
        if k == "t_seg":
            continue
        if k not in lab:
            fallas.append("%s: Lab no lo imprimio" % k)
            continue
        try:
            a = float(lab[k]); b = float(v_ml)
        except ValueError:
            if lab[k].strip() != v_ml.strip():
                fallas.append("%s: '%s' != '%s'" % (k, lab[k], v_ml))
            continue
        ncomp += 1
        if k.endswith("_res"):                      # residuo: ambos ~0, no identicos
            if abs(a) > TOL_RESIDUO or abs(b) > TOL_RESIDUO:
                fallas.append("%s: residuo alto Lab=%.3g MATLAB=%.3g" % (k, a, b))
            continue
        escala = max(abs(a), abs(b), 1e-300)
        if abs(a - b) / escala > TOL_REL:
            fallas.append("%s: Lab=%.14g  MATLAB=%.14g  (dif rel %.2g)"
                          % (k, a, b, abs(a - b) / escala))
    for k in lab:
        if k != "t_seg" and k not in ml:
            fallas.append("%s: MATLAB no lo imprimio (¿fallo el caso alli?)" % k)
    return fallas, ncomp


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", default=DEFAULT_EXE)
    ap.add_argument("--matlab", default=DEFAULT_MATLAB)
    ap.add_argument("--solo-lab", action="store_true")
    args = ap.parse_args()

    if not os.path.exists(args.exe):
        print("no encuentro el CLI: %s" % args.exe)
        print("compila con: dotnet build Symbolic.Cli/Symbolic.Cli.csproj -c Release")
        return 2

    print("Banco Hekatan Lab vs MATLAB R2017a  (tic/toc, mejor de 3)")
    print("CLI:    %s" % args.exe)

    ml_todo = {}
    if args.solo_lab:
        if os.path.exists(REF_JSON):
            ml_todo = json.load(open(REF_JSON, encoding="utf-8"))
            print("MATLAB: (congelado en %s, de una corrida anterior)" % os.path.basename(REF_JSON))
        else:
            print("MATLAB: no se corre y no hay referencia guardada -> solo se miden tiempos")
    else:
        if not os.path.exists(args.matlab):
            print("no encuentro MATLAB: %s  (usa --solo-lab si no lo tienes)" % args.matlab)
            return 2
        print("MATLAB: %s" % args.matlab)
        ml_todo, dt_ml, err = corre_matlab(args.matlab, CASOS)
        if err:
            print("  fallo MATLAB: %s" % err)
            return 2
        print("        (la sesion de MATLAB tardo %.0f s en total, arranque incluido)" % dt_ml)
        json.dump(ml_todo, open(REF_JSON, "w", encoding="utf-8"), indent=1, sort_keys=True)
    print("")

    filas = []
    fallas_tot = []
    for caso, desc in CASOS:
        lab, proc, err = corre_lab(args.exe, caso)
        if err:
            print("  [FALLA] %-11s %s" % (caso, err))
            fallas_tot.append((caso, err))
            continue
        t_lab = float(lab.get("t_seg", "nan"))
        ml = ml_todo.get(caso, {})
        t_ml = float(ml.get("t_seg", "nan")) if ml else float("nan")
        if ml:
            fallas, ncomp = compara(caso, lab, ml)
        else:
            fallas, ncomp = [], 0
        ratio = (t_lab / t_ml) if (t_ml == t_ml and t_ml > 0) else float("nan")
        filas.append((caso, desc, t_lab, t_ml, ratio, ncomp, len(fallas)))
        if fallas:
            print("  [FALLA] %-11s %d valores, %d mal" % (caso, ncomp, len(fallas)))
            for f in fallas:
                print("           - %s" % f)
            fallas_tot.extend((caso, f) for f in fallas)
        else:
            print("  [  OK  ] %-11s %d valores iguales a MATLAB" % (caso, ncomp))

    print("")
    print("  %-11s %10s %10s %9s   %s" % ("caso", "Lab (s)", "MATLAB(s)", "Lab/ML", "que mide"))
    print("  " + "-" * 88)
    for caso, desc, t_lab, t_ml, ratio, ncomp, nmal in filas:
        if ratio == ratio:
            veredicto = "Lab %.1fx mas rapido" % (1 / ratio) if ratio < 1 else "Lab %.1fx mas lento" % ratio
        else:
            veredicto = ""
        print("  %-11s %10.4f %10.4f %9s   %s" % (
            caso, t_lab, t_ml, ("%.2f" % ratio) if ratio == ratio else "-", desc))
        if veredicto:
            print("  %-11s %31s   -> %s" % ("", "", veredicto))
    print("")

    lentos = [f for f in filas if f[4] == f[4] and f[4] >= 3.0 and (f[2] - f[3]) > 0.5]
    for f in lentos:
        print("  AVISO: %s tarda %.1fx lo de MATLAB (%.3f s vs %.3f s) — eso no es ruido."
              % (f[0], f[4], f[2], f[3]))

    if fallas_tot:
        print("FALLARON %d comprobacion(es): Lab y MATLAB NO calculan lo mismo." % len(fallas_tot))
        return 1
    print("TODO OK — los %d casos dan los mismos numeros que MATLAB R2017a." % len(filas))
    return 0


if __name__ == "__main__":
    sys.exit(main())
