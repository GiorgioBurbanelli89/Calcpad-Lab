% Resolver_Ecuaciones.m — Resolver ecuaciones simbolicas con solve()
% NOTA: Hekatan Lab MVP usa solve(expr, x) asumiendo = 0.
clear; clc;

%% Comentarios inline: oculto vs visible
%-- En Hekatan Lab, un comentario en el MISMO renglon que una asignacion:
%--   var = valor %texto    -> comentario OCULTO (anotacion de codigo, no se muestra)
%--   var = valor %'texto   -> comentario VISIBLE (el ' = marcador de texto Hekatan)
a = 5%este comentario esta oculto, no aparece en el reporte
b = 7%'este comentario si aparece, es texto visible

%% Ecuacion cuadratica generica (coeficientes simbolicos)
syms x a2 b2 c2
fprintf('=== Ecuacion cuadratica generica ===\n');
sol = solve(a2*x^2 + b2*x + c2, x);
for k = 1:length(sol)
    fprintf('  x_%d = %s\n', k, char(sol(k)));
end

%% Raices de polinomios concretos
fprintf('\n=== Raices de polinomios concretos ===\n');
casos = {x^2 - 5*x + 6, x^3 - x, x^2 - 9};
for k = 1:length(casos)
    s = solve(casos{k}, x);
    fprintf('Raices de %s:\n', char(casos{k}));
    for j = 1:length(s)
        fprintf('  x = %s\n', char(s(j)));
    end
end
