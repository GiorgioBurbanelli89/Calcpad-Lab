% Torsion_Circular.m — Torsion en eje cilindrico macizo
clear; clc;
syms T r R G L

fprintf('=== Torsion en eje cilindrico ===\n\n');

J = pi*R^4/2;
fprintf('Momento polar (seccion circular maciza): J = pi*R^4/2 = %s\n', char(J));

tau = T*r/J;
fprintf('Tension tangencial: tau(r) = T*r/J = %s\n', char(tau));

tau_max = T*R/J;
fprintf('Tension maxima (en r=R): tau_max = %s\n', char(simplify(tau_max)));

phi = T*L/(G*J);
fprintf('Angulo de torsion: phi = T*L/(G*J) = %s\n\n', char(phi));

fprintf('=== Caso numerico ===\n');
fprintf('Eje de acero: R = 25 mm, L = 1 m, T = 500 N*m, G = 80 GPa\n');
t = double(subs(tau_max, {T, R}, {500, 0.025}));
fprintf('  tau_max = %.2f MPa\n', t/1e6);
p = double(subs(phi, {T, L, G, R}, {500, 1, 80e9, 0.025}));
fprintf('  phi     = %.4f rad = %.3f grados\n', p, rad2deg(p));
