% Norma_y_Condicion.m — Normas y numero de condicion de matrices
clear; clc;

A = [1 2 3;
     4 5 6;
     7 8 10];

fprintf('Matriz A:\n'); disp(A);

fprintf('=== Normas de matrices ===\n');
fprintf('  norm(A, 1)    = %.4f  (max columna suma)\n', norm(A, 1));
fprintf('  norm(A, 2)    = %.4f  (mayor valor singular)\n', norm(A, 2));
fprintf('  norm(A, inf)  = %.4f  (max fila suma)\n', norm(A, inf));
fprintf('  norm(A, fro)  = %.4f  (Frobenius)\n', norm(A, 'fro'));

fprintf('\n=== Numero de condicion ===\n');
fprintf('  cond(A) = %.4e\n', cond(A));
fprintf('  cond > 1e10 -> matriz mal condicionada (numericamente inestable)\n');

% Comparar con identidad (perfectamente condicionada)
fprintf('\ncond(eye(3)) = %.2f  (matriz identidad: condicion ideal)\n', cond(eye(3)));
