% Test directo de funcion con multiples outputs.
% El archivo es SCRIPT (no function-file): el codigo de demo va al inicio
% y la funcion local va al final. MATLAB R2016b+ syntax.

[p, q, r] = triple_out(10);
fprintf('p=%g q=%g r=%g\n', p, q, r);

% Echo matematico
p
q
r

% Definicion de la funcion local
function [a, b, c] = triple_out(x)
    a = x;
    b = x * 2;
    c = x * 3;
end
