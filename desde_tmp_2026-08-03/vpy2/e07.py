# %% Propiedades de sección rectangular y verificación a flexión
# Hoja de cálculo de ingeniería con el motor Python nativo.
import math

# %% Datos
b = 300.0      # base [mm]
h = 500.0      # altura [mm]
fc = 25.0      # resistencia hormigón [MPa]
fy = 420.0     # fluencia acero [MPa]
M_u = 180.0    # momento último [kN·m]

# %% Propiedades geométricas
A = b * h                  # área [mm2]
I = b * h**3 / 12          # inercia [mm4]
S = I / (h / 2)            # módulo resistente [mm3]
y_cg = h / 2               # centro de gravedad [mm]

# %% Armadura requerida (método aproximado)
d = h - 50.0                       # peralte efectivo [mm]
j = 0.9                            # brazo de palanca aprox.
As_req = (M_u * 1e6) / (fy * j * d)   # acero requerido [mm2]

# Número de barras Ø16 (201 mm2 c/u)
area_barra = math.pi * 16**2 / 4
n_barras = math.ceil(As_req / area_barra)
As_prov = n_barras * area_barra

cuantia = As_prov / (b * d)

print(f"As requerido = {As_req:.0f} mm2")
print(f"Usar {n_barras} Ø16 → As = {As_prov:.0f} mm2 (ρ = {cuantia*100:.2f} %)")
