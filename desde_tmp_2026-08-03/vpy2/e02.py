# %% Strings y f-strings
# Formato de texto y números estilo Python (PEP 3101).

nombre = "Hormigón H-30"
fc = 30.0          # resistencia [MPa]
gamma = 24.0       # peso específico [kN/m3]

# f-strings con formato
print(f"Material: {nombre}")
print(f"f'c = {fc:.1f} MPa")
print(f"peso = {gamma:>8.2f} kN/m3")
print(f"notación científica: {fc*1e6:.3e} Pa")

# Métodos de string
codigo = "  viga-V12  "
limpio = codigo.strip().upper()
partes = "L1,L2,L3,L4".split(",")
unido = " + ".join(partes)

# Operador % (printf-style)
linea = "Tramo %d: M = %.2f kN·m" % (3, 145.678)
print(linea)
