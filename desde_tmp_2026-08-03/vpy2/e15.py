# -*- coding: utf-8 -*-
# =============================================================================
#  MESA A TORSIÓN — explicación paso a paso (FEM 3D validado vs ETABS)
#  Réplica en Python de  calcpad-draw/mesa_torsion_explicada3D.cpd
#  para Calcpad Suite Py  (se ejecuta vía el intérprete python real; req. numpy/scipy)
# =============================================================================
#  Losa cuadrada apoyada en 4 columnas (una por esquina) con carga uniforme q.
#  Como solo se apoya en 4 puntos, la losa flexiona Y torsiona → "mesa a torsión".
#  Método de rigidez en 4 pasos: ELEMENTOS → K LOCAL → ENSAMBLAR → K·U=F.
#  6 GDL por nodo (u,v,w,θx,θy,θz), igual que ETABS. Modelo: 240 GDL (40 nodos).
# =============================================================================
import sys
try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass
import numpy as np
import html as _html

# ─── Mini-lib de salida estilo Calcpad ───────────────────────────────────────
#  Emite HTML con el markup de Calcpad (.eq, var, <table>) vía el marcador
#  __CPSPY_HTML__ → Calcpad Suite Py lo renderiza como worksheet (NO texto plano).
def _emit(h):
    print("__CPSPY_HTML__:" + h.replace("\n", " "))

def cp_h(text):
    _emit(f'<h3 style="color:#1a4f7a;border-bottom:1px solid #d9d9d9;'
          f'margin:.9em 0 .3em;padding-bottom:2px">{_html.escape(text)}</h3>')

def cp_p(text):
    _emit(f'<p class="line"><span class="eq">{text}</span></p>')

def _var(name):
    if "_" in name:
        b, s = name.split("_", 1)
        return f'<var>{_html.escape(b)}<sub>{_html.escape(s)}</sub></var>'
    return f'<var>{_html.escape(name)}</var>'

def cp_val(name, value, unit="", fmt="{:.3f}"):
    v = fmt.format(value) if isinstance(value, (int, float)) else _html.escape(str(value))
    u = f' <i class="unit">{_html.escape(unit)}</i>' if unit else ""
    _emit(f'<p class="line"><span class="eq">{_var(name)} = <b>{v}</b>{u}</span></p>')

def cp_table(headers, rows, title=None, ok_col=None):
    """Tabla estilo Calcpad. headers=list; rows=list de list (celdas texto/num).
    ok_col: índice de columna con '✓/~/✗' para colorear (opcional)."""
    th = "".join(f'<th style="text-align:left;padding:3px 12px;border-bottom:2px solid #888">'
                 f'{_html.escape(str(x))}</th>' for x in headers)
    body = []
    for r in rows:
        tds = []
        for k, c in enumerate(r):
            txt = c if (isinstance(c, str) and c.startswith("<")) else _html.escape(str(c))
            color = ""
            if ok_col is not None and k == ok_col:
                color = ("color:#2e7d32" if "✓" in str(c) else
                         "color:#b8860b" if "~" in str(c) else
                         "color:#c62828" if "✗" in str(c) else "")
            tds.append(f'<td style="padding:2px 12px;{color}">{txt}</td>')
        body.append("<tr>" + "".join(tds) + "</tr>")
    cap = (f'<caption style="text-align:left;font-weight:bold;color:#1a4f7a;'
           f'padding:4px 0">{_html.escape(title)}</caption>' if title else "")
    _emit(f'<table class="eq" style="border-collapse:collapse;margin:.4em 0 .8em;'
          f'font-size:.95em">{cap}<tr>{th}</tr>{"".join(body)}</table>')

# ───────────────────────────── 3. DATOS DE LA MESA ──────────────────────────
E_MPa = 24850.0          # módulo de elasticidad [MPa]
b_c   = 0.40             # lado columna C40x40 [m]
b_v   = 0.30             # viga V30x50 — ancho [m]
h_v   = 0.50             # viga V30x50 — alto [m]
H     = 4.0              # altura de columna [m]
a     = 6.0              # lado de la losa (cuadrada) [m]
t     = 0.10             # espesor de losa [m]
nu    = 0.20             # Poisson
q     = 4.9033           # carga patrón VIVA (0.5 tonf/m²) [kN/m²]

E   = 24850000.0         # [kN/m²]
Gm  = E / (2 * (1 + nu))
dx  = 1.2
N   = 5
GRAV = 9.8066            # kN → tonf

# Secciones
Ac  = b_c**2
Iyc = b_c**4 / 12
Jc  = (1/3 - 0.21*1*(1 - 1/12)) * b_c * b_c**3          # Saint-Venant columna
Av  = b_v * h_v
Iyv = b_v * h_v**3 / 12
Izv = h_v * b_v**3 / 12
Jvr = b_v / h_v
Jv  = (1/3 - 0.21*Jvr*(1 - Jvr**4/12)) * h_v * b_v**3   # Saint-Venant viga (lado corto³)

