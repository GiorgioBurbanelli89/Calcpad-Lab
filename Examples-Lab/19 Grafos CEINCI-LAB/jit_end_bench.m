%% JIT y el indice 'end' — correccion + tiempo de ejecucion
% Verifica que v(end), v(end-1) y v(end+1) den lo MISMO que MATLAB 2017a,
% y mide cuanto cuesta cada patron dentro de un bucle.
clear all; close all;

N = 200000;

%% 1) 'end' como lectura
v = [10 20 30 40 50];
tic
s = 0;
for k = 1:N
    s = s + v(end);
end
t1 = toc;
fprintf('v(end)      s = %d        (esperado %d)   %.3f s\n', s, 50*N, t1);

tic
u = 0;
for k = 1:N
    u = u + v(end-1);
end
t2 = toc;
fprintf('v(end-1)    u = %d        (esperado %d)   %.3f s\n', u, 40*N, t2);

%% 2) 'end+1' haciendo CRECER el arreglo
M = 5000;
tic
c = [];
for k = 1:M
    c(end+1) = k;
end
t3 = toc;
fprintf('c(end+1)    len = %d      (esperado %d)   suma = %d (esperado %d)   %.3f s\n', ...
        length(c), M, sum(c), M*(M+1)/2, t3);

%% 3) referencia: mismo bucle PREASIGNADO (el JIT lo compila sin problema)
tic
d = zeros(1, M);
for k = 1:M
    d(k) = k;
end
t4 = toc;
fprintf('d(k) prealoc len = %d      suma = %d                       %.3f s\n', ...
        length(d), sum(d), t4);
