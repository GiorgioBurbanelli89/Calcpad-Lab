# -*- coding: utf-8 -*-
# %% Daño en concreto — elasticidad dañada uniaxial (tipo CDP escalar)
#' Modelo constitutivo del hormigon con DANO ESCALAR d (damaged elasticity):
#'      sigma(eps) = (1 - d(eps)) * E * eps ,   con  d in [0, 1]
#' El dano crece al ablandarse (softening): tras el pico, la rigidez efectiva
#' (1-d)*E cae y el material pierde capacidad de carga.
#'
#' Convencion de signos: traccion eps>0 positiva, compresion eps<0.
#'  - Traccion:  rama elastica hasta ft, luego softening EXPONENCIAL.
#'  - Compresion: parabola ascendente (Kent-Park/Hognestad) hasta -fc,
#'                luego rama descendente lineal hasta un residual de 0.2*fc.
#'
#' Corre con el motor Python EMBEBIDO de Calcpad Suite Py (numpy + matplotlib
#' nativos, sin Python externo).
import math
import matplotlib.pyplot as plt

# %% Propiedades del material
E   = 30000.0     # modulo elastico [MPa]
fc  = 30.0        # resistencia a compresion [MPa]
ec0 = 0.002       # deformacion en el pico de compresion
ecu = 0.0035      # deformacion ultima de compresion
ft  = 3.0         # resistencia a traccion [MPa]
et0 = ft / E      # deformacion de fisuracion = 0.0001
etf = 0.0005      # deformacion caracteristica de softening en traccion

# Pendiente de la rama descendente en compresion:
# lleva de 1.0*fc en ec0 hasta 0.2*fc en ecu.
Z = (1.0 - 0.2) / (ecu - ec0)     # = 533.33


# %% Ley constitutiva sigma(eps)
def sigma(eps):
    if eps >= 0.0:                              # ----- TRACCION -----
        if eps <= et0:
            return E * eps                      # elastico
        return ft * math.exp(-(eps - et0) / etf)   # softening exponencial
    else:                                       # ----- COMPRESION -----
        x = -eps                                # magnitud de la deformacion
        if x <= ec0:
            r = x / ec0
            return -fc * (2.0 * r - r * r)      # parabola ascendente
        return -fc * max(0.2, 1.0 - Z * (x - ec0))  # descendente + residual


# %% Dano escalar d(eps) = 1 - sigma/(E*eps)
def dano(eps):
    if eps == 0.0:
        return 0.0                              # sin deformacion, sin dano
    d = 1.0 - sigma(eps) / (E * eps)
    return min(1.0, max(0.0, d))                # clamp a [0, 1]


# %% Barrido de deformaciones con un LOOP (200 puntos de -0.0035 a +0.0006)
N   = 200
e0  = -0.0035
e1  =  0.0006
eps_list, sig_list, dam_list = [], [], []
for i in range(N):
    eps = e0 + (e1 - e0) * i / (N - 1)
    eps_list.append(eps)
    sig_list.append(sigma(eps))
    dam_list.append(dano(eps))

# %% Valores de referencia (VALIDADOS)
print("  eps        sigma[MPa]   d")
for e in (0.0001, 0.0003, 0.0010, -0.0010, -0.0020, -0.0030, -0.0035):
    print(f"{e:+.5f}    {sigma(e):8.3f}   {dano(e):.3f}")
print()
print(f"Pico traccion  : sigma = {ft:.1f} MPa en eps = {et0:.5f}  (d = 0)")
print(f"Pico compresion: sigma = {-fc:.1f} MPa en eps = {-ec0:.5f}  (d = 0.50)")
print(f"Residual (ecu) : sigma = {sigma(-ecu):.1f} MPa en eps = {-ecu:.5f}  (d = {dano(-ecu):.3f})")

# %% Grafica 1 — curva constitutiva sigma vs eps (softening)
f1, a1 = plt.subplots(figsize=(6.5, 4))
sc = a1.scatter(eps_list, sig_list, c=dam_list, cmap='jet_r', s=12, vmin=0, vmax=1)
a1.plot(eps_list, sig_list, color='#444', lw=0.8, zorder=0)
a1.axhline(0, color='#999', lw=0.6); a1.axvline(0, color='#999', lw=0.6)
a1.set_title('Curva constitutiva del concreto con dano')
a1.set_xlabel('deformacion  eps'); a1.set_ylabel('esfuerzo  sigma [MPa]')
a1.grid(alpha=.3)
cb = f1.colorbar(sc, ax=a1); cb.set_label('dano  d')
plt.show()

# %% Grafica 2 — evolucion del dano d vs eps
f2, a2 = plt.subplots(figsize=(6.5, 4))
a2.plot(eps_list, dam_list, color='#c0392b', lw=1.8)
a2.axvline(0, color='#999', lw=0.6)
a2.set_ylim(-0.02, 1.02)
a2.set_title('Evolucion del dano escalar')
a2.set_xlabel('deformacion  eps'); a2.set_ylabel('dano  d  [0 = intacto, 1 = destruido]')
a2.grid(alpha=.3)
plt.show()
