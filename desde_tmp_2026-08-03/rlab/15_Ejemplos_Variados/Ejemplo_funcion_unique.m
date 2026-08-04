% Definir el vector Y
Y = [0.00 0.00 3.00 3.00 3.00 3.00];

% Obtener los valores únicos y los índices de las primeras apariciones
[a, b, c] = unique(Y);

% Mostrar los resultados
disp('Valores únicos:');
disp(a);
disp('Índices de las primeras apariciones:');
disp(b);
disp('Índices en el vector original:');
disp(c);
