# -*- coding: utf-8 -*-
# Demo: notebook de ingenieria (Markdown + formulas Calcpad + calculo Python).
# Todo va en comentarios #, asi que este .py corre IGUAL en Python real / IDLE.
import numpy as np

#" Notebook de ingenieria en Calcpad Suite Py
#' Este documento mezcla **texto Markdown**, *formulas simbolicas* y el ~~calculo~~ real.
#' Lo que puedes usar:
#' - **`#'`** para texto **Markdown** (negrita, listas, tablas)
#' - **`#cp`** para formulas de Calcpad (simbolos, derivadas, integrales)
#' - **`#show`** para mostrar un resultado con formato matematico

#" 1. Datos (formulas Calcpad)
#cp[
# b = 0.30 m
# h = 0.50 m
# f_c = 21 MPa
#cp]

# --- calculo en Python ---
b, h, fc = 0.30, 0.50, 21.0

#' Modulo de la seccion e inercia:
#cp I = b*h^3/12
#cp W = b*h^2/6
I = b*h**3/12
W = b*h**2/6
#show I
#show W

#" 2. Simbolico: derivadas e integrales
#' Derivada parcial (simbolo ∂, la calcula AngouriMath):
#cp[
# #sym
# pdiff(x^3 - 2*x^2 + x; x; 2)
# #end sym
#cp]
#' Integral definida (∫) y sumatoria (∑):
#cp[
# g(x) = x^2
# A = $Area{g(x) @ x = 0 : 3}
# S = $Sum{k^2 @ k = 1 : 5}
#cp]

#" 3. Tabla de resultados (Markdown)
#' | Propiedad | Simbolo | Valor |
#' |---|---|---|
#' | Base | b | 0.30 m |
#' | Altura | h | 0.50 m |
#' | Inercia | I | b·h³/12 |

print("Listo. I =", round(I, 5), "m4")
