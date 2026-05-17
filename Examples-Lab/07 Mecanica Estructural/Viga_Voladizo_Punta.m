% Viga_Voladizo_Punta.m — Viga en voladizo con carga puntual en la punta
clear; clc;
syms P L E I x

fprintf('=== Viga en voladizo, carga P en la punta ===\n\n');

M_x = -P*(L - x);
fprintf('Momento flector: M(x) = %s\n', char(M_x));
fprintf('Momento maximo (en x=0): M_max = %s\n\n', char(subs(M_x, x, 0)));

% Flecha clasica
delta_max = P*L^3/(3*E*I);
fprintf('Flecha en la punta (formula clasica):\n');
fprintf('  delta = P*L^3 / (3*E*I) = %s\n\n', char(delta_max));

% Caso numerico: P=10 kN, L=2 m, IPE 200 (I=1.94e-5 m^4), acero
fprintf('=== Caso numerico ===\n');
fprintf('P = 10 kN, L = 2 m, IPE 200 (I = 1.94e-5 m^4), E = 210 GPa\n');
d = double(subs(delta_max, {P, L, E, I}, {10e3, 2, 210e9, 1.94e-5}));
fprintf('  delta_max = %.3f mm = L/%.0f\n', d*1000, 2/d);
