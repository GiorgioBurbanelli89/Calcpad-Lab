# -*- coding: utf-8 -*-
# FEA de Losa Rectangular — Python (numpy) + ecuaciones Calcpad (#cp).
# Replica del ejemplo "Rectangular Slab FEA" de Calcpad: CALCULO en numpy,
# ECUACIONES con #cp. Corre igual en Python real (las lineas #cp son comentarios).
import numpy as np
#noauto      # Suite Py: no volcar todas las variables; solo #cp / #show / print / graficas

#" Analisis por Elementos Finitos de una Losa Rectangular
#' Datos de entrada:
#cp[
# a = 6 m
# b = 4 m
# t = 0.1 m
# q = 10 kN/m^2
# E = 35000 MPa
# ν = 0.15
#cp]

# --- Datos en Python (SI: m, N) ---
a, b, t = 6.0, 4.0, 0.1
q, E, nu = 10.0e3, 35000.0e6, 0.15

#' Rigidez de flexion de la placa:
#cp D = E*t^3/(12*(1 - ν^2))
D = E*t**3 / (12.0*(1.0 - nu**2))
#show D

#' Ecuacion de gobierno (flexion de placa delgada, Kirchhoff):
#cp[
# #deq D*∂^4w/∂x^4 + 2*D*∂^4w/∂x^2∂y^2 + D*∂^4w/∂y^4 = q
#cp]
#' Curvaturas (2da derivada parcial, con simbolo ∂):
#cp[
# #deq κ_x = -∂^2w/∂x^2
# #deq κ_y = -∂^2w/∂y^2
# #deq κ_xy = -2*∂^2w/∂x∂y
#cp]

#" Funciones de forma (Hermite, elemento de placa)
#' Funciones base en direccion a (definiciones, sin evaluar):
#cp[
# #noc Φ_1a(ξ) = 1 - ξ^2*(3 - 2*ξ)
# #noc Φ_2a(ξ) = ξ*a_1*(1 - ξ*(2 - ξ))
# #noc Φ_3a(ξ) = ξ^2*(3 - 2*ξ)
# #noc Φ_4a(ξ) = ξ^2*a_1*(-1 + ξ)
#cp]
#' Segundas derivadas PARCIALES (pdiff → simbolo ∂, calculadas por AngouriMath):
#cp[
# #sym
# pdiff(1 - ξ^2*(3 - 2*ξ); ξ; 2)
# pdiff(ξ^2*(3 - 2*ξ); ξ; 2)
# #end sym
#cp]
#' Integrales ($Area) y pendientes/derivadas ($Slope) de Calcpad:
#cp[
# f(x) = x^2
# I = $Area{f(x) @ x = 0 : 2}
# s = $Slope{f(x) @ x = 3}
#cp]

#" Matriz de rigidez del elemento (energia de deformacion)
#' Doble integral sobre el elemento; el Jacobiano |J| pasa de (x,y) a (ξ,η):
#cp[
# B = 1
# D_b = 1
# J = 1
# #noc
# K_e = $Area{$Area{transp(B)*D_b*B*abs(J) @ η = -1 : 1} @ ξ = -1 : 1}
#cp]

#" Malla de elementos finitos
na, nb = 12, 8
ne, nj = na*nb, (na+1)*(nb+1)
a1, b1 = a/na, b/nb
#cp[
# n_a = 12
# n_b = 8
# n_e = n_a*n_b
# n_j = (n_a + 1)*(n_b + 1)
#cp]
ndof_total = 3*nj
#show ndof_total

