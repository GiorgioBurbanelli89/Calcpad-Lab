# %% NumPy — álgebra lineal con el motor embebido
# Calcpad Suite Py trae numpy embebido (arrays, matmul, linalg) sin
# necesidad de un Python externo. Aquí resolvemos un sistema axial de
# 2 grados de libertad con np.linalg.solve.
import numpy as np

# Dos barras axiales en serie: nudo 0 (fijo) - nudo 1 - nudo 2
E = 200000.0   # MPa
A = 2500.0     # mm2
L1 = 3000.0    # mm  (barra 0-1)
L2 = 2000.0    # mm  (barra 1-2)
k1 = E * A / L1
k2 = E * A / L2

# Matriz de rigidez reducida (GDL libres u1, u2), ensamblada directamente
Kred = np.array([[k1 + k2, -k2],
                 [-k2,      k2]])
print("Matriz de rigidez reducida K [N/mm]:")
print(Kred)

# Fuerza de 50 kN aplicada en el nudo 2
F = np.array([0.0, 50000.0])     # N
u = np.linalg.solve(Kred, F)
print(f"\nDesplazamiento nudo 1 = {u[0]:.4f} mm")
print(f"Desplazamiento nudo 2 = {u[1]:.4f} mm")
