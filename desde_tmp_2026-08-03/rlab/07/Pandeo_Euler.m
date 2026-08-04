% Pandeo_Euler.m — Carga critica de Euler para columna biarticulada
clear; clc;
syms E I L

fprintf('=== Carga critica de Euler (columna biarticulada) ===\n\n');

P_cr = pi^2 * E * I / L^2;
fprintf('P_cr = pi^2 * E * I / L^2 = %s\n\n', char(P_cr));

fprintf('=== Diferentes condiciones de borde (factor K) ===\n');
fprintf('  Biarticulada:    K = 1.0  ->  Lk = 1.0 * L\n');
fprintf('  Empot-empot:     K = 0.5  ->  Lk = 0.5 * L\n');
fprintf('  Empot-articul:   K = 0.7  ->  Lk = 0.7 * L\n');
fprintf('  Empot-libre:     K = 2.0  ->  Lk = 2.0 * L\n\n');

fprintf('=== Caso numerico (IPE 200, L=4 m) ===\n');
fprintf('I_min (en y) = 1.42e-6 m^4, E = 210 GPa\n');
P_num = double(subs(P_cr, {E, I, L}, {210e9, 1.42e-6, 4}));
fprintf('  P_cr = %.2f kN\n', P_num/1e3);
