% ============================================================
%  Demo: myUnique sobre un vector con duplicados
% ============================================================
Y_demo = [3, 7, 1, 7, 3, 5, 1, 9, 5, 3];
fprintf('Entrada Y = [3, 7, 1, 7, 3, 5, 1, 9, 5, 3]\n');

[uniq, idx] = myUnique(Y_demo);

% --- Texto fprintf (en una sola linea cada vector) ---
fprintf('Elementos unicos: %s\n', mat2str(uniq));
fprintf('Primeros indices: %s\n', mat2str(idx));

% --- Echo de variables (render matematico) ---
Y_demo
uniq
idx

% ============================================================
%  Definicion de la funcion
% ============================================================
function [unique_elements, indices] = myUnique(Y)
    % Inicializar variables
    unique_elements = [];
    indices = [];
    checked = false(size(Y));

    % Recorrer el arreglo Y
    for i = 1:length(Y)
        if ~checked(i)
            % Obtener el elemento actual
            current_element = Y(i);
            
            % Encontrar todas las ocurrencias de current_element
            occurrences = find(Y == current_element);
            
            % Agregar el elemento unico y su indice
            unique_elements = [unique_elements current_element];
            indices = [indices occurrences(1)];  % Tomar solo el primer indice
            
            % Marcar todas las ocurrencias como revisadas
            checked(occurrences) = true;
        end
    end
end

