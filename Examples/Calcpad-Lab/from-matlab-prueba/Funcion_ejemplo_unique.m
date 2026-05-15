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
            
            % Agregar el elemento único y su índice
            unique_elements = [unique_elements current_element];
            indices = [indices occurrences(1)];  % Tomar solo el primer índice
            
            % Marcar todas las ocurrencias como revisadas
            checked(occurrences) = true;
        end
    end
end

