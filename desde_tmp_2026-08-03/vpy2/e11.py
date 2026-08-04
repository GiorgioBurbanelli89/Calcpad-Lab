# %% Funciones definidas por el usuario
import math

# Función con valor por defecto
def momento_inercia(b, h):
    return b * h**3 / 12

# Función con varios retornos (tupla)
def propiedades(b, h):
    A = b * h
    I = momento_inercia(b, h)
    S = I / (h / 2)
    return A, I, S

# Lambda
flecha = lambda q, L, E, I: 5 * q * L**4 / (384 * E * I)

# %% Uso
A, I, S = propiedades(300, 500)
print(f"A = {A:.0f} mm2 ; I = {I:.3e} mm4 ; S = {S:.3e} mm3")

# Flecha de viga simplemente apoyada con carga uniforme
delta = flecha(0.03, 6000, 200000, I)   # q en N/mm
print(f"flecha máxima = {delta:.2f} mm")

# Función recursiva
def factorial(n):
    if n <= 1:
        return 1
    return n * factorial(n - 1)

f5 = factorial(5)
