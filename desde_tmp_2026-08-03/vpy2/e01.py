# %% Variables y aritmética
# Calcpad Suite Py — el motor Python nativo en C# muestra cada asignación
# como una línea de hoja de cálculo: nombre = valor.

# Datos de entrada
a = 12
b = 5

# Operaciones básicas
suma = a + b
resta = a - b
producto = a * b
division = a / b          # división real → float
division_entera = a // b  # floor division
resto = a % b
potencia = a ** 2

# Notación de ingeniería
E = 200000.0   # módulo de Young [MPa]
I = 8.5e7      # inercia [mm^4]
L = 6000.0     # longitud [mm]

# Expresión suelta (se muestra como "expr = valor")
E * I / L**3
