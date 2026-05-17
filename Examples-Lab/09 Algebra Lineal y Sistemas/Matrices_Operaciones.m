% Matrices_Operaciones.m — Operaciones basicas con matrices
clear; clc;

A = [1 2 3; 4 5 6; 7 8 10];
B = [1 0 0; 0 1 0; 0 0 1];

fprintf('=== Matriz A ===\n');
disp(A);

fprintf('=== Operaciones ===\n');
fprintf('A + B:\n');     disp(A + B);
fprintf('A * 2:\n');     disp(A * 2);
fprintf('A * A:\n');     disp(A * A);
fprintf('A traspuesta:\n'); disp(A');
fprintf('Determinante de A: det(A) = %g\n', det(A));
fprintf('Traza de A:        trace(A) = %g\n', trace(A));
fprintf('Rango de A:        rank(A) = %d\n', rank(A));
