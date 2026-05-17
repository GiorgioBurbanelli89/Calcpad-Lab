% Esfuerzos_Combinados.m — Tension normal combinada N + M
clear; clc;
syms N M A W

fprintf('=== Esfuerzos combinados N + M (flexion + axial) ===\n\n');

sigma_max = N/A + M/W;
sigma_min = N/A - M/W;

fprintf('Tension maxima (fibra mas comprimida):\n');
fprintf('  sigma_max = N/A + M/W = %s\n\n', char(sigma_max));

fprintf('Tension minima (fibra menos comprimida):\n');
fprintf('  sigma_min = N/A - M/W = %s\n\n', char(sigma_min));

fprintf('=== Caso numerico ===\n');
fprintf('Columna IPE 300: A = 5.38e-3 m^2, W = 5.57e-4 m^3\n');
fprintf('N = 150 kN (compresion), M = 30 kN*m\n');
sm = double(subs(sigma_max, {N, M, A, W}, {150e3, 30e3, 5.38e-3, 5.57e-4}));
sn = double(subs(sigma_min, {N, M, A, W}, {150e3, 30e3, 5.38e-3, 5.57e-4}));
fprintf('  sigma_max = %.2f MPa\n', sm/1e6);
fprintf('  sigma_min = %.2f MPa\n', sn/1e6);
