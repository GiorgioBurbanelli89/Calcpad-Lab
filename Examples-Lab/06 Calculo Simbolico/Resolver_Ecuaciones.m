% Resolver_Ecuaciones.m — Resolver ecuaciones simbolicas con solve()
% NOTA: Calcpad-Lab MVP usa solve(expr, x) asumiendo = 0.
clear; clc;
syms x a b c

fprintf('=== Ecuacion cuadratica generica ===\n');
sol = solve(a*x^2 + b*x + c, x);
for k = 1:length(sol)
    fprintf('  x_%d = %s\n', k, char(sol(k)));
end

fprintf('\n=== Raices de polinomios concretos ===\n');
casos = {x^2 - 5*x + 6, x^3 - x, x^2 - 9};
for k = 1:length(casos)
    s = solve(casos{k}, x);
    fprintf('Raices de %s:\n', char(casos{k}));
    for j = 1:length(s)
        fprintf('  x = %s\n', char(s(j)));
    end
end
