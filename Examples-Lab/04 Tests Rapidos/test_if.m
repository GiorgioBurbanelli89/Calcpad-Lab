% Test de control de flujo: if / elseif / else
% Demo: clasificar un numero segun su signo y magnitud.

x = 5;
if x > 0
  y = 10;
  categoria = 'positivo';
elseif x == 0
  y = 0;
  categoria = 'cero';
else
  y = 20;
  categoria = 'negativo';
end

fprintf('x = %d => y = %d (categoria: %s)\n', x, y, categoria);

% Test con varios valores (loop sobre un vector)
valores = [-7, 0, 3, 12];
fprintf('\nClasificacion de varios valores:\n');
for v = valores
    if v > 0
        cat = 'positivo';
    elseif v == 0
        cat = 'cero';
    else
        cat = 'negativo';
    end
    fprintf('  v = %3d => %s\n', v, cat);
end

% Echo matematico del estado final
x
y
