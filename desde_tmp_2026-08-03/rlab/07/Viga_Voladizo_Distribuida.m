% Viga_Voladizo_Distribuida.m — Voladizo con carga uniforme
clear; clc;
syms q L E I x

fprintf('=== Voladizo con carga uniforme q ===\n\n');

% Momento por integracion de la carga (cortante = int q)
V_x = q*(L - x);
M_x = -q*(L - x)^2/2;
fprintf('Cortante:  V(x) = %s\n', char(V_x));
fprintf('Momento:   M(x) = %s\n', char(expand(M_x)));
fprintf('Momento maximo en empotramiento (x=0):\n');
fprintf('  M_max = %s\n\n', char(subs(M_x, x, 0)));

% Flecha clasica en la punta
delta_max = q*L^4/(8*E*I);
fprintf('Flecha maxima en la punta:\n');
fprintf('  delta = q*L^4/(8*E*I) = %s\n\n', char(delta_max));

fprintf('=== Caso numerico ===\n');
fprintf('q=5 kN/m, L=3 m, IPE 240 (I=3.89e-5 m^4), acero\n');
d = double(subs(delta_max, {q, L, E, I}, {5e3, 3, 210e9, 3.89e-5}));
fprintf('  delta_max = %.3f mm = L/%.0f\n', d*1000, 3/d);
