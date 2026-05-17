% Integrales_Polinomicas.m — Integracion simbolica de polinomios y trigonometricas
clear; clc;
syms x

fprintf('=== Integrales indefinidas ===\n');
casos = {x^4, x^3 + 2*x, sin(x), cos(x), 3*x^2 + 5*x + 7};
for k = 1:length(casos)
    f = casos{k};
    fprintf('int (%s) dx = %s + C\n', char(f), char(int(f, x)));
end

fprintf('\n=== Integral definida (polinomio entre 0 y 1) ===\n');
syms a
f = x^3 + a*x;
F = int(f, x, 0, 1);
fprintf('int_0^1 (%s) dx = %s\n', char(f), char(F));
fprintf('Con a=2: %g\n', double(subs(F, a, 2)));
