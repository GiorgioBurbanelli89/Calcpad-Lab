u = symunit;
% Rigidez mixta (kN/m, kN, kN, kN*m) x desplazamiento mixto (m, rad)
Kmix = [3*u.kN/u.m, 2*u.kN; 2*u.kN, 5*u.kN*u.m];
d = [2*u.m; 3];
f = Kmix*d
% otro: [kN/m, kN] . [m; 1] por fila
K2 = [3*u.kN/u.m, 2*u.kN; 4*u.kN/u.m, 6*u.kN];
d2 = [2*u.m; 5];
f2 = K2*d2
