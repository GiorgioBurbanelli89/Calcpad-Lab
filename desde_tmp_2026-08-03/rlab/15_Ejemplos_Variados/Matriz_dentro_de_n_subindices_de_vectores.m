clc;clear all;
% Definir la matriz A
A = [2 1; 2 0];

% Inicializar la celda B para almacenar matrices
B = cell(numel(A), 1);

% Llenar B con A - i
index = 1;
for i = 1:numel(A)
    B{index} = A - i;
    index = index + 1;
end

% Mostrar los resultados
disp('A =');
disp(A);

disp('Matrices B:');
for i = 1:numel(B)
    disp(['B{' num2str(i) '} =']);
    disp(B{i});
end

% Mostrar un ejemplo específico B{1}
disp('Ejemplo específico B{1}:');
disp(B{1});

B{1}
disp('Texto a visualizar: ahora');