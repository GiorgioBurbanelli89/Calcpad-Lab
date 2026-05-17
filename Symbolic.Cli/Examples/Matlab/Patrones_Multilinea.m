% Patrones_Multilinea.m
% =========================================================================
% EJEMPLO 1 - Patrones de texto multilinea en MATLAB / Calcpad-Lab
% =========================================================================
% MATLAB R2017a NO tiene heredoc ni triple comilla. Estas son las 10 formas
% conocidas de imprimir texto multilinea. Funcionan tanto en MATLAB pleno
% como en Calcpad-Lab MATLAB-mode.
%
% Ejecutar:
%   - MATLAB: F5 desde el editor
%   - Calcpad-Lab CLI: CalcpadLabCli.exe Patrones_Multilinea.m
% =========================================================================

clear; clc;
nombre = 'Viga IPE 300';
M     = 67.50;
W     = 287.23;
delta = 14.42;

% -------------------------------------------------------------------------
% PATRON A — fprintf con array [...] y continuacion ...
% El mas usado. Un solo fprintf por parrafo, args al final.
% -------------------------------------------------------------------------
fprintf('=== PATRON A: fprintf + array + continuacion ===\n');
fprintf([...
    'Resultados para %s:\n'...
    '  Momento maximo M = %.2f kN*m\n'...
    '  Modulo W         = %.2f cm^3\n'...
    '  Flecha delta     = %.2f mm\n'...
    '\n'], nombre, M, W, delta);

% -------------------------------------------------------------------------
% PATRON B — sprintf arma el texto + disp lo muestra
% Ventaja: el texto queda en una variable, util para log o archivo.
% -------------------------------------------------------------------------
fprintf('=== PATRON B: sprintf + disp ===\n');
txt = sprintf([...
    'Resultados para %s:\n'...
    '  M = %.2f kN*m\n'...
    '  W = %.2f cm^3'], nombre, M, W);
disp(txt);
fprintf('\n');

% -------------------------------------------------------------------------
% PATRON C — multiples disp consecutivos (solo texto fijo)
% -------------------------------------------------------------------------
fprintf('=== PATRON C: disp consecutivos ===\n');
disp('Verificacion estructural:');
disp('  - Resistencia OK');
disp('  - Servicio OK');
fprintf('\n');

% -------------------------------------------------------------------------
% PATRON D — concatenacion inline con num2str
% -------------------------------------------------------------------------
fprintf('=== PATRON D: concatenacion inline ===\n');
fprintf(['Resultado: ' num2str(M) ' kN*m\n' ...
         'Tension:   ' num2str(W) ' cm^3\n']);
fprintf('\n');

% -------------------------------------------------------------------------
% PATRON E — for + fprintf sobre cell array (lista de items)
% -------------------------------------------------------------------------
fprintf('=== PATRON E: cell array + for ===\n');
items = {'Cargas calculadas', 'Reacciones resueltas', 'Diagramas listos'};
for k = 1:length(items)
    fprintf('  %d) %s\n', k, items{k});
end
fprintf('\n');

% -------------------------------------------------------------------------
% PATRON F — tabla nombre/valor desde arrays paralelos
% -------------------------------------------------------------------------
fprintf('=== PATRON F: tabla nombre/valor ===\n');
nombres = {'M_max', 'W_req', 'delta'};
vals    = [M, W, delta];
unidades = {'kN*m', 'cm^3', 'mm'};
for k = 1:length(vals)
    fprintf('  %-7s = %8.2f  %s\n', nombres{k}, vals(k), unidades{k});
end
fprintf('\n');

% -------------------------------------------------------------------------
% PATRON G — char matrix vertical con ; (filas mismo ancho)
% -------------------------------------------------------------------------
fprintf('=== PATRON G: char matrix vertical ===\n');
bloque = ['Linea uno  ';
          'Linea dos  ';
          'Linea tres '];
disp(bloque);
fprintf('\n');

% -------------------------------------------------------------------------
% PATRON H — for + fprintf para series de datos (tabla)
% Alternativa MATLAB-pleno: compose(...) vectorizado (R2016b+). En
% Calcpad-Lab MVP no esta implementado, usar este loop equivalente.
% -------------------------------------------------------------------------
fprintf('=== PATRON H: tabla con for + fprintf ===\n');
x_vals = 1:5;
for k = 1:length(x_vals)
    fprintf('  x = %d  ->  x^2 = %d\n', x_vals(k), x_vals(k)^2);
end
fprintf('\n');

% -------------------------------------------------------------------------
% PATRON I — strjoin con separador \n (cell -> string unico)
% -------------------------------------------------------------------------
fprintf('=== PATRON I: strjoin con \\n ===\n');
partes = {'Primera observacion.', 'Segunda observacion.', 'Tercera observacion.'};
texto = strjoin(partes, sprintf('\n'));
disp(texto);
fprintf('\n');

% -------------------------------------------------------------------------
% PATRON J — plantilla en variable, reutilizable con args distintos
% -------------------------------------------------------------------------
fprintf('=== PATRON J: plantilla reutilizable ===\n');
PLANTILLA = [...
    '+------------------------+\n'...
    '|  %s\n'...
    '|  Valor: %.2f %s\n'...
    '+------------------------+\n'];

fprintf(PLANTILLA, 'Caso A', M, 'kN*m');
fprintf(PLANTILLA, 'Caso B', W, 'cm^3');

% -------------------------------------------------------------------------
% Nota sobre escape de comilla simple
% -------------------------------------------------------------------------
fprintf('\n=== Escape de comilla simple ===\n');
fprintf('Dentro de '''' va '''' como escape:  don''t worry, y''(x) = M(x)\n');
fprintf('\n=== FIN PATRONES MULTILINEA ===\n');