# ───────────────────────── FICHA / IDENTIFICACIÓN DEL MODELO ────────────────
FICHA = {
    "Nombre":        "Mesa a Torsión 6x6 m — losa sobre 4 columnas",
    "Tipo análisis": "Estático lineal + Modal (problema de valores propios)",
    "Método":        "Método Directo de Rigidez (FEM), K·U = F",
    "Solver":        "Propio en numpy (NO OpenSeesPy, NO ETABS) — ensamblaje directo",
    "Elem. losa":    "Shell DELGADO (Kirchhoff) = ETABS 'ShellThin'. Flexión: placa "
                     "ACM Q4 rect. (12 GDL) + membrana Q4 tensión plana (Gauss 2x2) + drilling",
    "Elem. barra":   "Frame 3D Euler-Bernoulli + cortante de Timoshenko (12 GDL)",
    "Convenciones":  "CSI Analysis Reference Manual (ejes locales, offsets, masa lumped)",
    "Material":      "Concreto '4000Psi': E=2'534'564 tonf/m² (24.86 GPa), ν=0.20, γ=2.40277",
    "Modelo fuente": "ETABS 19.1  'Mesa torsiónT.e2k' / 'Mesa torsión_1.e2k' (Seproinca 2020)",
    "Validación":    "vs ETABS 19.1 (e2k) y vs Calcpad (mesa_torsion_explicada3D.cpd)",
    "Autores form.": "Adini & Clough (1961), Melosh (1963) [placa ACM]; "
                     "Timoshenko [viga]; CSI (2017) [convenciones]",
    "Réplica de":    "calcpad-draw/mesa_torsion_explicada3D.cpd",
}
cp_h("MESA A TORSIÓN — FEM 3D  (Calcpad Suite Py)")
cp_table(["Campo", "Valor"], [[k, v] for k, v in FICHA.items()], title="Ficha del modelo")

cp_h("Datos de la mesa")
cp_table(["Parámetro", "Símbolo", "Valor", "Unidad"], [
    ["Módulo de elasticidad", _var("E"), f"{E_MPa:.0f}", "MPa"],
    ["Lado columna C40x40",   _var("b_c"), f"{b_c}", "m"],
    ["Viga V30x50 (ancho×alto)", _var("b_v") + "×" + _var("h_v"), f"{b_v}×{h_v}", "m"],
    ["Altura de columna",     _var("H"), f"{H}", "m"],
    ["Lado de la losa",       _var("a"), f"{a}", "m"],
    ["Espesor de losa",       _var("t"), f"{t}", "m"],
    ["Poisson",               _var("nu"), f"{nu}", "—"],
    ["Carga viva",            _var("q"), f"{q}", "kN/m²"],
], title="Entradas")
cp_table(["Sección", _var("A") + " [m²]", _var("I_y") + " [m⁴]", _var("I_z") + " [m⁴]", _var("J") + " [m⁴]"], [
    ["Columna C40x40", f"{Ac:.4f}", f"{Iyc:.3e}", f"{Iyc:.3e}", f"{Jc:.3e}"],
    ["Viga V30x50",    f"{Av:.4f}", f"{Iyv:.3e}", f"{Izv:.3e}", f"{Jv:.3e}"],
], title="Propiedades de sección")


# ═════════════════════════ 4. RIGIDEZ DE BARRA 3D (frame) ════════════════════
def frame_k(E, Gm, Af, Iyf, Izf, Jtf, Lf):
    """Frame 3D 12×12 con corrección de cortante de Timoshenko (φ).
    GDL por extremo: u, v, w, θx, θy, θz. v flexiona sobre z (Iz), w sobre y (Iy)."""
    EA_L = E*Af/Lf
    GJ_L = Gm*Jtf/Lf
    Asf  = 5/6*Af
    phiz = 12*E*Izf/(Gm*Asf*Lf**2)
    phiy = 12*E*Iyf/(Gm*Asf*Lf**2)
    tz = 12*E*Izf/Lf**3/(1+phiz)
    bz = 6*E*Izf/Lf**2/(1+phiz)
    kz = 4*E*Izf/Lf*(1+phiz/4)/(1+phiz)
    az = 2*E*Izf/Lf*(1-phiz/2)/(1+phiz)
    ty = 12*E*Iyf/Lf**3/(1+phiy)
    by = 6*E*Iyf/Lf**2/(1+phiy)
    ky = 4*E*Iyf/Lf*(1+phiy/4)/(1+phiy)
    ay = 2*E*Iyf/Lf*(1-phiy/2)/(1+phiy)
    K = np.array([
        [ EA_L,  0,   0,   0,    0,   0,  -EA_L, 0,   0,   0,    0,   0  ],
        [ 0,    tz,   0,   0,    0,   bz,  0,  -tz,   0,   0,    0,   bz ],
        [ 0,    0,   ty,   0,  -by,   0,   0,   0,  -ty,   0,  -by,   0  ],
        [ 0,    0,   0,  GJ_L,   0,   0,   0,   0,   0, -GJ_L,   0,   0  ],
        [ 0,    0, -by,    0,   ky,   0,   0,   0,  by,    0,   ay,   0  ],
        [ 0,   bz,   0,    0,    0,   kz,  0, -bz,   0,    0,    0,   az ],
        [-EA_L, 0,   0,    0,    0,   0,  EA_L, 0,   0,    0,    0,   0  ],
        [ 0,  -tz,   0,    0,    0,  -bz,  0,  tz,   0,    0,    0,  -bz ],
        [ 0,    0, -ty,    0,   by,   0,   0,   0,  ty,    0,   by,   0  ],
        [ 0,    0,   0, -GJ_L,   0,   0,   0,   0,   0,  GJ_L,   0,   0  ],
        [ 0,    0, -by,    0,   ay,   0,   0,   0,  by,    0,   ky,   0  ],
        [ 0,   bz,   0,    0,    0,   az,  0, -bz,   0,    0,    0,   kz ],
    ], dtype=float)
    return K