# --- Matriz de rigidez del elemento placa (12x12): C^-1 + Gauss 3x3 ---
def acm_ke(hx, hy, D, nu):
    ax, ay = hx/2.0, hy/2.0
    nodes = [(-ax, -ay), (ax, -ay), (ax, ay), (-ax, ay)]
    P   = lambda x, y: np.array([1, x, y, x*x, x*y, y*y, x**3, x*x*y, x*y*y, y**3, x**3*y, x*y**3], float)
    Px  = lambda x, y: np.array([0, 1, 0, 2*x, y, 0, 3*x*x, 2*x*y, y*y, 0, 3*x*x*y, y**3], float)
    Py  = lambda x, y: np.array([0, 0, 1, 0, x, 2*y, 0, x*x, 2*x*y, 3*y*y, x**3, 3*x*y*y], float)
    Pxx = lambda x, y: np.array([0, 0, 0, 2, 0, 0, 6*x, 2*y, 0, 0, 6*x*y, 0], float)
    Pyy = lambda x, y: np.array([0, 0, 0, 0, 0, 2, 0, 0, 2*x, 6*y, 0, 6*x*y], float)
    Pxy = lambda x, y: np.array([0, 0, 0, 0, 1, 0, 0, 2*x, 2*y, 0, 3*x*x, 3*y*y], float)
    C = np.zeros((12, 12)); r = 0
    for (xn, yn) in nodes:
        C[r] = P(xn, yn);   r += 1
        C[r] = Py(xn, yn);  r += 1
        C[r] = -Px(xn, yn); r += 1
    Cinv = np.linalg.inv(C)
    Db = D*np.array([[1.0, nu, 0.0], [nu, 1.0, 0.0], [0.0, 0.0, (1.0-nu)/2.0]])
    g = np.sqrt(3.0/5.0); pts = [-g, 0.0, g]; wts = [5/9, 8/9, 5/9]
    Ke = np.zeros((12, 12))
    for i, xi in enumerate(pts):
        for j, et in enumerate(pts):
            x, y = xi*ax, et*ay
            B = -np.vstack([Pxx(x, y) @ Cinv, Pyy(x, y) @ Cinv, 2.0*Pxy(x, y) @ Cinv])
            Ke += wts[i]*wts[j]*ax*ay*(B.T @ Db @ B)
    return Ke

# --- Ensamblaje + resolver ---
ndof = 3*nj
K = np.zeros((ndof, ndof)); F = np.zeros(ndof)
jid = lambda ix, iy: iy + (nb+1)*ix
Ke = acm_ke(a1, b1, D, nu)
qe = q*a1*b1/4.0
for ix in range(na):
    for iy in range(nb):
        nodes = [jid(ix, iy), jid(ix+1, iy), jid(ix+1, iy+1), jid(ix, iy+1)]
        dofs = [3*n+k for n in nodes for k in range(3)]
        for r in range(12):
            if r % 3 == 0: F[dofs[r]] += qe
            for c in range(12):
                K[dofs[r], dofs[c]] += Ke[r, c]
fixed = [3*jid(ix, iy) for ix in range(na+1) for iy in range(nb+1)
         if ix in (0, na) or iy in (0, nb)]
free = [d for d in range(ndof) if d not in fixed]
dsol = np.zeros(ndof)
dsol[free] = np.linalg.solve(K[np.ix_(free, free)], F[free])

# Deflexiones en la grilla (mm)
W = np.zeros((na+1, nb+1)); X = np.zeros_like(W); Y = np.zeros_like(W)
for ix in range(na+1):
    for iy in range(nb+1):
        X[ix, iy] = ix*a1; Y[ix, iy] = iy*b1
        W[ix, iy] = -dsol[3*jid(ix, iy)]*1000.0
w_max_fem = abs(W).max()

# Navier (losa SS, carga uniforme)
wN = sum(np.sin(m*np.pi/2)*np.sin(n*np.pi/2)/(m*n*(m**2/a**2 + n**2/b**2)**2)
         for m in range(1, 40, 2) for n in range(1, 40, 2))
w_navier = 16*q/(np.pi**6*D)*wN*1000.0
err = 100*(w_max_fem - w_navier)/w_navier

#" Resultados (en formato Calcpad, no print monoespaciado)
w_FEM = round(w_max_fem, 3)
#show w_FEM
w_Navier = round(w_navier, 3)
#show w_Navier
error_pct = round(err, 1)
#show error_pct

#' Solucion analitica de Navier (serie doble). Con #noc se muestra la formula
#' con las sumatorias ∑∑ pero SIN el resultado numerico (solo la ecuacion):
#cp[
# a = 6 m
# b = 4 m
# q = 10 kN/m^2
# D = 2983.8 kN*m
# #noc
# w_Navier = 16*q/(π^6*D)*$Sum{$Sum{sin(m*π/2)*sin(n*π/2)/(m*n*(m^2/a^2 + n^2/b^2)^2) @ n = 1 : 15} @ m = 1 : 15}
#cp]

# --- Deformada 3D con hover cursor (matplotlib -> GL3 en Suite Py) ---
import matplotlib.pyplot as plt
fig = plt.figure(figsize=(7, 4.5))
ax3 = fig.add_subplot(111, projection='3d')
ax3.plot_surface(X, Y, W, cmap='jet_r', edgecolor='k', linewidth=0.2)
ax3.set_title('Deformada de la losa  w [mm]')
ax3.set_xlabel('x [m]'); ax3.set_ylabel('y [m]'); ax3.set_zlabel('w [mm]')
plt.show()
