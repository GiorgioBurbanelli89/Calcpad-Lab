%% symunit en VECTORES y MATRICES (igual que MATLAB: unidad por elemento)
u = symunit;

%% Misma unidad para todo el vector
v = [10; 20; 30]*u.kN
dup = 2*v
suma = v + 5*u.kN

%% Matriz con unidad
K = [2 1; 1 2]*u.kN/u.m

%% DISTINTA unidad por elemento (como una matriz de rigidez FEM)
mix  = [5*u.m; 3*u.kN; 2*u.s]
Kmix = [3*u.kN/u.m, 2*u.kN; 2*u.kN, 5*u.kN*u.m]

%% Indexado preserva la unidad
e2  = v(2)
m2  = mix(2)
k11 = K(1,1)