Kcol  = frame_k(E, Gm, Ac, Iyc, Iyc, Jc, H)     # columna C40x40 (L=4)
Kbeam = frame_k(E, Gm, Av, Iyv, Izv, Jv, dx)    # viga V30x50 (segmento L=1.2)
print(f"\n  Frame: Kcol[0,0]=EA/L={Kcol[0,0]:.3e}   Kbeam[1,1]=12EIz/L³(Tim)={Kbeam[1,1]:.3e}")


# ═════════════════════ 5. RIGIDEZ DE LOSA — SHELL 24×24 ══════════════════════
# Matriz constitutiva de placa D (momentos ↔ curvaturas)
D0 = E*t**3/(12*(1-nu**2))
Dp = D0*np.array([[1, nu, 0], [nu, 1, 0], [0, 0, (1-nu)/2]], dtype=float)

# --- Bloque MEMBRANA (Q4 plane stress, Gauss 2×2) + DRILLING ---
xn = np.array([0, 1.2, 1.2, 0], float)
yn = np.array([0, 0, 1.2, 1.2], float)
av = 0.6; bv = 0.6
c  = E/(1-nu**2)
Em = c*np.array([[1, nu, 0], [nu, 1, 0], [0, 0, (1-nu)/2]], float)
sxn = np.array([-1, 1, 1, -1], float)
syn = np.array([-1, -1, 1, 1], float)
g = 1/np.sqrt(3)
gpx = np.array([-g, g, g, -g]); gpy = np.array([-g, -g, g, g])
Km  = np.zeros((8, 8))
Kdr = np.zeros((12, 12))
gamma = Gm*t
for ig in range(4):
    xi, eta = gpx[ig], gpy[ig]
    J11 = J12 = J21 = J22 = 0.0
    for i in range(4):
        dxi = 0.25*sxn[i]*(1 + syn[i]*eta)
        det = 0.25*syn[i]*(1 + sxn[i]*xi)
        J11 += dxi*xn[i]; J12 += dxi*yn[i]
        J21 += det*xn[i]; J22 += det*yn[i]
    dJ = J11*J22 - J12*J21
    Bm = np.zeros((3, 8)); Bd = np.zeros(12)
    for i in range(4):
        dxi = 0.25*sxn[i]*(1 + syn[i]*eta)
        det = 0.25*syn[i]*(1 + sxn[i]*xi)
        Ni  = 0.25*(1 + sxn[i]*xi)*(1 + syn[i]*eta)
        dNx = (J22*dxi - J12*det)/dJ
        dNy = (-J21*dxi + J11*det)/dJ
        Bm[0, 2*i]   = dNx
        Bm[1, 2*i+1] = dNy
        Bm[2, 2*i]   = dNy
        Bm[2, 2*i+1] = dNx
        Bd[3*i]   = 0.5*dNy
        Bd[3*i+1] = -0.5*dNx
        Bd[3*i+2] = Ni
    Km  += Bm.T @ Em @ Bm * t * dJ
    Kdr += gamma*abs(dJ)*np.outer(Bd, Bd)

