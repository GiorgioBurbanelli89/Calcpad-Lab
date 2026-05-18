%% Benchmark JIT Phase 2 — matrix indexing + function call dispatch
clear; clc;

%-- Patrón típico de mesh generation: loop con array indexing scalar.
%-- Phase 1 bail-out (CallOrIndex no soportado). Phase 2 compila.

N = 500000

%-- A) Array indexed write/read
xs = zeros(N, 1);
ys = zeros(N, 1);
tic;
for i = 1:N
    xs(i) = i*0.01;
    ys(i) = xs(i)*xs(i) + 2*xs(i) + 1;
end
t_index = toc;
fprintf('A) Array indexing %d iter: tiempo = %.3f s\n', N, t_index);

%-- B) Reducción escalar (Phase 1 target)
s = 0;
tic;
for i = 1:N
    s = s + i*i;
end
t_scalar = toc;
fprintf('B) Scalar accum %d iter: tiempo = %.3f s\n', N, t_scalar);

%-- C) Mixed: scalar + indexed write
acc = 0;
zs = zeros(N, 1);
tic;
for i = 1:N
    u = i*0.001;
    v = u*u - u + 1;
    zs(i) = v;
    acc = acc + v;
end
t_mixed = toc;
fprintf('C) Mixed scalar+index %d iter: tiempo = %.3f s\n', N, t_mixed);
