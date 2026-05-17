% Sustitucion_Numerica.m — Evaluar expresiones simbolicas en valores concretos
% NOTA: Calcpad-Lab MVP usa subs(expr, var, val) en encadenadas (no cells).
clear; clc;
syms x a b

fprintf('=== Sustitucion en una variable ===\n');
f = x^3 - 4*x + 7;
fprintf('f(x) = %s\n', char(f));
for v = [-2 -1 0 1 2]
    fprintf('  f(%d) = %g\n', v, double(subs(f, x, v)));
end

fprintf('\n=== Sustitucion en multiples variables ===\n');
g = a*x^2 + b*x;
fprintf('g(x) = %s\n', char(g));
g1 = subs(g, a, 2);
g1 = subs(g1, b, -3);
fprintf('Con a=2, b=-3: %s\n', char(g1));
fprintf('  evaluado en x=4: %g\n', double(subs(g1, x, 4)));
