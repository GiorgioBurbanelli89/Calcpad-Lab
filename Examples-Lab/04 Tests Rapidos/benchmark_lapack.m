%% Benchmark LAPACK DGESV vs solver C# nativo
clear; clc;

N = 500
fprintf('Construyendo sistema A·x = b con N = %d ...\n', N);

%-- Matriz tridiagonal-ish (bien condicionada)
A = zeros(N, N);
b = zeros(N, 1);
for i = 1:N
    A(i, i) = 4;
    if i > 1
        A(i, i-1) = -1;
        A(i-1, i) = -1;
    end
    b(i) = i*0.5;
end

%-- Solve con dispatch automatico (LAPACK si N >= 64)
tic;
x = A \ b;
t1 = toc;
fprintf('Solve %dx%d: %.4f s\n', N, N, t1);

%-- 5 solves consecutivos
tic;
for i = 1:5
    x = A \ b;
end
t5 = toc;
fprintf('5 solves: %.4f s (avg %.4f s/op)\n', t5, t5/5);

%-- Sanity check: ||A*x - b|| debe ser pequeño
resid = A * x - b;
norm_r = 0;
for i = 1:N
    norm_r = norm_r + resid(i)^2;
end
norm_r = sqrt(norm_r);
norm_b = 0;
for i = 1:N
    norm_b = norm_b + b(i)^2;
end
norm_b = sqrt(norm_b);
fprintf('||A*x - b|| / ||b|| = %.3e   (debe ser < 1e-10)\n', norm_r / norm_b);

%-- Operations / second
%-- Gauss elim ~ 2/3 N^3 FLOPs
ops = (2.0/3.0) * N^3;
gflops = (ops / (t5/5)) / 1e9;
fprintf('Performance: %.2f GFLOPS\n', gflops);
