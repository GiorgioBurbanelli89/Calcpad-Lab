clc;  % Limpia la ventana de comandos
clear all;  % Borra todas las variables del espacio de trabajo

Y = [0.00; 0.00; 2.00; 3.00; 3.00];

% Implementación personalizada de unique usando bucles
ai = [];  % Arreglo para almacenar valores únicos
bi = [];  % Arreglo para almacenar índices de aparición de los valores únicos

% Bucle para identificar valores únicos y sus índices
for i = 1:length(Y)
    value = Y(i);  % Obtener el valor actual del arreglo Y en la posición i
    
    % Verificar si el valor ya está en ai
    if ~ismember(value, ai)
        ai = [ai; value];  % Agregar el valor único a ai
        bi = [bi; i];      % Agregar el índice de aparición a bi
    end
end

% Ordenar ai y ajustar bi para que coincida con los índices originales
[ai_sorted, sort_index] = sort(ai);
bi_sorted = bi(sort_index);

% Crear ci con los índices de aparición de los valores originales
ci = zeros(size(Y));  % Crear un arreglo de ceros del mismo tamaño que Y
for j = 1:length(ai_sorted)
    ci(Y == ai_sorted(j)) = j;  % Asignar índices secuenciales a los valores en Y
end

% Mostrar resultados en la ventana de comandos de manera vertical
disp('Valores únicos:');
disp(ai_sorted);

disp('Índices de aparición:');
disp(bi_sorted);

disp('Índices ordenados:');
disp(ci);