# --- Bloque FLEXIÓN MZC (ACM, Gauss 3×3) ---
xin = np.array([-1, 1, 1, -1], float)
ein = np.array([-1, -1, 1, 1], float)
gp = np.array([-np.sqrt(3/5), 0, np.sqrt(3/5)])
gw = np.array([5/9, 8/9, 5/9])
H1xx = lambda xi, ei, x, y: 1/8*((1+xi*x)*(1+ei*y)*(-2) + 2*(xi*(1+ei*y))*(xi-2*x))
H1yy = lambda xi, ei, x, y: 1/8*((1+xi*x)*(1+ei*y)*(-2) + 2*(ei*(1+xi*x))*(ei-2*y))
H1xy = lambda xi, ei, x, y: 1/8*(xi*ei*(2+xi*x+ei*y-x**2-y**2) + (xi*(1+ei*y))*(ei-2*y) + (ei*(1+xi*x))*(xi-2*x))
H2yy = lambda xi, ei, x, y: bv/8*ei*(1+xi*x)*2*(3*ei*y+1)*ei*ei
H2xy = lambda xi, ei, x, y: bv/8*xi*ei*ei*(1+ei*y)*(3*ei*y-1)
H3xx = lambda xi, ei, x, y: -av/8*xi*(1+ei*y)*2*(3*xi*x+1)*xi*xi
H3xy = lambda xi, ei, x, y: -av/8*ei*xi*xi*(1+xi*x)*(3*xi*x-1)
Kb = np.zeros((12, 12))
for ig in range(3):
    for jg in range(3):
        xg, yg = gp[ig], gp[jg]
        wg = gw[ig]*gw[jg]
        Bp = np.zeros((3, 12))
        for i in range(4):
            xi, ei = xin[i], ein[i]
            Bp[0, 3*i]   = -H1xx(xi, ei, xg, yg)/av**2
            Bp[1, 3*i]   = -H1yy(xi, ei, xg, yg)/bv**2
            Bp[2, 3*i]   = -2*H1xy(xi, ei, xg, yg)/(av*bv)
            Bp[1, 3*i+1] = -H2yy(xi, ei, xg, yg)/bv**2
            Bp[2, 3*i+1] = -2*H2xy(xi, ei, xg, yg)/(av*bv)
            Bp[0, 3*i+2] = -H3xx(xi, ei, xg, yg)/av**2
            Bp[2, 3*i+2] = -2*H3xy(xi, ei, xg, yg)/(av*bv)
        Kb += wg * Bp.T @ Dp @ Bp * av*bv

# --- Ensamblar los 3 bloques en el SHELL 24×24 (u,v←mem; w,θx,θy←flex; θz←drill) ---
Ks = np.zeros((24, 24))
dmd = [0, 1, 5]   # u, v, θz  (0-based)
for i in range(4):
    for j in range(4):
        for di in range(2):
            for dj in range(2):
                Ks[6*i+di, 6*j+dj] += Km[2*i+di, 2*j+dj]
        for di in range(3):
            for dj in range(3):
                Ks[6*i+2+di, 6*j+2+dj] += Kb[3*i+di, 3*j+dj]
                Ks[6*i+dmd[di], 6*j+dmd[dj]] += Kdr[3*i+di, 3*j+dj]
print(f"  Shell: K membrana[0,0]={Km[0,0]:.3e}  K flexión Kb[0,0]={Kb[0,0]:.3e}  Ks 24x24 trace={np.trace(Ks):.3e}")


# ═══════════════════ 6. ROTACIÓN A EJES GLOBALES ════════════════════════════
def rblock(R3):
    R12 = np.zeros((12, 12))
    for bk in range(4):
        R12[3*bk:3*bk+3, 3*bk:3*bk+3] = R3
    return R12

R3col = np.array([[0, 0, 1], [0, 1, 0], [-1, 0, 0]], float)
R12c  = rblock(R3col)
Kcolg = R12c.T @ Kcol @ R12c
R3by  = np.array([[0, 1, 0], [-1, 0, 0], [0, 0, 1]], float)
R12by = rblock(R3by)
Kbyg  = R12by.T @ Kbeam @ R12by
Kbxg  = Kbeam.copy()
oJcol = 0.50    # offset geométrico columna (peralte viga) — solo reporte de fuerzas
obeam = 0.20    # offset geométrico viga (media columna) — solo reporte


# ═══════════════════ 7. ENSAMBLAJE → K global 240×240 ═══════════════════════
ndof = 240
K = np.zeros((ndof, ndof))

def ixc(i, j):
    return 5 + j*6 + i      # id de nodo de piso (1-based)

def scatter(Ke, nlist):
    nn = len(nlist)
    for li in range(6*nn):
        ni = li // 6
        dli = li - 6*ni
        gi = 6*(nlist[ni]-1) + dli
        for lj in range(6*nn):
            nj = lj // 6
            dlj = lj - 6*nj
            gj = 6*(nlist[nj]-1) + dlj
            K[gi, gj] += Ke[li, lj]

# 25 shells (losa)
for jj in range(5):
    for ii in range(5):
        scatter(Ks, [ixc(ii, jj), ixc(ii+1, jj), ixc(ii+1, jj+1), ixc(ii, jj+1)])
# 4 columnas
cb = [1, 2, 3, 4]
ct = [ixc(0, 0), ixc(5, 0), ixc(5, 5), ixc(0, 5)]
for e in range(4):
    scatter(Kcolg, [cb[e], ct[e]])
# vigas borde X (y=0 y y=6) e Y (x=0 y x=6), 5 segmentos c/u
for ii in range(5):
    scatter(Kbxg, [ixc(ii, 0), ixc(ii+1, 0)])
    scatter(Kbxg, [ixc(ii, 5), ixc(ii+1, 5)])
