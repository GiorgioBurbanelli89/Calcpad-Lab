% Sistema_Simbolico.m — Resolver sistema lineal en forma simbolica
clear; clc;
syms a b c x y z

fprintf('=== Sistema 2x2 simbolico ===\n');
% a*x + b*y = c
% b*x + a*y = -c
A = [a b; b a];
B = [c; -c];

% Resolver con \
sol = A \ B;
fprintf('x = %s\n', char(simplify(sol(1))));
fprintf('y = %s\n', char(simplify(sol(2))));

fprintf('\n=== Caso 3x3 numerico para comparar ===\n');
A2 = sym([2 1 -1; 1 3 2; 0 1 4]);
b2 = sym([5; 10; 8]);
sol2 = A2 \ b2;
fprintf('x = '); disp(sol2');
