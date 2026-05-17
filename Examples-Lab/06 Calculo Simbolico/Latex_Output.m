% Latex_Output.m — Generar codigo LaTeX desde expresiones simbolicas
clear; clc;
syms x

fprintf('=== Expresiones en formato LaTeX ===\n');
casos = {x^2 + 3*x, diff(x^3), int(x^2 + 1, x), x^4 + 2*x^2 + 1};
for k = 1:length(casos)
    e = casos{k};
    fprintf('%s  ->  $$%s$$\n', char(e), latex(e));
end

fprintf('\n=== Util para reportes Markdown/HTML ===\n');
fprintf('Pegar entre $$..$$ en Markdown:\n');
syms a b c
formula = (-b + sqrt(b^2 - 4*a*c))/(2*a);
fprintf('  Formula cuadratica:\n');
fprintf('  $$x = %s$$\n', latex(formula));
