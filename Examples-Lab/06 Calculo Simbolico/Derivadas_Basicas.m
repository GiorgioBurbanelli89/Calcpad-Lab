% Derivadas_Basicas.m — Reglas elementales de derivacion simbolica
clear; clc;
syms x

fprintf('=== Derivadas elementales ===\n\n');

casos = {x^4, sin(x), cos(x), exp(x), log(x), x^2*sin(x)};
for k = 1:length(casos)
    f = casos{k};
    fprintf('d/dx [%s] = %s\n', char(f), char(diff(f, x)));
end

fprintf('\n=== Derivadas de orden superior ===\n');
g = x^5 + 3*x^3;
fprintf('g(x)    = %s\n', char(g));
fprintf('g''(x)  = %s\n', char(diff(g)));
fprintf('g''''(x) = %s\n', char(diff(g, 2)));
fprintf('g''''''(x)= %s\n', char(diff(g, 3)));