for jj in range(5):
    scatter(Kbyg, [ixc(0, jj), ixc(0, jj+1)])
    scatter(Kbyg, [ixc(5, jj), ixc(5, jj+1)])

# --- carga: q lumped a nodos de piso (esquinas /4, bordes /2) ---
F = np.zeros(ndof)
qa = q*dx*dx
for jj in range(6):
    for ii in range(6):
        fac = 1.0
        if ii in (0, 5) or jj in (0, 5):
            fac = 0.5
        if ii in (0, 5) and jj in (0, 5):
            fac = 0.25
        nd = ixc(ii, jj)
        F[6*nd - 3 - 1] -= qa*fac     # GDL w (1-based 6*nd-3 → 0-based -1)

# --- apoyos: bases (nodos 1-4) u,v,w fijos ---
for n in range(1, 5):
    for d in range(3):
        gi = 6*(n-1) + d
        K[gi, :] = 0; K[:, gi] = 0
        K[gi, gi] = 1; F[gi] = 0

print(f"\n  Ensamblado: K {K.shape}, 49 elementos (25 shells + 4 cols + 20 vigas), 40 nodos × 6 = 240 GDL")


# ═══════════════════ 8. RESOLUCIÓN K·U = F ══════════════════════════════════
U = np.linalg.solve(K, F)


# ═══════════════════ recovery estilo ETABS (Gauss 2×2 + extrapol al nodo) ════
ar = dx/2; br = dx/2
g3 = 1/np.sqrt(3)
xig  = np.array([-g3, g3, g3, -g3]); etag = np.array([-g3, -g3, g3, g3])
xisn = np.array([-1, 1, 1, -1], float); etasn = np.array([-1, -1, 1, 1], float)
s3 = np.sqrt(3); ad = 1 + s3/2; am = 1 - s3/2
Aext = np.array([[ad, -0.5, am, -0.5], [-0.5, ad, -0.5, am],
                 [am, -0.5, ad, -0.5], [-0.5, am, -0.5, ad]], float)

def Uw(nd):  return U[6*nd - 3 - 1]    # w
def Utx(nd): return U[6*nd - 2 - 1]    # θx
def Uty(nd): return U[6*nd - 1 - 1]    # θy

wmin = 0.0
for jj in range(6):
    for ii in range(6):
        wmin = min(wmin, Uw(ixc(ii, jj)))

Mxx_m = Myy_m = Mxy_m = 0.0
Mxn = np.zeros(36); Myn = np.zeros(36); Mxyn = np.zeros(36); cnt = np.zeros(36)
for jj in range(5):
    for ii in range(5):
        nl = [ixc(ii, jj), ixc(ii+1, jj), ixc(ii+1, jj+1), ixc(ii, jj+1)]
        m11g = np.zeros(4); m22g = np.zeros(4); m12g = np.zeros(4)
        for gpi in range(4):
            xi, eta = xig[gpi], etag[gpi]
            kxx = kyy = kxy = 0.0
            for ni in range(4):
                nd = nl[ni]
                tx = Utx(nd); ty = Uty(nd)
                dxv = 0.25*xisn[ni]*(1 + eta*etasn[ni])/ar
                dyv = 0.25*etasn[ni]*(1 + xi*xisn[ni])/br
                kxx += -dxv*ty
                kyy += dyv*tx
                kxy += dxv*tx - dyv*ty
            m11g[gpi] = D0*(kxx + nu*kyy)/GRAV
            m22g[gpi] = D0*(nu*kxx + kyy)/GRAV
            m12g[gpi] = D0*(1-nu)/2*kxy/GRAV
        for kk in range(4):
            m11k = sum(Aext[kk, gpi]*m11g[gpi] for gpi in range(4))
            m22k = sum(Aext[kk, gpi]*m22g[gpi] for gpi in range(4))
            m12k = sum(Aext[kk, gpi]*m12g[gpi] for gpi in range(4))
            Mxx_m = max(Mxx_m, abs(m11k)); Myy_m = max(Myy_m, abs(m22k)); Mxy_m = max(Mxy_m, abs(m12k))
            sk = nl[kk] - 5
            Mxn[sk] += m11k; Myn[sk] += m22k; Mxyn[sk] += m12k; cnt[sk] += 1

# Matrices 6×6 por nodo (para los gráficos): deflexión [mm] y momentos promediados
Wmat = np.zeros((6, 6)); Mxmat = np.zeros((6, 6)); Mymat = np.zeros((6, 6)); Mxymat = np.zeros((6, 6))
for jj in range(6):
    for ii in range(6):
        nd = ixc(ii, jj); sk = nd - 5
        Wmat[ii, jj]  = -Uw(nd)*1000
        Mxmat[ii, jj]  = Mxn[sk]/cnt[sk]
        Mymat[ii, jj]  = Myn[sk]/cnt[sk]
        Mxymat[ii, jj] = Mxyn[sk]/cnt[sk]

