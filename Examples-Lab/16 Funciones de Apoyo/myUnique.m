function [unique_elements, indices] = myUnique(Y)
    % Inicializar variables
    unique_elements = [];  % Arreglo para almacenar elementos únicos
    indices = [];           % Arreglo para almacenar índices de elementos únicos
    
    % Recorrer el arreglo Y
    for i = 1:length(Y)
        is_unique = true;  % Bandera para verificar si el elemento es único
        
        % Verificar si el elemento actual no está en unique_elements
        for j = 1:length(unique_elements)
            if Y(i) == unique_elements(j)
                is_unique = false;  % El elemento no es único
                break;              % Salir del bucle interno
            end
        end
        
        % Si el elemento es único, agregarlo a unique_elements y su índice a indices
        if is_unique
            unique_elements = [unique_elements Y(i)];
            indices = [indices i];
        end
    end
end
