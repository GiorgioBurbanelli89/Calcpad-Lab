# %% Módulo math (nativo en C#)
# El módulo `math` está implementado nativamente: no necesita Python instalado.
import math

# Constantes
pi = math.pi
e = math.e

# Trigonometría (radianes)
angulo = math.radians(30)
seno = math.sin(angulo)
coseno = math.cos(angulo)

# Raíz, potencia, logaritmo
r = math.sqrt(3**2 + 4**2)
ln2 = math.log(2)
log10_1000 = math.log10(1000)

# Redondeo
piso = math.floor(7.8)
techo = math.ceil(7.2)

# %% Aplicación: resultante de fuerzas
Fx = 30.0
Fy = 40.0
R = math.hypot(Fx, Fy)
theta = math.degrees(math.atan2(Fy, Fx))
print(f"R = {R:.2f} kN  ;  θ = {theta:.1f}°")