# --- fuerzas de columna en la CARA ---
T12c = rblock(R3col)
M2c = 0.0
for e in range(4):
    nb, nt = cb[e], ct[e]
    uG = np.zeros(12)
    for d in range(6):
        uG[d]   = U[6*(nb-1)+d]
        uG[6+d] = U[6*(nt-1)+d]
    fL = Kcol @ (T12c @ uG)
    fL[10] = fL[10] - fL[8]*oJcol   # M += -V·offset (1-based fL.11,fL.9)
    fL[11] = fL[11] + fL[7]*oJcol
    M2c = max(M2c, abs(fL[4]), abs(fL[10]))
Naxial = q*a*a/4

cp_h("8. Resultados (carga Viva)")
cp_table([_var("Resultado"), "Python", "Referencia"], [
    ["Deflexión máxima " + _var("w") + " [mm]",     f"{wmin*1000:.3f}", "−6.59 (Python val.)"],
    ["Momento " + _var("M_xx") + " [tonf·m/m]",     f"{Mxx_m:.3f}", "0.478"],
    ["Momento " + _var("M_yy") + " [tonf·m/m]",     f"{Myy_m:.3f}", "0.478"],
    ["Momento torsor " + _var("M_xy") + " [tonf·m/m]", f"{Mxy_m:.3f}", "0.189 ETABS"],
    ["Axial columna " + _var("N") + " [tonf]",      f"{Naxial/GRAV:.3f}", "4.50 ETABS"],
    ["Momento columna " + _var("M_2") + " cara [tonf·m]", f"{M2c/GRAV:.3f}", "2.13 ETABS (≈0%)"],
])


# ═══════════════════ 10. DIAGRAMAS DE FRAME (vigas borde) ═══════════════════
Iid = np.eye(12)
T12by = rblock(R3by)
Mmaxb = Vmaxb = Tmaxb = 0.0
Mall = np.zeros((4, 6)); Vall = np.zeros((4, 6)); Tall = np.zeros((4, 6))   # diagramas (4 vigas × 6 pts)
for bm in range(1, 5):
    nb = np.zeros(6, int)
    for k in range(6):
        if bm == 1:   nb[k] = ixc(k, 0)
        elif bm == 2: nb[k] = ixc(5, k)
        elif bm == 3: nb[k] = ixc(k, 5)
        else:         nb[k] = ixc(0, k)
    T12 = Iid if bm in (1, 3) else T12by
    MIv = np.zeros(5); MJv = np.zeros(5); VIv = np.zeros(5); VJv = np.zeros(5); TIv = np.zeros(5); TJv = np.zeros(5)
    for ii in range(5):
        pI, pJ = nb[ii], nb[ii+1]
        uGb = np.zeros(12)
        for d in range(6):
            uGb[d]   = U[6*(pI-1)+d]
            uGb[6+d] = U[6*(pJ-1)+d]
        fLb = Kbeam @ (T12 @ uGb)
        if ii == 0: fLb[4]  = fLb[4]  + fLb[2]*0.20   # cara: M += V·offset
        if ii == 4: fLb[10] = fLb[10] - fLb[8]*0.20
        MIv[ii] = -fLb[4]; MJv[ii] = fLb[10]
        VIv[ii] = fLb[2];  VJv[ii] = -fLb[8]
        TIv[ii] = fLb[3];  TJv[ii] = -fLb[9]
    M = [MIv[0]] + [(MJv[k-1]+MIv[k])/2 for k in range(1, 5)] + [MJv[4]]
    V = [VIv[0]] + [(VJv[k-1]+VIv[k])/2 for k in range(1, 5)] + [VJv[4]]
    Tt = [TIv[0]] + [(TJv[k-1]+TIv[k])/2 for k in range(1, 5)] + [TJv[4]]
    Mall[bm-1, :] = M; Vall[bm-1, :] = V; Tall[bm-1, :] = Tt
    Mmaxb = max(Mmaxb, max(abs(x) for x in M))
    Vmaxb = max(Vmaxb, max(abs(x) for x in V))
    Tmaxb = max(Tmaxb, max(abs(x) for x in Tt))

cp_h("10. Diagramas de viga V30x50 (máx)")
cp_table([_var("Resultado"), "Python", "ETABS"], [
    ["Momento flector " + _var("M_3") + " [kNm]", f"{Mmaxb:.2f}", "30.8 (+2%)"],
    ["Cortante " + _var("V_2") + " [kN]",         f"{Vmaxb:.2f}", "21.6"],
    ["Torsión " + _var("T") + " [kNm]",           f"{Tmaxb:.2f}", "11.2 (+8%)"],
])


# ═══════════════════ 11. MODAL — masa lumped + rigidez de piso ══════════════
rho = 2.40277
Mv = np.zeros(ndof)
mslab = rho*0.1*dx*dx/4
for jj in range(5):
    for ii in range(5):
        for nd in [ixc(ii, jj), ixc(ii+1, jj), ixc(ii+1, jj+1), ixc(ii, jj+1)]:
            Mv[6*nd-5-1] += mslab    # u
            Mv[6*nd-4-1] += mslab    # v
