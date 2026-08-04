# %% Clases (POO) en el motor nativo
import math

class Seccion:
    def __init__(self, b, h):
        self.b = b
        self.h = h

    def area(self):
        return self.b * self.h

    def inercia(self):
        return self.b * self.h**3 / 12

    def modulo(self):
        return self.inercia() / (self.h / 2)

# %% Instanciar y usar
viga = Seccion(300, 600)
A = viga.area()
I = viga.inercia()
S = viga.modulo()
print(f"Sección {viga.b}x{viga.h}: A={A:.0f}  I={I:.3e}  S={S:.3e}")

# Lista de objetos + comprensión
secciones = [Seccion(b, 400) for b in [200, 250, 300, 350]]
areas = [s.area() for s in secciones]
mayor = max(areas)
