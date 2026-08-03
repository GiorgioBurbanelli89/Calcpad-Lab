%% Solve FEM con unidades: K*u = f  ->  u = K\f  (el flujo estructural real)
u = symunit;

%% Rigidez de un elemento (unidades MIXTAS: kN/m, kN, kN*m)
K = [3*u.kN/u.m, 2*u.kN;
     2*u.kN,     5*u.kN*u.m]

%% Fuerzas aplicadas: fuerza (kN) y momento (kN*m)
f = [12*u.kN; 19*u.kN*u.m]

%% Solve: desplazamientos y giros (m, rad) -- unidades derivadas solas
d = K\f

%% Verificacion: K*d reproduce f
f_check = K*d