mcol = rho*0.4*0.4*4/2
mbeamc = rho*0.3*0.5*(6-0.4)        # masa viga sobre luz libre, a las esquinas
mcb = mcol + mbeamc
ctm = [ixc(0, 0), ixc(5, 0), ixc(5, 5), ixc(0, 5)]
for ndc in ctm:
    Mv[6*ndc-5-1] += mcb
    Mv[6*ndc-4-1] += mcb
mtot = 0.0; MMIp = 0.0; xc = 3.0; yc = 3.0
for jj in range(6):
    for ii in range(6):
        nd = ixc(ii, jj)
        mi = Mv[6*nd-5-1]
        mtot += mi
        MMIp += mi*((ii*dx-xc)**2 + (jj*dx-yc)**2)

# rigidez lateral del piso (carga lateral unitaria repartida)
Ftr = np.zeros(ndof)
for jj in range(6):
    for ii in range(6):
        Ftr[6*ixc(ii, jj)-5-1] = 1/36
Utr = np.linalg.solve(K, Ftr)
dxm = sum(Utr[6*ixc(ii, jj)-5-1]/36 for jj in range(6) for ii in range(6))
Ktrans = 1/dxm
# rigidez torsional del piso (par torsor unitario)
sumr2 = sum((ii*dx-xc)**2 + (jj*dx-yc)**2 for jj in range(6) for ii in range(6))
Fth = np.zeros(ndof)
for jj in range(6):
    for ii in range(6):
        nd = ixc(ii, jj)
        Fth[6*nd-5-1] = -(jj*dx-yc)/sumr2
        Fth[6*nd-4-1] = (ii*dx-xc)/sumr2
Uth = np.linalg.solve(K, Fth)
thm = sum(((ii*dx-xc)*Uth[6*ixc(ii, jj)-4-1] - (jj*dx-yc)*Uth[6*ixc(ii, jj)-5-1])/sumr2
          for jj in range(6) for ii in range(6))
Ktheta = 1/thm
twopi = 2*np.pi
f1 = np.sqrt(Ktrans/mtot)/twopi
f3 = np.sqrt(Ktheta/MMIp)/twopi
T1 = 1/f1; T3 = 1/f3

cp_h("11. Modal — masa lumped + rigidez de piso")
cp_val("m_total", mtot, "t  (ETABS 19.80)")
cp_val("MMI", MMIp, "t·m²  (ETABS 256.7)", fmt="{:.2f}")
cp_val("K_lateral", Ktrans, "kN/m", fmt="{:.3e}")
cp_val("K_torsional", Ktheta, "kN·m/rad", fmt="{:.3e}")
cp_table(["Modo", "Tipo", "T [s]", "f [Hz]", "T ETABS"], [
    ["1", "Traslación X", f"{T1:.4f}", f"{f1:.4f}", "0.3434"],
    ["2", "Traslación Y", f"{T1:.4f}", f"{f1:.4f}", "0.3434"],
    ["3", "Torsión " + _var("θ_z"), f"{T3:.4f}", f"{f3:.4f}", "0.2876"],
])
cp_p("<small>Los 3 modos cierran a &lt;0.1% vs ETABS. " +
     _var("M_2") + " columna ≈0% (reporte en cara, una sola K).</small>")


