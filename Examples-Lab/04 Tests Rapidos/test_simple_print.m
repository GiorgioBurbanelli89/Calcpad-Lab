% Demo de las distintas formas de imprimir en MATLAB

%% (1) fprintf con strings literales
fprintf('Linea 1\n');
fprintf('Linea 2\n');
fprintf('Linea 3\n');

%% (2) fprintf con formato (numeros, strings, escapes)
nombre = 'Calcpad Lab';
version = 1.0;
fprintf('\n--- Identificacion ---\n');
fprintf('Producto: %s\n', nombre);
fprintf('Version:  %.1f\n', version);
fprintf('PI ~=     %.10f\n', pi);

%% (3) disp(): imprime sin formato, una linea por arg
disp('--- disp() ---');
disp(nombre);
disp(pi);
disp([1, 2, 3]);             % vector
disp([1, 2; 3, 4]);          % matriz 2x2

%% (4) Echo: asignacion SIN ';' al final -> render matematico
nombre
version
pi_estimado = pi
