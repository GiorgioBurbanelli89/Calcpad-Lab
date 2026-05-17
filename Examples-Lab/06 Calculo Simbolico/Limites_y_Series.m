% Limites_y_Series.m — Limites simbolicos y serie de Taylor
clear; clc;
syms x

fprintf('=== Limites notables ===\n');
fprintf('lim x->0 sin(x)/x = %s\n', char(limit(sin(x)/x, x, 0)));
fprintf('lim x->0 (1-cos(x))/x^2 = %s\n', char(limit((1-cos(x))/x^2, x, 0)));
fprintf('lim x->Inf (1+1/x)^x = %s\n', char(limit((1+1/x)^x, x, Inf)));

fprintf('\n=== Serie de Taylor (orden 5 en torno a 0) ===\n');
casos = {exp(x), sin(x), cos(x), log(1+x)};
nombres = {'exp(x)', 'sin(x)', 'cos(x)', 'log(1+x)'};
for k = 1:length(casos)
    t = taylor(casos{k}, x, 'Order', 5);
    fprintf('  %s ~ %s\n', nombres{k}, char(t));
end
