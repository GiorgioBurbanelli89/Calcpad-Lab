%% Benchmark BLAS — comparación naive C# vs OpenBLAS DGEMM
clear; clc;

%-- Crear matrices grandes (deterministico, sin random)
N = 200
A = zeros(N, N);
B = zeros(N, N);
for i = 1:N
    for j = 1:N
        A(i, j) = (i + j) * 0.01;
        B(i, j) = (i * j) * 0.001;
    end
end

fprintf('Matriz %dx%d construida\n', N, N);

%-- Multiplicación: BLAS si N >= 32 (threshold actual)
tic;
C = A * B;
t1 = toc;
fprintf('1 matmul %dx%d: tiempo = %.4f s\n', N, N, t1);

%-- Repetir 10 veces
tic;
for i = 1:10
    C = A * B;
end
t10 = toc;
fprintf('10 matmuls: tiempo = %.4f s  (avg %.4f s/op)\n', t10, t10/10);

%-- Sanity check: trace = sum_i (A*B)(i,i)
trC = 0;
for i = 1:N
    trC = trC + C(i, i);
end
fprintf('trace(C) = %g\n', trC);

%-- Operations / second
ops = 2 * N * N * N;     % 2*N^3 flops por matmul N×N
gflops = (ops / (t10/10)) / 1e9;
fprintf('Performance: %.2f GFLOPS\n', gflops);
