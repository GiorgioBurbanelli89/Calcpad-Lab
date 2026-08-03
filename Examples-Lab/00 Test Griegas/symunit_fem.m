%% FEM completo con unidades: ensamblar K -> aplicar BC -> resolver K\f
u = symunit;

%% 2 resortes en serie (k = 10 kN/m c/u), 3 nodos
ke = [10*u.kN/u.m, -10*u.kN/u.m; -10*u.kN/u.m, 10*u.kN/u.m];
Kg = zeros(3,3);
Kg([1 2],[1 2]) = Kg([1 2],[1 2]) + ke;   % elemento 1
Kg([2 3],[2 3]) = Kg([2 3],[2 3]) + ke;   % elemento 2
Kg

%% Nodo 1 fijo -> resolver DOFs libres [2 3]
Kff = Kg([2 3],[2 3]);
f = [0*u.kN; 5*u.kN];      % carga 5 kN en el nodo 3
d = Kff\f                  % desplazamientos (m) -- unidad derivada sola

%% Reacciones: R = K*d (verificacion)
R = Kff*d
