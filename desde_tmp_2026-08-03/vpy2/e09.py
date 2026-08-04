# -*- coding: utf-8 -*-
# %% Cyclic Damage — concreto ciclico no lineal (histeresis, degradacion de rigidez)
#' Mismo modelo constitutivo de `concrete_damage` (elasticidad danada  sigma=(1-d)*E*eps),
#' pero ahora el DANO es ESTADO PERSISTENTE (maximo historico). Bajo reversas de carga:
#'  - la descarga/recarga viaja sobre la SECANTE danada  (1-d)*E  hacia el origen,
#'  - la pendiente se APLANA cada ciclo (rigidez degradada),
#'  - al invertir el signo (compresion <-> traccion) actua la rama con SU propio dano
#'    (CIERRE DE FISURA: la rigidez se recupera al recomprimir).
#' Reproduce EXACTO el uniaxialMaterial ASDConcrete1D de ASDEA/STKO (elasticidad danada
#' secante, sin deformacion plastica residual).
#'
#' Corre con el motor Python EMBEBIDO de Calcpad Suite Py (numpy + matplotlib nativos,
#' sin Python externo).
import math
import matplotlib.pyplot as plt

# %% Propiedades del material (identicas a concrete_damage)
E   = 30000.0     # modulo elastico [MPa]
fc  = 30.0        # resistencia a compresion [MPa]
ec0 = 0.002       # deformacion en el pico de compresion
ecu = 0.0035      # deformacion ultima de compresion
ft  = 3.0         # resistencia a traccion [MPa]
et0 = ft / E      # deformacion de fisuracion = 0.0001
etf = 0.0005      # deformacion caracteristica de softening en traccion

# Pendiente de la rama descendente en compresion: de 1.0*fc en ec0 a 0.2*fc en ecu.
Z = (1.0 - 0.2) / (ecu - ec0)     # = 533.33


# %% Envelope monotonico sigma(eps)  (reusado de concrete_damage)
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


# %% Dano secante del envelope  d(eps) = 1 - sigma/(E*eps)  (reusado de concrete_damage)
def dano(eps):
    if eps == 0.0:
        return 0.0
    d = 1.0 - sigma(eps) / (E * eps)
    return min(1.0, max(0.0, d))                # clamp a [0, 1]


# %% Maquina de estado ciclica — dano como MAXIMO HISTORICO (persistente)
#' etmax = maximo eps>0 alcanzado (fisura de traccion), ecmax = maximo |eps| en compresion.
#' Nunca se resetean: el dano SOLO crece. La rama activa usa SU dano congelado ->
#' descarga/recarga secante  (1-d)*E  al origen.
etmax = 0.0    # umbral de dano en traccion  (deformacion positiva historica)
ecmax = 0.0    # umbral de dano en compresion (magnitud negativa historica)

def paso_ciclico(eps):
    """Devuelve (sigma, dt, dc) para una deformacion eps con el estado actual."""
    global etmax, ecmax
    if eps >= 0.0:                              # rama de TRACCION activa
        if eps > etmax:
            etmax = eps                         # avanza la fisura de traccion
        dt = dano(etmax)                        # dano congelado del maximo historico
        s  = (1.0 - dt) * E * eps               # secante danada al origen
    else:                                       # rama de COMPRESION activa
        if -eps > ecmax:
            ecmax = -eps                        # avanza el aplastamiento
        dc = dano(-ecmax)                       # dano congelado del maximo historico
        s  = (1.0 - dc) * E * eps               # secante danada al origen
    return s, dano(etmax), dano(ecmax if ecmax == 0.0 else -ecmax)


# %% Protocolo de deformacion (reversas de amplitud creciente) — sin notacion cientifica
puntos = [0.0, -0.0006, 0.0, -0.0012, 0.0, -0.0020, 0.0,
          -0.0030, 0.0, 0.0001, 0.0, -0.0040]
NSUB = 40                                       # subpasos por segmento

# Deformaciones interpoladas linealmente entre cada par de puntos (rampa)
eps_prot, _h = [], 0                            # tuple-unpack: no se imprime el vector largo
for k in range(len(puntos) - 1):
    a, b = puntos[k], puntos[k + 1]
    for j in range(NSUB):
        eps_prot.append(a + (b - a) * j / NSUB)
eps_prot.append(puntos[-1])                     # cierra el ultimo punto

# %% Recorre el protocolo con el estado persistente
eps_hist, sig_hist, dt_hist, dc_hist = [], [], [], []
for eps in eps_prot:
    s, dt, dc = paso_ciclico(eps)
    eps_hist.append(eps)
    sig_hist.append(s)
    dt_hist.append(dt)
    dc_hist.append(dc)

# Dano por punto (para colorear la histeresis con jet_r): rama activa
dam_hist, _h = [dt_hist[i] if eps_hist[i] >= 0.0 else dc_hist[i]
                for i in range(len(eps_hist))], 0     # tuple-unpack: no se imprime

# %% Tabla de PICOS del protocolo (deben coincidir con el envelope validado)
print("  Picos del protocolo ciclico (sobre el envelope):")
print("  eps        sigma[MPa]   d")
# reinicia el estado para evaluar cada pico limpio sobre el envelope
for e in (-0.0006, -0.0012, -0.0020, -0.0030, -0.0040, 0.0001):
    print(f"{e:+.5f}    {sigma(e):8.3f}   {dano(e):.3f}")
print()
print(f"  Estado final: etmax = {etmax:.5f} (dt = {dano(etmax):.3f}),"
      f"  ecmax = {ecmax:.5f} (dc = {dano(-ecmax):.3f})")

# %% Grafica 1 — HISTERESIS  sigma vs eps  (lazos de descarga/recarga secante)
f1, a1 = plt.subplots(figsize=(6.8, 4.4))
a1.plot(eps_hist, sig_hist, color='#555', lw=0.9, zorder=1)   # traza los lazos
sc = a1.scatter(eps_hist, sig_hist, c=dam_hist, cmap='jet_r',
                s=14, vmin=0, vmax=1, zorder=2)
a1.axhline(0, color='#999', lw=0.6); a1.axvline(0, color='#999', lw=0.6)
a1.set_title('Histeresis del concreto — elasticidad danada ciclica')
a1.set_xlabel('deformacion  eps'); a1.set_ylabel('esfuerzo  sigma [MPa]')
a1.grid(alpha=.3)
cb = f1.colorbar(sc, ax=a1); cb.set_label('dano  d  (rama activa)')
plt.show()

# %% Grafica 2 — degradacion de rigidez: dano dt / dc crece (persistente)
f2, a2 = plt.subplots(figsize=(6.8, 4.0))
pasos, _h = list(range(len(eps_hist))), 0       # tuple-unpack: no se imprime
a2.plot(pasos, dc_hist, color='#c0392b', lw=1.8, label='dc (compresion)')
a2.plot(pasos, dt_hist, color='#2980b9', lw=1.8, label='dt (traccion)')
a2.set_ylim(-0.02, 1.02)
a2.set_title('Degradacion — el dano solo crece (estado persistente)')
a2.set_xlabel('paso del protocolo'); a2.set_ylabel('dano  d  [0 = intacto, 1 = destruido]')
a2.grid(alpha=.3); a2.legend(loc='upper left')
plt.show()
