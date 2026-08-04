# %% Listas, comprensiones y diccionarios
# Las listas numéricas se muestran como matrices estilo Calcpad.

# Lista literal
cargas = [10, 25, 40, 15, 30]   # cargas por nudo [kN]

# Funciones de agregación
total = sum(cargas)
maxima = max(cargas)
minima = min(cargas)
n = len(cargas)
promedio = total / n

# Comprensión de lista
mayoradas = [1.5 * q for q in cargas]
grandes = [q for q in cargas if q > 20]

# Comprensión con enumerate
indexadas = [(i, q) for i, q in enumerate(cargas)]

# Diccionario
material = {"E": 200000, "fy": 420, "nu": 0.3}
modulo = material["E"]
claves = list(material.keys())

# Slicing
primeros_tres = cargas[:3]
ultimo = cargas[-1]
invertida = cargas[::-1]
