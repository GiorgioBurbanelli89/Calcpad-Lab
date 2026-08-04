# %% Control de flujo: if / for / while
# Las variables se muestran; los bucles corren en silencio salvo lo que impriman.

# Condicional
fc = 25.0
if fc >= 30:
    clase = "alta resistencia"
elif fc >= 20:
    clase = "convencional"
else:
    clase = "baja"

# Operador ternario
estado = "OK" if fc >= 21 else "REVISAR"

# Bucle for con acumulador
momento_total = 0.0
for tramo in range(1, 6):
    momento_total += tramo * 12.5

# Bucle while
n = 100
pasos = 0
while n > 1:
    n = n // 2
    pasos += 1
print(f"log2(100) ≈ {pasos} pasos de bisección")

# %% Serie con bucle e impresión
print("Tabla de cuadrados:")
for k in range(1, 6):
    print(f"  {k}^2 = {k**2}")
