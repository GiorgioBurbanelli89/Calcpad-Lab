%% Benchmark JIT Phase 4 — matrix lit 2D + slicing
clear; clc;

N = 30000

%-- A) Matrix literal 2D (Phase 4 target)
tic;
for i = 1:N
    A = [1, i, i+1; i+2, i+3, i+4];   %-- 2×3 matrix lit
end
t_2dlit = toc;
fprintf('A) Matrix lit 2D (2×3) %d iter: tiempo = %.3f s\n', N, t_2dlit);

%-- B) Slicing — A(:, j) column slice (Phase 4 target)
M = [1, 2, 3, 4; 5, 6, 7, 8; 9, 10, 11, 12];   %-- 3×4 matrix
acc = 0;
tic;
for i = 1:N
    j = 1 + (i - 1) - 4 * floor((i - 1) / 4);   % i mod 4 manual; ColonAll wrapper around it
    col = M(:, 1);    %-- columna 1 (fija pero usa slicing)
    acc = acc + col(2);
end
t_slice = toc;
fprintf('B) Column slice %d iter: tiempo = %.3f s\n', N, t_slice);

%-- C) Regresion scalar (Phase 1)
s = 0;
tic;
for i = 1:N
    s = s + i*i;
end
t_scalar = toc;
fprintf('C) Scalar accum %d iter: tiempo = %.3f s\n', N, t_scalar);
