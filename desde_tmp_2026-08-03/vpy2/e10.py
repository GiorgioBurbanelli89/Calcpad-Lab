# -*- coding: utf-8 -*-
# %% Shell Damage — seccion de cascara RC por capas (M-kappa con dano de concreto)
#' Seccion de CASCARA/PLACA de hormigon armado, por metro de ancho, analizada por
#' CAPAS (layered shell section). Se calcula la relacion MOMENTO-CURVATURA con
#' DANO DE CONCRETO POR CAPA (reusa el mismo modelo de concrete_damage). La cascara
#' se ABLANDA (softening) al crecer el dano en la fibra comprimida = "shell damage".
#'
#' Formulacion por deformacion plana de la seccion:  eps(z) = eps0 + kappa * z
#'  - Para cada curvatura kappa se resuelve eps0 por EQUILIBRIO AXIAL N(eps0)=0
#'    (biseccion), y luego se integra el MOMENTO sobre las capas.
#'  - Cada capa de concreto usa la ley con dano escalar sigma=(1-d)*E*eps.
#'
#' Corre con el motor Python EMBEBIDO de Calcpad Suite Py (numpy + matplotlib
#' nativos, sin Python externo).
import math
import matplotlib.pyplot as plt

# %% Propiedades del material (concreto = MISMO que concrete_damage)
E   = 30000.0     # modulo elastico [MPa]
fc  = 30.0        # resistencia a compresion [MPa]
ec0 = 0.002       # deformacion en el pico de compresion
ecu = 0.0035      # deformacion ultima de compresion
ft  = 3.0         # resistencia a traccion [MPa]
et0 = ft / E      # deformacion de fisuracion = 0.0001
etf = 0.0005      # deformacion caracteristica de softening en traccion

# Pendiente de la rama descendente en compresion (1.0*fc en ec0 -> 0.2*fc en ecu)
Z = (1.0 - 0.2) / (ecu - ec0)     # = 533.33

# Acero (elasto-plastico)
Es = 200000.0     # modulo del acero [MPa]
fy = 420.0        # limite de fluencia [MPa]


# %% Ley constitutiva del concreto sigma_c(eps)  (traccion eps>0, compresion eps<0)
def sc(eps):
    if eps >= 0.0:                                  # ----- TRACCION -----
        if eps <= et0:
            return E * eps                          # elastico
        return ft * math.exp(-(eps - et0) / etf)    # softening exponencial
    else:                                           # ----- COMPRESION -----
        x = -eps                                    # magnitud
        if x <= ec0:
            r = x / ec0
            return -fc * (2.0 * r - r * r)          # parabola ascendente
        return -fc * max(0.2, 1.0 - Z * (x - ec0))  # descendente + residual


# %% Dano escalar del concreto d(eps) = 1 - sigma/(E*eps)
def dano(eps):
    if eps == 0.0:
        return 0.0
    d = 1.0 - sc(eps) / (E * eps)
    return min(1.0, max(0.0, d))


# %% Ley del acero sigma_s(eps) — elasto-plastico
def ss(eps):
    return min(fy, max(-fy, Es * eps))


# %% Geometria de la seccion (por metro de ancho)
t     = 200.0     # espesor [mm]
b     = 1000.0    # ancho [mm/m]
nc    = 20        # numero de capas de concreto
dz    = t / nc    # espesor de capa = 10 mm
As    = 1000.0    # area de acero por cara [mm2/m]
cover = 25.0      # recubrimiento [mm]
zs    = t / 2.0 - cover   # brazo del acero = 75 mm

# Centro de cada capa de concreto:  z_i = -t/2 + dz*(i-0.5)
z_lay = [(-t / 2.0 + dz * (i - 0.5)) for i in range(1, nc + 1)]


# %% Fuerza axial N(eps0) para una curvatura dada (equilibrio de la seccion)
def axial_N(eps0, kappa):
    N = 0.0
    for z in z_lay:                                 # concreto por capas
        N += sc(eps0 + kappa * z) * b * dz
    N += ss(eps0 + kappa * zs) * As                 # acero cara superior (+zs)
    N += ss(eps0 - kappa * zs) * As                 # acero cara inferior (-zs)
    return N


