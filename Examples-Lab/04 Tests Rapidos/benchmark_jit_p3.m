%% Benchmark JIT Phase 3 — matrix literal + matrix arithmetic
clear; clc;

N = 50000

%-- A) Matrix literal en loop (Phase 3 target)
tic;
for i = 1:N
    pa = [i*0.1, i*0.2, i*0.3, i*0.4];
end
t_matlit = toc;
fprintf('A) Matrix literal %d iter: tiempo = %.3f s\n', N, t_matlit);

%-- B) Regresion scalar (Phase 1)
s = 0;
tic;
for i = 1:N
    s = s + i*i;
end
t_scalar = toc;
fprintf('B) Scalar accum %d iter: tiempo = %.3f s\n', N, t_scalar);

%-- C) Regresion indexing (Phase 2)
xs = zeros(N, 1);
tic;
for i = 1:N
    xs(i) = i*0.01 + 1;
end
t_idx = toc;
fprintf('C) Indexed write %d iter: tiempo = %.3f s\n', N, t_idx);