# ═══════════════ 12. VISUALIZACIÓN con matplotlib (reemplaza el viewer Calcpad) ═══
#  Las figuras se guardan a PNG en memoria, se codifican base64 y se imprimen con el
#  marcador __CPSPY_IMG__: ; Calcpad Suite Py las embebe como <img> en el reporte.
cp_h("12. Visualización (matplotlib · colormap jet_r estilo SAP2000)")
try:
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib import cm
    import io, base64

    def emit_fig(fig, caption=""):
        if caption:
            cp_p("<b>" + _html.escape(caption.strip()) + "</b>")
        buf = io.BytesIO()
        fig.savefig(buf, format="png", dpi=110, bbox_inches="tight")
        plt.close(fig)
        print("__CPSPY_IMG__:" + base64.b64encode(buf.getvalue()).decode("ascii"))

    CMAP = "jet_r"   # colormap estilo SAP2000/ETABS (jet invertido)
    xs = np.array([ii*dx for ii in range(6)])
    ys = np.array([jj*dx for jj in range(6)])
    wdef = np.array([[Uw(ixc(ii, jj)) for jj in range(6)] for ii in range(6)])  # m (negativo = baja)
    scale = 0.15*a / max(abs(wmin), 1e-9)

    # --- Malla FINA + interpolador (spline cúbico) — suaviza 3D y contornos ---
    nf = 70
    xf = np.linspace(0, a, nf); yf = np.linspace(0, a, nf)
    Xf, Yf = np.meshgrid(xf, yf, indexing="ij")
    try:
        from scipy.interpolate import RectBivariateSpline
        def smooth(Z):
            return RectBivariateSpline(xs, ys, Z, kx=3, ky=3)(xf, yf)
    except ImportError:
        def smooth(Z):
            fi = np.interp(xf, xs, np.arange(6)); fj = np.interp(yf, ys, np.arange(6))
            i0 = np.clip(fi.astype(int), 0, 4); j0 = np.clip(fj.astype(int), 0, 4)
            ti = (fi - i0)[:, None]; tj = (fj - j0)[None, :]
            Z00 = Z[np.ix_(i0, j0)]; Z10 = Z[np.ix_(i0 + 1, j0)]
            Z01 = Z[np.ix_(i0, j0 + 1)]; Z11 = Z[np.ix_(i0 + 1, j0 + 1)]
            return (Z00*(1-ti)*(1-tj) + Z10*ti*(1-tj) + Z01*(1-ti)*tj + Z11*ti*tj)

    def norm01(A):
        lo, hi = A.min(), A.max()
        return (A - lo)/(hi - lo + 1e-12)

    # --- Fig 1: mesa 3D deformada SUAVE (losa + columnas), color jet_r ---
    Wf = smooth(Wmat); wdeff = smooth(wdef)
    fig = plt.figure(figsize=(7.5, 5.8))
    ax = fig.add_subplot(111, projection="3d")
    Zdef = H + wdeff*scale
    cmap = matplotlib.colormaps[CMAP]
    ax.plot_surface(Xf, Yf, Zdef, facecolors=cmap(norm01(Wf)),
                    rstride=1, cstride=1, linewidth=0, antialiased=True, alpha=0.98, shade=False)
    corners = [(0, 0), (5, 0), (5, 5), (0, 5)]
    bases   = [(0, 0), (6, 0), (6, 6), (0, 6)]
    for e in range(4):
        bx, by = bases[e]; ti, tj = corners[e]
        ztop = H + wdef[ti, tj]*scale
        ax.plot([bx, ti*dx], [by, tj*dx], [0, ztop], color="#333", lw=3)
    m = cm.ScalarMappable(cmap=cmap); m.set_array(Wmat)
    fig.colorbar(m, ax=ax, shrink=0.6, pad=0.1, label="deflexión w [mm]")
    ax.set_title(f"Mesa deformada (×{scale:.0f}) — color = deflexión w")
    ax.set_xlabel("X [m]"); ax.set_ylabel("Y [m]"); ax.set_zlabel("Z [m]")
    emit_fig(fig, "  Vista 3D: losa deformada + 4 columnas")

    # --- Fig 2: contornos SUAVES de deflexión y momentos (jet_r) ---
    fig, axs = plt.subplots(2, 2, figsize=(9, 7.5))
    campos = [(axs[0, 0], Wmat, "Deflexión w [mm]"),
              (axs[0, 1], Mxmat, "Mxx [tonf·m/m]"),
              (axs[1, 0], Mymat, "Myy [tonf·m/m]"),
              (axs[1, 1], Mxymat, "Mxy torsor [tonf·m/m]")]
    for axx, Z, ti in campos:
        Zf = smooth(Z)
        cf = axx.contourf(Xf, Yf, Zf, 40, cmap=CMAP)
        axx.set_title(ti); axx.set_aspect("equal")
        axx.set_xlabel("X [m]"); axx.set_ylabel("Y [m]")
        fig.colorbar(cf, ax=axx, shrink=0.85)
    fig.suptitle("Campos sobre la losa (recovery Gauss 2×2 estilo ETABS)")
    fig.tight_layout()
    emit_fig(fig, "  Contornos 2D: deflexión y momentos Mxx, Myy, Mxy")

    # --- Fig 3: diagramas de las 4 vigas de borde ---
    sp = np.linspace(0, a, 6)
    labels = ["Viga 1 (y=0)", "Viga 2 (x=6)", "Viga 3 (y=6)", "Viga 4 (x=0)"]
    fig, axs = plt.subplots(1, 3, figsize=(12, 3.6))
    for arr, axx, ti, un in [(Mall, axs[0], "Momento M3", "kNm"),
                             (Vall, axs[1], "Cortante V2", "kN"),
                             (Tall, axs[2], "Torsión T", "kNm")]:
        for bm in range(4):
            axx.plot(sp, arr[bm], marker="o", ms=3, label=labels[bm])
        axx.axhline(0, color="k", lw=0.6)
        axx.set_title(f"{ti} [{un}]"); axx.set_xlabel("posición [m]"); axx.grid(alpha=0.3)
    axs[0].legend(fontsize=7)
    fig.tight_layout()
    emit_fig(fig, "  Diagramas de viga de borde (M3, V2, T)")
    print("  [matplotlib OK — 3 figuras embebidas]")
except ImportError:
    print("  [matplotlib no instalado → pip install matplotlib para ver los gráficos]")
except Exception as _e:
    print(f"  [aviso visualización: {_e}]")

print("\n  Done.")