# %% Resolver eps0 por BISECCION para que N(eps0)=0
def solve_eps0(kappa):
    a, c = -0.01, 0.01                              # intervalo de busqueda
    fa = axial_N(a, kappa)
    for _ in range(60):                             # ~60 iteraciones
        m = 0.5 * (a + c)
        fm = axial_N(m, kappa)
        if fa * fm <= 0.0:
            c = m
        else:
            a, fa = m, fm
    return 0.5 * (a + c)


# %% Momento M(kappa) integrando sobre las capas + acero
def moment(eps0, kappa):
    M = 0.0
    for z in z_lay:
        M += sc(eps0 + kappa * z) * b * dz * z
    M += ss(eps0 + kappa * zs) * As * zs
    M += ss(eps0 - kappa * zs) * As * (-zs)
    return M / 1.0e6                                # N*mm -> kN*m


# %% Barrido de curvatura con un LOOP (200 puntos de 0 a 4e-5)
N     = 200
k1    = 4.0e-5
kap_list, M_list, dc_list = [], [], []
for i in range(N):
    kappa = k1 * i / (N - 1)
    eps0  = solve_eps0(kappa)
    kap_list.append(kappa)
    M_list.append(moment(eps0, kappa))
    dc_list.append(dano(eps0 - kappa * t / 2.0))    # dano en fibra comprimida

# Momento pico y su curvatura
Mpk  = max(M_list)
ipk  = M_list.index(Mpk)
kpk  = kap_list[ipk]

# Curvatura re-escalada para graficar (x1e-6 1/mm): el lienzo 2D serializa con 4
# decimales, y kappa ~1e-5 se colapsaria a 0. Multiplicar por 1e6 la lleva a 0..40.
# tuple-unpack -> no se imprime el vector largo.
kap_u, _h = [k * 1.0e6 for k in kap_list], 0
kpk_u     = kpk * 1.0e6

# %% Valores de referencia (VALIDADOS)
print("  kappa[1/mm]   M[kN*m/m]   d_comp")
for kt in (5.0e-6, 1.0e-5, 1.5e-5, 1.64e-5, 2.0e-5, 3.0e-5, 4.0e-5):
    e0 = solve_eps0(kt)
    print(f"  {kt:.2e}    {moment(e0, kt):8.2f}   {dano(e0 - kt * t / 2.0):.3f}")
print()
print(f"Momento pico   : M = {Mpk:.2f} kN*m/m  en  kappa = {kpk:.2e} 1/mm")
print(f"Momento a 4e-5 : M = {M_list[-1]:.2f} kN*m/m  (ablandamiento por dano)")

# %% Grafica 1 — Momento-curvatura M vs kappa (con softening por dano)
f1, a1 = plt.subplots(figsize=(6.5, 4))
sc1 = a1.scatter(kap_u, M_list, c=dc_list, cmap='jet_r', s=12, vmin=0, vmax=0.5)
a1.plot(kap_u, M_list, color='#444', lw=0.8, zorder=0)
a1.scatter([kpk_u], [Mpk], color='k', s=40, zorder=3, marker='v')
a1.annotate(f'pico {Mpk:.1f}', (kpk_u, Mpk), textcoords='offset points',
            xytext=(6, -14), fontsize=9)
a1.set_title('Cascara RC por capas: Momento-curvatura con dano')
a1.set_xlabel('curvatura  kappa  [x1e-6 1/mm]'); a1.set_ylabel('momento  M  [kN*m/m]')
a1.grid(alpha=.3)
cb = f1.colorbar(sc1, ax=a1); cb.set_label('dano en fibra comprimida  d_comp')
plt.show()

# %% Grafica 2 — evolucion del dano en la fibra comprimida d_comp vs kappa
f2, a2 = plt.subplots(figsize=(6.5, 4))
a2.plot(kap_u, dc_list, color='#c0392b', lw=1.8)
a2.axvline(kpk_u, color='#999', lw=0.8, ls='--')
a2.set_ylim(-0.02, max(dc_list) * 1.15 + 0.02)
a2.set_title('Evolucion del dano en la fibra comprimida')
a2.set_xlabel('curvatura  kappa  [x1e-6 1/mm]'); a2.set_ylabel('dano  d_comp  [0 = intacto]')
a2.grid(alpha=.3)
plt.show()
