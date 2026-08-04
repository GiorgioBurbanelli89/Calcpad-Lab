% Tension_Normal_Barra.m — Tension axial en barra prismatica
clear; clc;
syms P A E L

fprintf('=== Barra a traccion pura ===\n\n');

sigma = P/A;
fprintf('Tension normal: sigma = P/A = %s\n', char(sigma));

epsilon = sigma/E;
fprintf('Deformacion unitaria: epsilon = sigma/E = %s\n', char(epsilon));

delta = sigma*L/E;
fprintf('Alargamiento total: delta = sigma*L/E = %s\n\n', char(delta));

fprintf('=== Caso numerico (perfil L 50x50x5 en acero) ===\n');
fprintf('P=50 kN, A=4.8 cm^2 = 4.8e-4 m^2, L=2 m, E=210 GPa\n');
s = double(subs(sigma, {P, A}, {50e3, 4.8e-4}));
fprintf('  sigma = %.1f MPa\n', s/1e6);
d = double(subs(delta, {P, A, L, E}, {50e3, 4.8e-4, 2, 210e9}));
fprintf('  delta = %.4f mm\n', d*1000);
