# -*- coding: utf-8 -*-
#" numpy EMBEBIDO en Calcpad Suite Py
#' Este script usa numpy ESTANDAR (import numpy as np). Lo corre el motor C#
#' embebido (sin Python externo): array, @, .T, linalg.solve van a OpenBLAS.
#' El MISMO archivo corre igual en IDLE/VS Code con numpy real.
import numpy as np

#" 1) Rigidez de 3 resortes en serie (k.u = f)
k1 = 100.0
k2 = 150.0
k3 = 200.0

#' Matriz de rigidez global (3 grados de libertad, nodo 0 empotrado)
K = np.array([
    [k1 + k2, -k2,      0.0],
    [-k2,      k2 + k3, -k3],
    [0.0,     -k3,       k3],
])

#' Vector de cargas (kN) en cada nodo
f = np.array([10.0, 5.0, 8.0])

#" 2) Resolver el sistema  K . u = f   (OpenBLAS, DGESV)
u = np.linalg.solve(K, f)

#" 3) Reacciones y comprobacion
f_recuperado = K @ u
traza = np.trace(K)
Kt = K.T

#' Desplazamientos nodales u [mm]:
u
#' Comprobacion  K @ u  (debe dar f = 10, 5, 8):
f_recuperado
#' Traza de K:
traza
#' Norma del residuo (debe ser ~0):
np.linalg.norm(K @ u - f)
