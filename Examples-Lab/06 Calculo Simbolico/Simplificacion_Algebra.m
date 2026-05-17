% Simplificacion_Algebra.m — Simplify, expand, factor
clear; clc;
syms x y

fprintf('=== Simplificacion ===\n');
e1 = (sin(x)^2 + cos(x)^2) * (x^2 - 1)/(x-1);
fprintf('  Original:  %s\n', char(e1));
fprintf('  Simplify:  %s\n\n', char(simplify(e1)));

fprintf('=== Expansion ===\n');
e2 = (x + 1)^4;
fprintf('  (x+1)^4 = %s\n', char(expand(e2)));

e3 = (x + y)*(x - y);
fprintf('  (x+y)(x-y) = %s\n\n', char(expand(e3)));

fprintf('=== Factorizacion ===\n');
e4 = x^3 - 1;
fprintf('  factor(x^3 - 1) = %s\n', char(prod(factor(e4))));
e5 = x^2 - 4;
fprintf('  factor(x^2 - 4) = %s\n', char(prod(factor(e5))));
